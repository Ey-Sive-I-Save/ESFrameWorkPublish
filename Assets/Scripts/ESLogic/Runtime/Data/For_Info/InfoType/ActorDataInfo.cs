using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [ESCreatePath("鏁版嵁淇℃伅", "瑙掕壊鏁版嵁淇℃伅")]
    public class ActorDataInfo : SoDataInfo
    {
        [Title("瑙掕壊瀹氫綅")]
        [LabelText("瑙掕壊绫诲瀷")]
        public ActorDataKind actorKind = ActorDataKind.Player;

        [LabelText("鏄剧ず鍚嶇О")]
        public string displayName;

        [LabelText("璇存槑")]
        [MultiLineProperty(3)]
        public string description;

        [Title("Runtime Key")]
        [ShowIf(nameof(ShowNpcKey))]
        [HideLabel, InlineProperty]
        public ESNpcConfigKey npcKey = new ESNpcConfigKey();

        [ShowIf(nameof(ShowMonsterKey))]
        [HideLabel, InlineProperty]
        public ESMonsterConfigKey monsterKey = new ESMonsterConfigKey();

        [Title("杩愬姩鍏变韩閰嶇疆")]
        [InfoBox("Shared motion data is read-mostly runtime definition. Buffs and skills should modify variable/runtime data instead.")]
        [HideLabel]
        public EntityMotionSharedData motionShared = EntityMotionSharedData.Default;

        [Title("杩愬姩鍙橀噺閰嶇疆")]
        [InfoBox("Variable motion data is the spawn/runtime default. Gameplay changes should target runtime variables, not shared data.")]
        [HideLabel]
        public EntityMotionVariableData motionVariable = EntityMotionVariableData.Default;

        [Button("鍒濆鍖栭€氱敤瑙掕壊杩愬姩閰嶇疆")]
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

        private bool ShowNpcKey()
        {
            return actorKind == ActorDataKind.NPC;
        }

        private bool ShowMonsterKey()
        {
            return actorKind == ActorDataKind.Monster;
        }
    }

    public enum ActorDataKind
    {
        [InspectorName("鐜╁")]
        Player = 0,

        [InspectorName("NPC")]
        NPC = 1,

        [InspectorName("鎬墿")]
        Monster = 2,

        [InspectorName("Rider")]
        Rider = 3,

        [InspectorName("鍓ф儏瑙掕壊")]
        StoryActor = 4
    }

    [Serializable]
    public struct EntityMotionSharedData
    {
        [Title("Main Motion Abilities")]
        [LabelText("鍚敤鍦伴潰绉诲姩")]
        public bool enableGroundMove;

        [LabelText("鍚敤璺宠穬")]
        public bool enableJump;

        [LabelText("鍚敤涓嬭共")]
        public bool enableCrouch;

        [LabelText("鍚敤椋炶")]
        public bool enableFly;

        [LabelText("Enable Climb")]
        public bool enableClimb;

        [LabelText("鍚敤楠戜箻")]
        public bool enableMount;

        [LabelText("棰勭暀绔嬩綋鏈哄姩")]
        public bool enableGrappleMotion;

        [Title("鍦伴潰鍙傛暟")]
        [LabelText("鏈€澶у湴闈㈤€熷害")]
        public float maxStableMoveSpeed;

        [LabelText("鍦伴潰鍝嶅簲閫熷害")]
        public float stableMovementSharpness;

        [LabelText("鏈€澶х┖涓€熷害")]
        public float maxAirMoveSpeed;

        [LabelText("绌轰腑鍔犻€熷害")]
        public float airAccelerationSpeed;

        [LabelText("璺宠穬閫熷害")]
        public float jumpSpeed;

        [Title("鏂滈潰/鍙伴樁绛栫暐")]
        [LabelText("Max Stable Slope Angle")]
        [Range(0f, 89f)]
        public float maxStableSlopeAngle;

        [LabelText("闄″潯婊戣惤閫熷害")]
        public float steepSlopeSlideSpeed;

        [LabelText("涓婂潯閫熷害鍊嶇巼")]
        public float uphillSpeedMultiplier;

        [LabelText("涓嬪潯閫熷害鍊嶇巼")]
        public float downhillSpeedMultiplier;

        [LabelText("Downhill Inertia")]
        public float downhillInertia;

        [LabelText("鍔ㄦ€佸钩鍙扮户鎵块€熷害")]
        public bool inheritMovingPlatformVelocity;

        [LabelText("鍙伴樁閫傚簲")]
        public EntityMotionStepPolicy stepPolicy;

        [Title("椋炶绛栫暐")]
        [LabelText("椋炶妯″紡")]
        public EntityFlyControlMode flyControlMode;

        [LabelText("椋炶鏈€澶ч€熷害")]
        public float flyMaxSpeed;

        [LabelText("椋炶鍐插埡鍊嶇巼")]
        public float flySprintMultiplier;

        [LabelText("鎮仠鍒跺姩")]
        public float flyHoverBrake;

        [LabelText("淇啿鍔犻€熷害")]
        public float flyDiveAcceleration;

        [Title("楠戜箻绛栫暐")]
        [LabelText("杞藉叿鎺ョ杈撳叆")]
        public bool mountVehicleConsumesInput;

        [LabelText("楠戜箻鏃堕攣瀹氳鑹查€熷害")]
        public bool mountLockRiderVelocity;

        [LabelText("楠戜箻瀵归綈瀹屾垚鍚庣敱杞藉叿鍚屾")]
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

        [LabelText("閫熷害鍊嶇巼")]
        public float speedMultiplier;

        [LabelText("閫熷害涓婇檺(<=0 涓嶉檺鍒?")]
        public float speedLimit;

        [LabelText("閲嶅姏鍊嶇巼")]
        public float gravityMultiplier;

        [Title("鎺у埗鏉冮檺")]
        [LabelText("鍏佽绉诲姩杈撳叆")]
        public bool allowMoveInput;

        [LabelText("鍏佽杞悜杈撳叆")]
        public bool allowLookInput;

        [LabelText("鍏佽璺宠穬")]
        public bool allowJump;

        [LabelText("鍏佽鍒囨崲杩愬姩妯″紡")]
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
        [InspectorName("浜ょ粰 KCC")]
        CharacterController = 0,

        [InspectorName("鑴?IK 杈呭姪")]
        FootIKAssist = 1,

        [InspectorName("涓ユ牸鐗╃悊")]
        StrictPhysics = 2
    }

    public enum EntityFlyControlMode
    {
        [InspectorName("鐩告満鏂瑰悜")]
        CameraRelative = 0,

        [InspectorName("瑙掕壊鏈濆悜")]
        CharacterForward = 1,

        [InspectorName("閿佸畾鐩爣")]
        TargetRelative = 2
    }
}
