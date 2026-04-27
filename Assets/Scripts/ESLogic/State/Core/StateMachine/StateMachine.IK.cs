using System;
using UnityEngine;

namespace ES
{
    public partial class StateMachine
    {
        private void UpdateStateGeneralFinalIKDriverPoseCache(float deltaTime)
        {
            stateGeneralFinalIKDriverPose.Reset();

            bool captureIKContributionSummary = false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var dbg = StateMachineDebugSettings.Instance;
            captureIKContributionSummary = dbg != null && dbg.IsIKContributionSummaryEnabled;
#endif

            if (captureIKContributionSummary)
            {
                _ikContributionBuilder.Length = 0;
            }

            StateGeneralFinalIKDriverPose upper = default;
            StateGeneralFinalIKDriverPose lower = default;
            StateGeneralFinalIKDriverPose buff = default;
            StateGeneralFinalIKDriverPose main = default;
            StateGeneralFinalIKDriverPose @base = default;
            upper.Reset();
            lower.Reset();
            buff.Reset();
            main.Reset();
            @base.Reset();

            var allRunningStates = GetRunningStatesList();
            if (captureIKContributionSummary)
            {
                _ikContributionBuilder.Append("RunningStates=").Append(allRunningStates.Count).AppendLine();
            }

            for (int i = 0; i < allRunningStates.Count; i++)
            {
                var state = allRunningStates[i];
                if (state == null)
                {
                    if (captureIKContributionSummary)
                        _ikContributionBuilder.Append("#").Append(i).Append(" <null> Skip:state null").AppendLine();
                    continue;
                }

                string stateKey = string.IsNullOrEmpty(state.strKey) ? "<NoKey>" : state.strKey;

                if (state.baseStatus != StateBaseStatus.Running)
                {
                    if (captureIKContributionSummary)
                        _ikContributionBuilder.Append("#").Append(i).Append(' ').Append(stateKey)
                            .Append(" Skip:status=").Append(state.baseStatus).AppendLine();
                    continue;
                }

                var runtime = state.AnimationRuntime;
                if (runtime == null)
                {
                    if (captureIKContributionSummary)
                        _ikContributionBuilder.Append("#").Append(i).Append(' ').Append(stateKey)
                            .Append(" Skip:runtime null").AppendLine();
                    continue;
                }

                if (!runtime.ik.enabled)
                {
                    if (captureIKContributionSummary)
                        _ikContributionBuilder.Append("#").Append(i).Append(' ').Append(stateKey)
                            .Append(" Skip:ik disabled")
                            .Append(" | Raw(lh/rh/lf/rf/look)=")
                            .Append(runtime.ik.leftHand.targetWeight.ToString("F3")).Append('/')
                            .Append(runtime.ik.rightHand.targetWeight.ToString("F3")).Append('/')
                            .Append(runtime.ik.leftFoot.targetWeight.ToString("F3")).Append('/')
                            .Append(runtime.ik.rightFoot.targetWeight.ToString("F3")).Append('/')
                            .Append(runtime.ik.lookAt.targetWeight.ToString("F3"))
                            .AppendLine();
                    continue;
                }

                if (!runtime.ik.HasAnyTargetWeight)
                {
                    if (captureIKContributionSummary)
                        _ikContributionBuilder.Append("#").Append(i).Append(' ').Append(stateKey)
                            .Append(" Skip:all target weights <= 0").AppendLine();
                    continue;
                }

                float master = 1f;

                if (!stateLayerMap.TryGetValue(state, out var layerType))
                {
                    layerType = state.stateSharedData != null ? state.stateSharedData.basicConfig.layerType : StateLayerType.Main;
                }

                if (captureIKContributionSummary)
                {
                    _ikContributionBuilder.Append("#").Append(i).Append(' ').Append(stateKey)
                        .Append(" | Layer=").Append(layerType)
                        .Append(" | Master=").Append(master.ToString("F3"))
                        .Append(" | IKActive=").Append(state.IsIKActive ? "Y" : "N")
                        .Append(" | Raw(lh/rh/lf/rf/look)=")
                        .Append(runtime.ik.leftHand.targetWeight.ToString("F3")).Append('/')
                        .Append(runtime.ik.rightHand.targetWeight.ToString("F3")).Append('/')
                        .Append(runtime.ik.leftFoot.targetWeight.ToString("F3")).Append('/')
                        .Append(runtime.ik.rightFoot.targetWeight.ToString("F3")).Append('/')
                        .Append(runtime.ik.lookAt.targetWeight.ToString("F3"))
                        .Append(" | Effective(lh/rh/lf/rf/look)=")
                        .Append((runtime.ik.leftHand.targetWeight * master).ToString("F3")).Append('/')
                        .Append((runtime.ik.rightHand.targetWeight * master).ToString("F3")).Append('/')
                        .Append((runtime.ik.leftFoot.targetWeight * master).ToString("F3")).Append('/')
                        .Append((runtime.ik.rightFoot.targetWeight * master).ToString("F3")).Append('/')
                        .Append((runtime.ik.lookAt.targetWeight * master).ToString("F3"))
                        .AppendLine();
                }

                ref var target = ref GetLayerPoseRef(layerType, ref upper, ref lower, ref buff, ref main, ref @base);
                AccumulateFromRuntime(ref target, state, master);
            }

            ComposeOverride(ref stateGeneralFinalIKDriverPose, ref upper);
            ComposeOverride(ref stateGeneralFinalIKDriverPose, ref lower);
            ComposeOverride(ref stateGeneralFinalIKDriverPose, ref buff);
            ComposeOverride(ref stateGeneralFinalIKDriverPose, ref main);
            ComposeOverride(ref stateGeneralFinalIKDriverPose, ref @base);

            ClampPoseWeights(ref stateGeneralFinalIKDriverPose);

            var cb = OnStateGeneralFinalIKDriverPosePostProcess;
            if (cb != null)
            {
                cb(this, ref stateGeneralFinalIKDriverPose, deltaTime);
                ClampPoseWeights(ref stateGeneralFinalIKDriverPose);
            }

            stateGeneralFinalIKContributionSummary = captureIKContributionSummary
                ? _ikContributionBuilder.ToString()
                : "IK贡献诊断已关闭（在 StateMachineDebugSettings 中开启 IK贡献明细）";
        }

        private static ref StateGeneralFinalIKDriverPose GetLayerPoseRef(
            StateLayerType layerType,
            ref StateGeneralFinalIKDriverPose upper,
            ref StateGeneralFinalIKDriverPose lower,
            ref StateGeneralFinalIKDriverPose buff,
            ref StateGeneralFinalIKDriverPose main,
            ref StateGeneralFinalIKDriverPose @base)
        {
            switch (layerType)
            {
                case StateLayerType.UpperBody: return ref upper;
                case StateLayerType.LowerBody: return ref lower;
                case StateLayerType.Buff: return ref buff;
                case StateLayerType.Base: return ref @base;
                case StateLayerType.Main:
                default: return ref main;
            }
        }

        private static void AccumulateFromRuntime(ref StateGeneralFinalIKDriverPose pose, StateBase state, float master)
        {
            ref var ik = ref state.AnimationRuntime.ik;
            var proceduralDriveConfig = state.stateSharedData != null ? state.stateSharedData.proceduralDriveConfig : null;
            float normalizedProgress = Mathf.Clamp01(state.normalizedProgress);

            bool runtimeDrivenIK = state.IsIKActive;
            bool useProceduralIKConfig = proceduralDriveConfig != null && !runtimeDrivenIK;

            float leftHandWeight = (useProceduralIKConfig ? proceduralDriveConfig.GetGoalTargetWeight(IKGoal.LeftHand, normalizedProgress) : 1f) * ik.leftHand.targetWeight * master;
            float rightHandWeight = (useProceduralIKConfig ? proceduralDriveConfig.GetGoalTargetWeight(IKGoal.RightHand, normalizedProgress) : 1f) * ik.rightHand.targetWeight * master;
            float leftFootWeight = (useProceduralIKConfig ? proceduralDriveConfig.GetGoalTargetWeight(IKGoal.LeftFoot, normalizedProgress) : 1f) * ik.leftFoot.targetWeight * master;
            float rightFootWeight = (useProceduralIKConfig ? proceduralDriveConfig.GetGoalTargetWeight(IKGoal.RightFoot, normalizedProgress) : 1f) * ik.rightFoot.targetWeight * master;

            float leftHandLerpingRate = (useProceduralIKConfig ? proceduralDriveConfig.GetGoalLerpingRate(IKGoal.LeftHand, normalizedProgress) : 1f) * ik.leftHand.lerpingRate;
            float rightHandLerpingRate = (useProceduralIKConfig ? proceduralDriveConfig.GetGoalLerpingRate(IKGoal.RightHand, normalizedProgress) : 1f) * ik.rightHand.lerpingRate;
            float leftFootLerpingRate = (useProceduralIKConfig ? proceduralDriveConfig.GetGoalLerpingRate(IKGoal.LeftFoot, normalizedProgress) : 1f) * ik.leftFoot.lerpingRate;
            float rightFootLerpingRate = (useProceduralIKConfig ? proceduralDriveConfig.GetGoalLerpingRate(IKGoal.RightFoot, normalizedProgress) : 1f) * ik.rightFoot.lerpingRate;

            AccumulateGoal(ref pose.leftHand, leftHandWeight, leftHandLerpingRate, ik.leftHand.position, ik.leftHand.rotation, ik.leftHand.hintPosition);
            AccumulateGoal(ref pose.rightHand, rightHandWeight, rightHandLerpingRate, ik.rightHand.position, ik.rightHand.rotation, ik.rightHand.hintPosition);
            AccumulateGoal(ref pose.leftFoot, leftFootWeight, leftFootLerpingRate, ik.leftFoot.position, ik.leftFoot.rotation, ik.leftFoot.hintPosition);
            AccumulateGoal(ref pose.rightFoot, rightFootWeight, rightFootLerpingRate, ik.rightFoot.position, ik.rightFoot.rotation, ik.rightFoot.hintPosition);

            var lookAtConfig = useProceduralIKConfig ? proceduralDriveConfig.GetResolvedLookAtConfig(normalizedProgress) : ResolvedIKLookAtConfig.Disabled;
            float lookConfigWeight = useProceduralIKConfig ? (lookAtConfig.enabled ? Mathf.Clamp01(lookAtConfig.targetWeight) : 0f) : 1f;
            float lookW = lookConfigWeight * ik.lookAt.targetWeight * master;
            if (lookW > 0.001f)
            {
                float lookAtLerpingRate = (useProceduralIKConfig ? Mathf.Clamp(lookAtConfig.lerpingRate, 0.05f, 8f) : 1f) * ik.lookAt.lerpingRate;
                AccumulateLookAt(ref pose, lookW, lookAtLerpingRate, ik.lookAt.position, state);
            }
        }

        private static void AccumulateGoal(ref IKGoalPose goal, float w, float lerpingRate, Vector3 pos, Quaternion rot, Vector3 hintPos)
        {
            if (w <= 0.001f) return;
            float normalizedLerpingRate = Mathf.Clamp(lerpingRate, 0.05f, 8f);

            if (goal.weight <= 0.001f)
            {
                goal.weight = w;
                goal.lerpingRate = normalizedLerpingRate;
                goal.position = pos;
                goal.rotation = rot;
                goal.hintPosition = hintPos;
                return;
            }

            float newW = goal.weight + w;
            float t = w / newW;
            goal.lerpingRate = Mathf.Lerp(goal.lerpingRate, normalizedLerpingRate, t);
            goal.position = Vector3.Lerp(goal.position, pos, t);
            goal.rotation = Quaternion.Slerp(goal.rotation, rot, t);

            if (hintPos != Vector3.zero)
            {
                goal.hintPosition = (goal.hintPosition == Vector3.zero) ? hintPos : Vector3.Lerp(goal.hintPosition, hintPos, t);
            }

            goal.weight = newW;
        }

        private static void AccumulateLookAt(ref StateGeneralFinalIKDriverPose pose, float w, float lerpingRate, Vector3 lookAtPos, StateBase state)
        {
            float normalizedLerpingRate = Mathf.Clamp(lerpingRate, 0.05f, 8f);
            if (pose.lookAtWeight <= 0.001f)
            {
                pose.lookAtWeight = w;
                pose.lookAtLerpingRate = normalizedLerpingRate;
                pose.lookAtPosition = lookAtPos;
                ApplyLookAtConfig(ref pose, state);
                return;
            }

            float newW = pose.lookAtWeight + w;
            float t = w / newW;
            pose.lookAtLerpingRate = Mathf.Lerp(pose.lookAtLerpingRate, normalizedLerpingRate, t);
            pose.lookAtPosition = Vector3.Lerp(pose.lookAtPosition, lookAtPos, t);
            pose.lookAtWeight = newW;
        }

        private static void ApplyLookAtConfig(ref StateGeneralFinalIKDriverPose pose, StateBase state)
        {
            var proceduralDriveConfig = state.stateSharedData != null ? state.stateSharedData.proceduralDriveConfig : null;
            var cfg = proceduralDriveConfig != null ? proceduralDriveConfig.GetResolvedLookAtConfig(state.normalizedProgress) : ResolvedIKLookAtConfig.Disabled;
            if (cfg.enabled)
            {
                pose.lookAtBodyWeight = cfg.bodyWeight;
                pose.lookAtHeadWeight = cfg.headWeight;
                pose.lookAtEyesWeight = cfg.eyesWeight;
                pose.lookAtClampWeight = cfg.clampWeight;
            }
            else
            {
                pose.lookAtBodyWeight = 0.5f;
                pose.lookAtHeadWeight = 1f;
                pose.lookAtEyesWeight = 1f;
                pose.lookAtClampWeight = 0.5f;
            }
        }

        private static void ComposeOverride(ref StateGeneralFinalIKDriverPose dst, ref StateGeneralFinalIKDriverPose src)
        {
            if (dst.leftHand.weight <= 0.001f && src.leftHand.weight > 0.001f) dst.leftHand = src.leftHand;
            if (dst.rightHand.weight <= 0.001f && src.rightHand.weight > 0.001f) dst.rightHand = src.rightHand;
            if (dst.leftFoot.weight <= 0.001f && src.leftFoot.weight > 0.001f) dst.leftFoot = src.leftFoot;
            if (dst.rightFoot.weight <= 0.001f && src.rightFoot.weight > 0.001f) dst.rightFoot = src.rightFoot;

            if (dst.lookAtWeight <= 0.001f && src.lookAtWeight > 0.001f)
            {
                dst.lookAtWeight = src.lookAtWeight;
                dst.lookAtLerpingRate = src.lookAtLerpingRate;
                dst.lookAtPosition = src.lookAtPosition;
                dst.lookAtBodyWeight = src.lookAtBodyWeight;
                dst.lookAtHeadWeight = src.lookAtHeadWeight;
                dst.lookAtEyesWeight = src.lookAtEyesWeight;
                dst.lookAtClampWeight = src.lookAtClampWeight;
            }
        }

        private static void ClampPoseWeights(ref StateGeneralFinalIKDriverPose pose)
        {
            pose.leftHand.weight = Mathf.Clamp01(pose.leftHand.weight);
            pose.rightHand.weight = Mathf.Clamp01(pose.rightHand.weight);
            pose.leftFoot.weight = Mathf.Clamp01(pose.leftFoot.weight);
            pose.rightFoot.weight = Mathf.Clamp01(pose.rightFoot.weight);
            pose.lookAtWeight = Mathf.Clamp01(pose.lookAtWeight);
        }
    }
}