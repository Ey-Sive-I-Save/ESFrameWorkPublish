using System;
using UnityEngine;

namespace ES
{
    public enum IKGoal
    {
        LeftHand = 0,
        RightHand = 1,
        LeftFoot = 2,
        RightFoot = 3,
    }

    /// <summary>
    /// StateMachine -> StateGeneralFinalIKDriver 的通用姿态快照（四肢 + LookAt）。
    /// 不包含 AimIK、Grounder、HitReaction、Recoil 等其它 IK/程序动画通道。
    /// 该结构为零 GC 值类型，用于每帧缓存与 LateUpdate 驱动。
    /// </summary>
    [Serializable]
    public struct IKGoalPose
    {
        public float weight;
        // lerping 速度倍率，不是静态混合值：1 为默认速度，小于 1 更慢，大于 1 更快。
        public float lerpingRate;
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 hintPosition;

        public void Reset()
        {
            weight = 0f;
            lerpingRate = 1f;
            position = Vector3.zero;
            rotation = Quaternion.identity;
            hintPosition = Vector3.zero;
        }
    }

    [Serializable]
    public struct StateGeneralFinalIKDriverPose
    {
        public IKGoalPose leftHand;
        public IKGoalPose rightHand;
        public IKGoalPose leftFoot;
        public IKGoalPose rightFoot;

        public float lookAtWeight;
        public float lookAtLerpingRate;
        public Vector3 lookAtPosition;
        public float lookAtBodyWeight;
        public float lookAtHeadWeight;
        public float lookAtEyesWeight;
        public float lookAtClampWeight;

        /// <summary>四肢中任意一肢有权重（BipedIK 专用检查，不含 LookAt）。</summary>
        public bool HasLimbWeight =>
            leftHand.weight > 0.001f || rightHand.weight > 0.001f ||
            leftFoot.weight > 0.001f || rightFoot.weight > 0.001f;

        /// <summary>四肢或 LookAt 任意有权重。</summary>
        public bool HasAnyWeight => HasLimbWeight || lookAtWeight > 0.001f;

        public void Reset()
        {
            leftHand.Reset();
            rightHand.Reset();
            leftFoot.Reset();
            rightFoot.Reset();
            lookAtWeight = 0f;
            lookAtLerpingRate = 1f;
            lookAtPosition = Vector3.zero;
            lookAtBodyWeight = 0.5f;
            lookAtHeadWeight = 1f;
            lookAtEyesWeight = 1f;
            lookAtClampWeight = 0.5f;
        }
    }
}
