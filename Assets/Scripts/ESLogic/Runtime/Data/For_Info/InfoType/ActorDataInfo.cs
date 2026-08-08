using System;
using Sirenix.OdinInspector;
using UnityEngine;
using System.Collections.Generic;

namespace ES
{
    [ESCreatePath("数据信息", "角色数据信息")]
    /// <summary>
    /// 非 GameCore 的通用角色定义（Player/Rider/StoryActor 等）。
    /// Monster 与 NPC 必须使用各自独立的 GameCore 根 SO，禁止通过 Actor 注入。
    /// </summary>
    public class ActorDataInfo : SoDataInfo
    {
        [Title("角色定位")]
        [LabelText("角色类型")]
        public ActorDataKind actorKind = ActorDataKind.Player;

        [LabelText("显示名称")]
        public string displayName;

        [LabelText("说明")]
        [MultiLineProperty(3)]
        public string description;

        [Title("出生 Tag")]
        [LabelText("出生时添加")]
        [Tooltip("角色出生后持续持有的事实。Entity Prefab 不重复保存此列表。")]
        public List<ESTagStableReference> tags = new List<ESTagStableReference>();

        [Title("运动共享配置")]
        [InfoBox("Shared motion data is read-mostly runtime definition. Buffs and skills should modify variable/runtime data instead.")]
        [HideLabel]
        public EntityMotionSharedData motionShared = EntityMotionSharedData.Default;

        [Title("运动变量配置")]
        [InfoBox("Variable motion data is the spawn/runtime default. Gameplay changes should target runtime variables, not shared data.")]
        [HideLabel]
        public EntityMotionVariableData motionVariable = EntityMotionVariableData.Default;

        [Button("初始化通用角色运动配置")]
        public void InitDefaultMotion()
        {
            motionShared = EntityMotionSharedData.Default;
            motionVariable = EntityMotionVariableData.Default;
        }

        [Button("Init Action Demo Motion")]
        public void InitActionDemoMotion()
        {
            motionShared = EntityMotionSharedData.ActionDemo;
            motionVariable = EntityMotionVariableData.Default;
        }

    }

    public enum ActorDataKind
    {
        [InspectorName("玩家")]
        Player = 0,

        [InspectorName("Rider")]
        Rider = 3,

        [InspectorName("剧情角色")]
        StoryActor = 4
    }

    [Serializable]
    public sealed class EntityMotionSharedData
    {
        [Title("Main Motion Abilities")]
        [LabelText("启用地面移动")]
        public bool enableGroundMove;

        [LabelText("启用跳跃")]
        public bool enableJump;

        [LabelText("启用下蹲")]
        public bool enableCrouch;

        [LabelText("启用飞行")]
        public bool enableFly;

        [LabelText("Enable Climb")]
        public bool enableClimb;

        [LabelText("启用骑乘")]
        public bool enableMount;

        [LabelText("预留立体机动")]
        public bool enableGrappleMotion;

        [Title("地面参数")]
        [LabelText("最大地面速度")]
        public float maxStableMoveSpeed;

        [LabelText("地面响应速度")]
        public float stableMovementSharpness;

        [LabelText("朝向响应速度")]
        public float orientationSharpness;

        [LabelText("最大空中速度")]
        public float maxAirMoveSpeed;

        [LabelText("空中加速度")]
        public float airAccelerationSpeed;

        [LabelText("跳跃速度")]
        public float jumpSpeed;

        [Title("斜面/台阶策略")]
        [LabelText("Max Stable Slope Angle")]
        [Range(0f, 89f)]
        public float maxStableSlopeAngle;

        [LabelText("陡坡滑落速度")]
        public float steepSlopeSlideSpeed;

        [LabelText("上坡速度倍率")]
        public float uphillSpeedMultiplier;

        [LabelText("下坡速度倍率")]
        public float downhillSpeedMultiplier;

        [LabelText("Downhill Inertia")]
        public float downhillInertia;

        [LabelText("动态平台继承速度")]
        public bool inheritMovingPlatformVelocity;

        [LabelText("台阶适应")]
        public EntityMotionStepPolicy stepPolicy;

        [Title("飞行策略")]
        [LabelText("飞行模式")]
        public EntityFlyControlMode flyControlMode;

        [LabelText("飞行最大速度")]
        public float flyMaxSpeed;

        [LabelText("飞行冲刺倍率")]
        public float flySprintMultiplier;

        [LabelText("悬停制动")]
        public float flyHoverBrake;

        [LabelText("俯冲加速度")]
        public float flyDiveAcceleration;

        [Title("骑乘策略")]
        [LabelText("载具接管输入")]
        public bool mountVehicleConsumesInput;

        [LabelText("骑乘时锁定角色速度")]
        public bool mountLockRiderVelocity;

        [LabelText("骑乘对齐完成后由载具同步")]
        public bool mountSyncAfterMatchTarget;

        public static EntityMotionSharedData Default => new EntityMotionSharedData
        {
            enableGroundMove = true,
            enableJump = true,
            enableCrouch = true,
            enableFly = false,
            enableClimb = false,
            enableMount = false,
            enableGrappleMotion = false,
            maxStableMoveSpeed = 8f,
            stableMovementSharpness = 15f,
            orientationSharpness = 10f,
            maxAirMoveSpeed = 8f,
            airAccelerationSpeed = 5f,
            jumpSpeed = 8f,
            maxStableSlopeAngle = 55f,
            steepSlopeSlideSpeed = 4f,
            uphillSpeedMultiplier = 0.9f,
            downhillSpeedMultiplier = 1.05f,
            downhillInertia = 0.15f,
            inheritMovingPlatformVelocity = true,
            stepPolicy = EntityMotionStepPolicy.CharacterController,
            flyControlMode = EntityFlyControlMode.CameraRelative,
            flyMaxSpeed = 10f,
            flySprintMultiplier = 1.5f,
            flyHoverBrake = 8f,
            flyDiveAcceleration = 12f,
            mountVehicleConsumesInput = true,
            mountLockRiderVelocity = true,
            mountSyncAfterMatchTarget = true
        };

        /// <summary>把 Table 自有的运行时默认对象原位恢复为领域默认值，不产生新对象。</summary>
        internal void ResetToDefaults()
        {
            enableGroundMove = true;
            enableJump = true;
            enableCrouch = true;
            enableFly = false;
            enableClimb = false;
            enableMount = false;
            enableGrappleMotion = false;
            maxStableMoveSpeed = 8f;
            stableMovementSharpness = 15f;
            orientationSharpness = 10f;
            maxAirMoveSpeed = 8f;
            airAccelerationSpeed = 5f;
            jumpSpeed = 8f;
            maxStableSlopeAngle = 55f;
            steepSlopeSlideSpeed = 4f;
            uphillSpeedMultiplier = 0.9f;
            downhillSpeedMultiplier = 1.05f;
            downhillInertia = 0.15f;
            inheritMovingPlatformVelocity = true;
            stepPolicy = EntityMotionStepPolicy.CharacterController;
            flyControlMode = EntityFlyControlMode.CameraRelative;
            flyMaxSpeed = 10f;
            flySprintMultiplier = 1.5f;
            flyHoverBrake = 8f;
            flyDiveAcceleration = 12f;
            mountVehicleConsumesInput = true;
            mountLockRiderVelocity = true;
            mountSyncAfterMatchTarget = true;
        }

        public static EntityMotionSharedData ActionDemo
        {
            get
            {
                var data = Default;
                data.enableFly = true;
                data.enableClimb = true;
                data.enableMount = true;
                data.enableGrappleMotion = true;
                data.maxStableMoveSpeed = 9f;
                data.maxAirMoveSpeed = 9f;
                data.flyMaxSpeed = 14f;
                data.flySprintMultiplier = 1.8f;
                return data;
            }
        }
    }

    [Serializable]
    public struct EntityMotionVariableData
    {
        [Title("Spawn Runtime Values")]
        [LabelText("Initial Support Flag")]
        public StateSupportFlags initialSupportFlag;

        [LabelText("速度倍率")]
        public float speedMultiplier;

        [LabelText("速度上限(<=0 不限制)")]
        public float speedLimit;

        [LabelText("重力倍率")]
        public float gravityMultiplier;

        [Title("控制权限")]
        [LabelText("允许移动输入")]
        public bool allowMoveInput;

        [LabelText("允许转向输入")]
        public bool allowLookInput;

        [LabelText("允许跳跃")]
        public bool allowJump;

        [LabelText("允许切换运动模式")]
        public bool allowMotionModeSwitch;

        [LabelText("Allow Root Motion")]
        public bool allowRootMotion;

        public static EntityMotionVariableData Default => new EntityMotionVariableData
        {
            initialSupportFlag = StateSupportFlags.Grounded,
            speedMultiplier = 1f,
            speedLimit = 0f,
            gravityMultiplier = 1f,
            allowMoveInput = true,
            allowLookInput = true,
            allowJump = true,
            allowMotionModeSwitch = true,
            allowRootMotion = true
        };
    }

    public enum EntityMotionStepPolicy
    {
        [InspectorName("交给 KCC")]
        CharacterController = 0,

        [InspectorName("足部 IK 辅助")]
        FootIKAssist = 1,

        [InspectorName("严格物理")]
        StrictPhysics = 2
    }

    public enum EntityFlyControlMode
    {
        [InspectorName("相机方向")]
        CameraRelative = 0,

        [InspectorName("角色朝向")]
        CharacterForward = 1,

        [InspectorName("锁定目标")]
        TargetRelative = 2
    }
}
