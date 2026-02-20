using System;
using UnityEngine;
using RootMotion.FinalIK;

namespace ES
{
    /// <summary>
    /// Final IK(BipedIK) 的高性能桥接层：
    /// - 直接引用 RootMotion.FinalIK（无反射）
    /// - 手动驱动 UpdateBipedIK，确保执行顺序稳定
    /// </summary>
    internal sealed class FinalIKBipedIKBridge
    {
        private BipedIK _bipedIK;

        private string _lastBindError;

        public bool AutoDetectReferencesIfMissing { get; set; } = true;

        /// <summary>
        /// 0: 脚只驱动位置不驱动旋转（最稳，商业常用兜底）
        /// 1: 正常驱动脚旋转
        /// </summary>
        public float FootRotationWeightMultiplier { get; set; } = 1f;

        public bool IsBound => _bipedIK != null;

        public bool IsReady => _bipedIK != null && string.IsNullOrEmpty(_lastBindError);

        public string LastBindError => _lastBindError;

        public bool TryBind(Animator animator)
        {
            if (animator == null) return false;

            // 已绑定：仍允许外部反复调用以刷新错误状态
            if (_bipedIK == null)
            {
                _bipedIK = animator.GetComponent<BipedIK>();
            }

            if (_bipedIK == null)
            {
                _lastBindError = "Animator同物体上未找到 RootMotion.FinalIK.BipedIK 组件";
                return false;
            }

            // 尝试自动识别骨骼引用（否则 Initiate 会直接 SetupError 并不工作）
            if (AutoDetectReferencesIfMissing && (_bipedIK.references == null || !_bipedIK.references.isFilled))
            {
                RootMotion.BipedReferences.AutoDetectReferences(
                    ref _bipedIK.references,
                    _bipedIK.transform,
                    new RootMotion.BipedReferences.AutoDetectParams(legsParentInSpine: false, includeEyes: true));
            }

            if (_bipedIK.references == null || !_bipedIK.references.isFilled)
            {
                _lastBindError = AutoDetectReferencesIfMissing
                    ? "BipedIK.references 未配置且自动识别失败（通常是非Humanoid或骨骼结构异常）"
                    : "BipedIK.references 未配置（已关闭自动识别）";
                return false;
            }

            string setupMessage = string.Empty;
            if (RootMotion.BipedReferences.SetupError(_bipedIK.references, ref setupMessage))
            {
                _lastBindError = setupMessage;
                return false;
            }

            _lastBindError = string.Empty;

            // 我们手动驱动执行顺序（LateUpdate里调用 UpdateBipedIK），避免FinalIK自身LateUpdate与业务脚本顺序不确定
            _bipedIK.enabled = false;
            _bipedIK.InitiateBipedIK();
            return true;
        }

        public void Apply(
            Animator animator,
            StateIKPose pose,
            Transform leftHandHint,
            Transform rightHandHint,
            Transform leftFootHint,
            Transform rightFootHint,
            Transform leftHandTarget = null,
            Transform rightHandTarget = null,
            Transform leftFootTarget = null,
            Transform rightFootTarget = null)
        {
            if (animator == null) return;

            if (_bipedIK == null)
            {
                if (!TryBind(animator)) return;
            }

            if (_bipedIK == null) return;

            if (!pose.HasAnyWeight)
            {
                _bipedIK.SetToDefaults();
                return;
            }

            ApplyGoal(pose.leftHand, AvatarIKGoal.LeftHand, leftHandHint, leftHandTarget);
            ApplyGoal(pose.rightHand, AvatarIKGoal.RightHand, rightHandHint, rightHandTarget);
            ApplyGoal(pose.leftFoot, AvatarIKGoal.LeftFoot, leftFootHint, leftFootTarget);
            ApplyGoal(pose.rightFoot, AvatarIKGoal.RightFoot, rightFootHint, rightFootTarget);

            if (pose.lookAtWeight > 0.001f)
            {
                _bipedIK.SetLookAtPosition(pose.lookAtPosition);
                _bipedIK.SetLookAtWeight(
                    pose.lookAtWeight,
                    pose.lookAtBodyWeight,
                    pose.lookAtHeadWeight,
                    pose.lookAtEyesWeight,
                    pose.lookAtClampWeight,
                    pose.lookAtClampWeight,
                    pose.lookAtClampWeight);
            }
            else
            {
                _bipedIK.SetLookAtWeight(0f, 0.5f, 1f, 1f, 0.5f, 0.7f, 0.5f);
            }
        }

        public void UpdateSolver()
        {
            if (_bipedIK == null) return;
            _bipedIK.UpdateBipedIK();
        }

        public void ResetToDefaults()
        {
            if (_bipedIK == null) return;
            _bipedIK.SetToDefaults();
        }

        private void ApplyGoal(IKGoalPose goal, AvatarIKGoal avatarGoal, Transform hint, Transform target)
        {
            if (_bipedIK == null) return;

            float w = Mathf.Clamp01(goal.weight);
            _bipedIK.SetIKPositionWeight(avatarGoal, w);

            bool isFoot = avatarGoal == AvatarIKGoal.LeftFoot || avatarGoal == AvatarIKGoal.RightFoot;
            float rotW = w;
            if (isFoot)
            {
                rotW = w * Mathf.Clamp01(FootRotationWeightMultiplier);
            }
            _bipedIK.SetIKRotationWeight(avatarGoal, rotW);

            // 直接把目标Transform挂到BipedIK的solver上：
            // - 方便在BipedIK Inspector里看到目标不再是空
            // - 也便于运行时手动拖动目标点进行调试
            var limbForTarget = _bipedIK.GetGoalIK(avatarGoal);
            if (limbForTarget != null)
            {
                // IKSolverLimb.target 为 FinalIK 的标准目标入口
                if (limbForTarget.target != target)
                {
                    limbForTarget.target = target;
                }
            }

            if (w > 0.001f)
            {
                // 当存在 target Transform 时，让 FinalIK 直接从 solver.target 读取位置/旋转，
                // 避免同时 SetIKPosition/Rotation 与 target 两套输入互相“打架”造成扭曲/抖动。
                if (target == null)
                {
                    _bipedIK.SetIKPosition(avatarGoal, goal.position);
                    _bipedIK.SetIKRotation(avatarGoal, goal.rotation);
                }

                // Hint -> bend goal
                if (hint != null && goal.hintPosition != Vector3.zero)
                {
                    // 避免每帧重复写同值（Transform 写入在 VR 下也要尽量克制）
                    if ((hint.position - goal.hintPosition).sqrMagnitude > 0.000001f)
                    {
                        hint.position = goal.hintPosition;
                    }
                    var limb = _bipedIK.GetGoalIK(avatarGoal);
                    if (limb != null)
                    {
                        limb.bendGoal = hint;
                        limb.bendModifier = IKSolverLimb.BendModifier.Goal;
                        limb.bendModifierWeight = w;
                    }
                }
                else
                {
                    var limb = _bipedIK.GetGoalIK(avatarGoal);
                    if (limb != null)
                    {
                        limb.bendModifier = IKSolverLimb.BendModifier.Animation;
                        limb.bendModifierWeight = 1f;
                        limb.bendGoal = null;
                    }
                }
            }
        }
    }
}
