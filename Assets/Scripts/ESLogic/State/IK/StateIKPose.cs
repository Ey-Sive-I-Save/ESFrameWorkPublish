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
    /// StateMachine -> FinalIK 的最终输出 Pose（四肢 + LookAt）。
    /// 该结构为零GC值类型，用于每帧缓存与 LateUpdate 驱动。
    /// </summary>
    [Serializable]
    public struct IKGoalPose
    {
        public float weight;
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 hintPosition;

        public void Reset()
        {
            weight = 0f;
            position = Vector3.zero;
            rotation = Quaternion.identity;
            hintPosition = Vector3.zero;
        }
    }

    [Serializable]
    public struct StateIKPose
    {
        public IKGoalPose leftHand;
        public IKGoalPose rightHand;
        public IKGoalPose leftFoot;
        public IKGoalPose rightFoot;

        public float lookAtWeight;
        public Vector3 lookAtPosition;
        public float lookAtBodyWeight;
        public float lookAtHeadWeight;
        public float lookAtEyesWeight;
        public float lookAtClampWeight;

        public bool HasAnyWeight
        {
            get
            {
                return leftHand.weight > 0.001f || rightHand.weight > 0.001f ||
                       leftFoot.weight > 0.001f || rightFoot.weight > 0.001f ||
                       lookAtWeight > 0.001f;
            }
        }

        public void Reset()
        {
            leftHand.Reset();
            rightHand.Reset();
            leftFoot.Reset();
            rightFoot.Reset();
            lookAtWeight = 0f;
            lookAtPosition = Vector3.zero;
            lookAtBodyWeight = 0.5f;
            lookAtHeadWeight = 1f;
            lookAtEyesWeight = 1f;
            lookAtClampWeight = 0.5f;
        }
    }
}
