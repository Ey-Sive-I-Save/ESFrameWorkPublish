using UnityEngine;
using Sirenix.OdinInspector;

namespace ES
{
    public sealed partial class StateFinalIKDriver
    {
#if UNITY_EDITOR
        [TabGroup("DriverLayout", "测试工具", Order = 200)]
        [PropertyOrder(0)]
        [BoxGroup("DriverLayout/测试工具/IK调试可视化", ShowLabel = false)]
        [TitleGroup("DriverLayout/测试工具/IK调试可视化/Scene 可视化", BoldTitle = true)]
        [LabelText("启用 Scene/Game 可视化")]
        [SerializeField] private bool debugDrawIKGizmosInSceneAndGame;

        [TabGroup("DriverLayout", "测试工具")]
        [PropertyOrder(1)]
        [BoxGroup("DriverLayout/测试工具/IK调试可视化", ShowLabel = false)]
        [TitleGroup("DriverLayout/测试工具/IK调试可视化/Scene 可视化")]
        [LabelText("目标连线")]
        [SerializeField] private bool debugDrawIKTargetLines = true;

        [TabGroup("DriverLayout", "测试工具")]
        [PropertyOrder(2)]
        [BoxGroup("DriverLayout/测试工具/IK调试可视化", ShowLabel = false)]
        [TitleGroup("DriverLayout/测试工具/IK调试可视化/Scene 可视化")]
        [LabelText("Hint 点")]
        [SerializeField] private bool debugDrawIKHints = true;

        [TabGroup("DriverLayout", "测试工具")]
        [PropertyOrder(3)]
        [BoxGroup("DriverLayout/测试工具/IK调试可视化", ShowLabel = false)]
        [TitleGroup("DriverLayout/测试工具/IK调试可视化/Scene 可视化")]
        [LabelText("权重条")]
        [SerializeField] private bool debugDrawIKWeightBars = true;

        [TabGroup("DriverLayout", "测试工具")]
        [PropertyOrder(4)]
        [BoxGroup("DriverLayout/测试工具/IK调试可视化", ShowLabel = false)]
        [TitleGroup("DriverLayout/测试工具/IK调试可视化/Scene 可视化")]
        [LabelText("显示尺寸")]
        [SerializeField, Range(0.01f, 0.25f)] private float debugIKGizmoSize = 0.045f;

        private void OnDrawGizmos()
        {
            if (!debugDrawIKGizmosInSceneAndGame)
                return;

            StateGeneralFinalIKDriverPose pose = _stateMachine != null
                ? _stateMachine.stateGeneralFinalIKDriverPose
                : _lastPose;

            if (!pose.HasAnyWeight)
                return;

            DrawIKGoalGizmos("LH", in pose.leftHand, ResolveGoalBone(HumanBodyBones.LeftHand, bindingLeftHand, _bipedIKReady ? _bipedIK.references.leftHand : null), Color.green);
            DrawIKGoalGizmos("RH", in pose.rightHand, ResolveGoalBone(HumanBodyBones.RightHand, bindingRightHand, _bipedIKReady ? _bipedIK.references.rightHand : null), new Color(0.2f, 0.9f, 1f));
            DrawIKGoalGizmos("LF", in pose.leftFoot, ResolveGoalBone(HumanBodyBones.LeftFoot, bindingLeftFoot, _bipedIKReady ? _bipedIK.references.leftFoot : null), new Color(1f, 0.8f, 0.25f));
            DrawIKGoalGizmos("RF", in pose.rightFoot, ResolveGoalBone(HumanBodyBones.RightFoot, bindingRightFoot, _bipedIKReady ? _bipedIK.references.rightFoot : null), new Color(1f, 0.45f, 0.25f));

            if (pose.lookAtWeight > 0.001f)
            {
                Vector3 origin = transform.position + Vector3.up * 1.45f;
                Gizmos.color = new Color(0.4f, 0.65f, 1f, 0.9f);
                Gizmos.DrawSphere(pose.lookAtPosition, debugIKGizmoSize * 0.85f);
                Gizmos.DrawLine(origin, pose.lookAtPosition);
                if (debugDrawIKWeightBars)
                    DrawWeightBar(pose.lookAtPosition + Vector3.up * debugIKGizmoSize * 2.2f, pose.lookAtWeight, Gizmos.color);
            }
        }

        private Transform ResolveGoalBone(HumanBodyBones humanBone, Transform binding, Transform bipedReference)
        {
            if (binding != null)
                return binding;
            if (bipedReference != null)
                return bipedReference;
            return _animator != null && _animator.isHuman ? _animator.GetBoneTransform(humanBone) : null;
        }

        private void DrawIKGoalGizmos(string label, in IKGoalPose goal, Transform bone, Color color)
        {
            if (!goal.HasAnyWeight)
                return;

            float influence = Mathf.Clamp01(Mathf.Max(goal.weight, goal.rotationWeight));
            Color targetColor = new Color(color.r, color.g, color.b, Mathf.Lerp(0.35f, 1f, influence));
            float size = debugIKGizmoSize * Mathf.Lerp(0.8f, 1.6f, influence);

            Gizmos.color = targetColor;
            Gizmos.DrawSphere(goal.position, size);
            Gizmos.DrawWireSphere(goal.position, size * 1.55f);

            if (bone != null)
            {
                Gizmos.color = new Color(1f, 1f, 1f, 0.85f);
                Gizmos.DrawWireSphere(bone.position, debugIKGizmoSize * 0.9f);

                if (debugDrawIKTargetLines)
                {
                    Gizmos.color = targetColor;
                    Gizmos.DrawLine(bone.position, goal.position);
                }
            }

            if (debugDrawIKHints && goal.hintPosition != Vector3.zero)
            {
                Gizmos.color = new Color(0.85f, 0.85f, 1f, 0.8f);
                Gizmos.DrawSphere(goal.hintPosition, debugIKGizmoSize * 0.65f);
                if (bone != null)
                    Gizmos.DrawLine(goal.hintPosition, bone.position);
            }

            if (debugDrawIKWeightBars)
            {
                DrawWeightBar(goal.position + Vector3.up * debugIKGizmoSize * 2.2f, influence, targetColor);
            }

            DrawLabel(goal.position + Vector3.up * debugIKGizmoSize * 3.4f, $"{label} {influence:F2}");
        }

        private void DrawWeightBar(Vector3 origin, float weight, Color color)
        {
            float clamped = Mathf.Clamp01(weight);
            Vector3 bottom = origin;
            Vector3 top = origin + Vector3.up * (debugIKGizmoSize * 4f);
            Vector3 fillTop = Vector3.Lerp(bottom, top, clamped);

            Gizmos.color = new Color(0.05f, 0.05f, 0.05f, 0.85f);
            Gizmos.DrawLine(bottom, top);
            Gizmos.color = color;
            Gizmos.DrawCube(fillTop, Vector3.one * debugIKGizmoSize * 0.55f);
            Gizmos.DrawLine(bottom, fillTop);
        }

        private static void DrawLabel(Vector3 worldPos, string text)
        {
            UnityEditor.Handles.Label(worldPos, text, UnityEditor.EditorStyles.whiteMiniLabel);
        }
#endif
    }
}
