using RootMotion.FinalIK;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// StateFinalIKDriver 对制作工具公开的稳定语义契约。
    /// 字段继续保持 Driver 私有；外部工具不得依赖序列化字段名或 SerializedObject。
    /// </summary>
    public sealed partial class StateFinalIKDriver
    {
        /// <summary>
        /// 将 Driver 设为不携带 Solver 的轻量模板桥。正式 Variant 只能在补齐 Solver 后再显式开启能力。
        /// </summary>
        public void ConfigureSolverFreeTemplateBaseline()
        {
            enableBipedIK = false;
            enableGrounderBipedIK = false;
            enableLookAtIK = false;
            enableAimIK = false;
            enableFullBodyBipedIK = false;
            enableHitReaction = false;
            enableRecoil = false;

            autoAddBipedIK = false;
            autoAddGrounderBipedIK = false;
            autoAddLookAtIK = false;
            autoAddAimIK = false;
            autoAddFullBodyBipedIK = false;
            autoAddHitReaction = false;
            autoAddRecoil = false;
            logMissingComponentHints = true;
        }

        /// <summary>验证已开启的能力及其 Solver/前置依赖；关闭的能力不要求挂载 Solver。</summary>
        public bool ValidateEnabledSolverContract(out string error)
        {
            BipedIK biped = GetComponent<BipedIK>();
            GrounderBipedIK grounder = GetComponent<GrounderBipedIK>();
            LookAtIK lookAt = GetComponent<LookAtIK>();
            AimIK aim = GetComponent<AimIK>();
            FullBodyBipedIK fullBody = GetComponent<FullBodyBipedIK>();
            HitReaction hitReaction = GetComponent<HitReaction>();
            Recoil recoil = GetComponent<Recoil>();

            if (enableBipedIK && biped == null)
            {
                error = "已启用 BipedIK，但同一对象没有 BipedIK Solver。";
                return false;
            }
            if (enableGrounderBipedIK && (!enableBipedIK || biped == null || grounder == null))
            {
                error = "GrounderBipedIK 需要已启用的 BipedIK 及 GrounderBipedIK Solver。";
                return false;
            }
            if (enableLookAtIK && lookAt == null)
            {
                error = "已启用 LookAtIK，但同一对象没有 LookAtIK Solver。";
                return false;
            }
            if (enableAimIK && aim == null)
            {
                error = "已启用 AimIK，但同一对象没有 AimIK Solver。";
                return false;
            }
            if (enableFullBodyBipedIK && fullBody == null)
            {
                error = "已启用 FullBodyBipedIK，但同一对象没有 FullBodyBipedIK Solver。";
                return false;
            }
            if (enableHitReaction && (!enableFullBodyBipedIK || fullBody == null || hitReaction == null))
            {
                error = "HitReaction 需要已启用的 FullBodyBipedIK、FullBodyBipedIK Solver 和 HitReaction Solver。";
                return false;
            }
            if (enableRecoil && (!enableFullBodyBipedIK || fullBody == null || recoil == null))
            {
                error = "Recoil 需要已启用的 FullBodyBipedIK、FullBodyBipedIK Solver 和 Recoil Solver。";
                return false;
            }

            error = string.Empty;
            return true;
        }

        /// <summary>验证基础/池模板的无 Solver 轻量基线。</summary>
        public bool IsSolverFreeTemplateBaseline(out string error)
        {
            bool allFeaturesDisabled = !enableBipedIK
                                       && !enableGrounderBipedIK
                                       && !enableLookAtIK
                                       && !enableAimIK
                                       && !enableFullBodyBipedIK
                                       && !enableHitReaction
                                       && !enableRecoil;
            bool autoAddDisabled = !autoAddBipedIK
                                   && !autoAddGrounderBipedIK
                                   && !autoAddLookAtIK
                                   && !autoAddAimIK
                                   && !autoAddFullBodyBipedIK
                                   && !autoAddHitReaction
                                   && !autoAddRecoil;
            bool hasNoSolver = GetComponent<BipedIK>() == null
                               && GetComponent<GrounderBipedIK>() == null
                               && GetComponent<LookAtIK>() == null
                               && GetComponent<AimIK>() == null
                               && GetComponent<FullBodyBipedIK>() == null
                               && GetComponent<HitReaction>() == null
                               && GetComponent<Recoil>() == null;
            if (!allFeaturesDisabled || !autoAddDisabled || !hasNoSolver || !logMissingComponentHints)
            {
                error = "模板 Driver 必须关闭全部 FinalIK 功能和自动加组件，不携带 Solver，并开启缺失组件提示。";
                return false;
            }

            error = string.Empty;
            return true;
        }

        /// <summary>从有效 Humanoid Animator 生成 Driver 的统一骨骼绑定。</summary>
        public bool ConfigureHumanoidBinding(Animator animator)
        {
            if (!IsValidHumanoid(animator))
                return false;

            useDriverBoneBinding = true;
            bindingRoot = animator.transform;
            bindingPelvis = animator.GetBoneTransform(HumanBodyBones.Hips);
            bindingSpine = animator.GetBoneTransform(HumanBodyBones.Spine);
            bindingChest = ResolveChestBone(animator);
            bindingNeck = animator.GetBoneTransform(HumanBodyBones.Neck);
            bindingHead = animator.GetBoneTransform(HumanBodyBones.Head);
            bindingLeftEye = animator.GetBoneTransform(HumanBodyBones.LeftEye);
            bindingRightEye = animator.GetBoneTransform(HumanBodyBones.RightEye);
            bindingLeftUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            bindingLeftForearm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            bindingLeftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            bindingRightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            bindingRightForearm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            bindingRightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            bindingLeftThigh = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            bindingLeftCalf = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            bindingLeftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            bindingRightThigh = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            bindingRightCalf = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            bindingRightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            return true;
        }

        /// <summary>验证 Driver 的统一骨骼绑定与指定 Humanoid Animator 一致。</summary>
        public bool MatchesHumanoidBinding(Animator animator)
        {
            if (!IsValidHumanoid(animator) || !useDriverBoneBinding || bindingRoot != animator.transform)
                return false;

            return Matches(bindingPelvis, animator.GetBoneTransform(HumanBodyBones.Hips))
                   && Matches(bindingSpine, animator.GetBoneTransform(HumanBodyBones.Spine))
                   && Matches(bindingChest, ResolveChestBone(animator))
                   && Matches(bindingHead, animator.GetBoneTransform(HumanBodyBones.Head))
                   && Matches(bindingLeftUpperArm, animator.GetBoneTransform(HumanBodyBones.LeftUpperArm))
                   && Matches(bindingLeftForearm, animator.GetBoneTransform(HumanBodyBones.LeftLowerArm))
                   && Matches(bindingLeftHand, animator.GetBoneTransform(HumanBodyBones.LeftHand))
                   && Matches(bindingRightUpperArm, animator.GetBoneTransform(HumanBodyBones.RightUpperArm))
                   && Matches(bindingRightForearm, animator.GetBoneTransform(HumanBodyBones.RightLowerArm))
                   && Matches(bindingRightHand, animator.GetBoneTransform(HumanBodyBones.RightHand))
                   && Matches(bindingLeftThigh, animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg))
                   && Matches(bindingLeftCalf, animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg))
                   && Matches(bindingLeftFoot, animator.GetBoneTransform(HumanBodyBones.LeftFoot))
                   && Matches(bindingRightThigh, animator.GetBoneTransform(HumanBodyBones.RightUpperLeg))
                   && Matches(bindingRightCalf, animator.GetBoneTransform(HumanBodyBones.RightLowerLeg))
                   && Matches(bindingRightFoot, animator.GetBoneTransform(HumanBodyBones.RightFoot));
        }

        private static bool IsValidHumanoid(Animator animator)
        {
            return animator != null && animator.isHuman && animator.avatar != null && animator.avatar.isValid;
        }

        private static bool Matches(Transform actual, Transform expected)
        {
            return expected != null && actual == expected;
        }

        private static Transform ResolveChestBone(Animator animator)
        {
            Transform upperChest = animator.GetBoneTransform(HumanBodyBones.UpperChest);
            if (upperChest != null)
                return upperChest;

            Transform chest = animator.GetBoneTransform(HumanBodyBones.Chest);
            return chest != null ? chest : animator.GetBoneTransform(HumanBodyBones.Spine);
        }
    }
}
