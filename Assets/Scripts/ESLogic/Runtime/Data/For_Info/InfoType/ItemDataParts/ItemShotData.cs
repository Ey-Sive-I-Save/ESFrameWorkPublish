using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [Serializable]
    public sealed class ItemShotSharedData
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
        public LayerMask hitLayers = ESPhysicsLayers.ShotHitMask;

        [ShowIf(nameof(enabled))]
        [LabelText("使用重力")]
        public bool useGravity;

        [ShowIf(nameof(enabled))]
        [LabelText("朝向速度方向")]
        public bool orientToVelocity;

        [ShowIf(nameof(enabled))]
        [LabelText("允许必中")]
        public bool allowMustHit;

        [ShowIf(nameof(enabled)), Title("命中资格")]
        [LabelText("命中 Tag 条件")]
        [Tooltip("飞行物只生成物理候选。命中判定会读取此条件决定是否继续命中；这里不结算伤害、阵营或部位倍率。")]
        public ESHitTagEligibility hitTagEligibility = new ESHitTagEligibility();

        [ShowIf(nameof(enabled)), Title("冲击策略")]
        [InlineProperty, HideLabel]
        public ShotImpactDefinitionData impact = ShotImpactDefinitionData.Default;

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
            hitLayers = ESPhysicsLayers.ShotHitMask,
            useGravity = false,
            orientToVelocity = true,
            allowMustHit = true,
            hitTagEligibility = new ESHitTagEligibility(),
            impact = ShotImpactDefinitionData.Default
        };

        /// <summary>把 Table 自有的运行时默认对象原位恢复为领域默认值，不产生新对象。</summary>
        internal void ResetToDefaults()
        {
            enabled = true;
            aimMode = ShotAimMode.Free;
            blockMode = ShotBlockMode.AnyBlocker;
            launchDelay = 0f;
            warmupTime = 0f;
            speed = 30f;
            acceleration = 120f;
            maxSpeed = 30f;
            trackingStartTime = 0f;
            trackingDuration = -1f;
            turnSpeed = 720f;
            lifeTime = 5f;
            radius = 0.05f;
            hitLayers = ESPhysicsLayers.ShotHitMask;
            useGravity = false;
            orientToVelocity = true;
            allowMustHit = true;
            hitTagEligibility = new ESHitTagEligibility();
            impact = ShotImpactDefinitionData.Default;
        }

        public bool ValidateDefinition(out string error)
        {
            if (!enabled)
            {
                error = "ShotDefinition 必须启用。";
                return false;
            }
            if ((uint)aimMode > (uint)ShotAimMode.Scan
                || (uint)blockMode > (uint)ShotBlockMode.AnyBlocker)
            {
                error = "ShotDefinition 的瞄准模式或阻挡模式无效。";
                return false;
            }
            if (!IsFinite(launchDelay)
                || !IsFinite(warmupTime)
                || !IsFinite(speed)
                || !IsFinite(acceleration)
                || !IsFinite(maxSpeed)
                || !IsFinite(trackingStartTime)
                || !IsFinite(trackingDuration)
                || !IsFinite(turnSpeed)
                || !IsFinite(lifeTime)
                || !IsFinite(radius))
            {
                error = "ShotDefinition 的运动参数必须是有限数值。";
                return false;
            }
            if (launchDelay < 0f || warmupTime < 0f || speed < 0f || maxSpeed < 0f
                || trackingStartTime < 0f || turnSpeed < 0f || lifeTime <= 0f || radius < 0f)
            {
                error = "ShotDefinition 的时间、速度、转向、寿命和半径参数超出合法范围。";
                return false;
            }
            if (hitLayers.value == 0)
            {
                error = "ShotDefinition 的命中层不能为空。";
                return false;
            }
            if (aimMode == ShotAimMode.MustHit && !allowMustHit)
            {
                error = "必中瞄准模式要求允许必中。";
                return false;
            }
            if (hitTagEligibility != null
                && !hitTagEligibility.TryPrepare(out string tagEligibilityError))
            {
                error = "ShotDefinition 的命中 Tag 条件无效：" + tagEligibilityError;
                return false;
            }
            if (impact == null)
            {
                error = "ShotDefinition 缺少冲击策略。";
                return false;
            }
            if (!impact.Validate(out error))
                return false;

            error = string.Empty;
            return true;
        }

        internal bool Internal_TryCreatePreparedCopy(
            out ItemShotSharedData prepared,
            out string error)
        {
            prepared = null;
            if (!ValidateDefinition(out error))
                return false;

            ESHitTagEligibility preparedEligibility = null;
            if (hitTagEligibility != null
                && !hitTagEligibility.Internal_TryCreatePreparedCopy(
                    out preparedEligibility,
                    out error))
            {
                return false;
            }

            prepared = new ItemShotSharedData
            {
                enabled = enabled,
                aimMode = aimMode,
                blockMode = blockMode,
                launchDelay = launchDelay,
                warmupTime = warmupTime,
                speed = speed,
                acceleration = acceleration,
                maxSpeed = maxSpeed,
                trackingStartTime = trackingStartTime,
                trackingDuration = trackingDuration,
                turnSpeed = turnSpeed,
                lifeTime = lifeTime,
                radius = radius,
                hitLayers = hitLayers,
                useGravity = useGravity,
                orientToVelocity = orientToVelocity,
                allowMustHit = allowMustHit,
                hitTagEligibility = preparedEligibility,
                impact = impact.Internal_CreatePreparedCopy()
            };
            error = null;
            return true;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

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
    public sealed class ShotImpactDefinitionData
    {
        [LabelText("反弹次数"), MinValue(0), MaxValue(16)]
        public int bounceCount;

        [LabelText("反弹速度保留"), Range(0.05f, 1f)]
        public float bounceVelocityScale = 0.8f;

        [LabelText("爆炸半径"), MinValue(0f)]
        public float explosionRadius;

        [LabelText("爆炸目标上限"), MinValue(1), MaxValue(128)]
        public int explosionTargetCapacity = 16;

        [LabelText("链式半径"), MinValue(0f)]
        public float chainRadius;

        [LabelText("链式目标数"), MinValue(0), MaxValue(32)]
        public int chainTargetCount;

        public static ShotImpactDefinitionData Default => new ShotImpactDefinitionData();

        public bool Validate(out string error)
        {
            if (bounceCount < 0 || bounceCount > 16
                || float.IsNaN(bounceVelocityScale)
                || float.IsInfinity(bounceVelocityScale)
                || bounceVelocityScale < 0.05f
                || bounceVelocityScale > 1f
                || float.IsNaN(explosionRadius)
                || float.IsInfinity(explosionRadius)
                || explosionRadius < 0f
                || explosionTargetCapacity < 1
                || explosionTargetCapacity > 128
                || float.IsNaN(chainRadius)
                || float.IsInfinity(chainRadius)
                || chainRadius < 0f
                || chainTargetCount < 0
                || chainTargetCount > 32)
            {
                error = "Shot 冲击策略参数超出合法范围。";
                return false;
            }

            if (chainTargetCount > 0 && chainRadius <= 0f)
            {
                error = "启用链式目标时必须配置大于零的链式半径。";
                return false;
            }

            error = null;
            return true;
        }

        internal ShotImpactDefinitionData Internal_CreatePreparedCopy()
        {
            return new ShotImpactDefinitionData
            {
                bounceCount = bounceCount,
                bounceVelocityScale = bounceVelocityScale,
                explosionRadius = explosionRadius,
                explosionTargetCapacity = explosionTargetCapacity,
                chainRadius = chainRadius,
                chainTargetCount = chainTargetCount
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

        public bool ValidateDefinition(out string error)
        {
            if (!IsFinite(speedMultiplier)
                || !IsFinite(lifeTimeMultiplier)
                || !IsFinite(radiusMultiplier)
                || !IsFinite(launchDelay)
                || !IsFinite(trackingStartTime)
                || !IsFinite(spreadAngle)
                || !IsFinite(targetOffset.x)
                || !IsFinite(targetOffset.y)
                || !IsFinite(targetOffset.z))
            {
                error = "Shot 的每发变量必须是有限数值。";
                return false;
            }
            if (speedMultiplier <= 0f || lifeTimeMultiplier <= 0f || radiusMultiplier <= 0f)
            {
                error = "Shot 的速度、寿命和半径倍率必须大于零。";
                return false;
            }
            if (launchDelay < 0f || trackingStartTime < 0f || spreadAngle < 0f)
            {
                error = "Shot 的可变延迟、追踪开始时间和散射角不能为负数。";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
