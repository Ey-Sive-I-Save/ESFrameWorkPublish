using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    public class ESInteractable : MonoBehaviour
    {
        [Title("Interactable")]
        public bool isInteractable = true;

        [LabelText("Duration"), Tooltip("<=0 means no auto-complete")]
        public float interactDuration = 1f;

        [LabelText("Timeout"), Tooltip("<=0 means no timeout")]
        public float interactTimeout = 3f;

        [LabelText("Cooldown")]
        public float interactCooldown = 0.2f;

        [Title("State Injection")]
        public StateAniDataInfo interactionStateInfo;

        [LabelText("State Key Override")]
        public string stateKeyOverride = "";

        [LabelText("Allow State Injection")]
        public bool allowStateInjection = true;

        [Title("IK")]
        public bool enableIK = true;

        public IKGoal ikGoal = IKGoal.RightHand;

        public Transform ikTarget;

        public Transform ikHintTarget;

        [Range(0f, 1f)]
        public float ikWeight = 1f;

        [LabelText("IK TargetWeight"), Tooltip("IK总目标权重(targetWeight)：控制最终 IK 强度的总开关（0=完全关闭，1=完全开启）。\n交互模块会在交互期间每帧读取它，并写入到交互状态的 IK targetWeight（支持运行时动态变化）。")]
        [Range(0f, 1f)]
        public float ikTargetWeight = 1f;

        [LabelText("Use IK Rotation")]
        public bool useIKRotation = true;

        /// <summary>
        /// 评估“单肢体权重”（left/right hand/foot 的 limb weight）。
        /// 默认直接使用 Inspector 的 ikWeight。
        /// 你可以在派生类里重写，实现：随进度/距离/曲线动态变化。
        /// </summary>
        public virtual float EvaluateIKLimbWeight(Entity entity, float normalized01)
        {
            return ikWeight;
        }

        /// <summary>
        /// 评估“总目标权重”（IK targetWeight，决定 ik.weight 的平滑目标）。
        /// 默认直接使用 Inspector 的 ikTargetWeight。
        /// 你可以在派生类里重写，实现：随进度/距离/曲线动态变化。
        /// </summary>
        public virtual float EvaluateIKTargetWeight(Entity entity, float normalized01)
        {
            return ikTargetWeight;
        }

        [Title("MatchTarget (Optional)")]
        public bool enableMatchTarget = false;

        public Transform matchTarget;

        public AvatarTarget matchTargetBodyPart = AvatarTarget.RightHand;

        [Range(0f, 1f)]
        public float matchTargetStartTime = 0.1f;

        [Range(0f, 1f)]
        public float matchTargetEndTime = 0.9f;

        public Vector3 matchTargetPosWeight = new Vector3(1f, 1f, 1f);

        [Range(0f, 1f)]
        public float matchTargetRotWeight = 1f;

        private float _lastInteractTime = -999f;

        public virtual bool CanInteract(Entity entity)
        {
            if (!isInteractable) return false;
            return Time.time - _lastInteractTime >= interactCooldown;
        }

        public virtual void OnInteractStarted(Entity entity)
        {
            _lastInteractTime = Time.time;
        }

        public virtual void OnInteractUpdate(Entity entity, float deltaTime)
        {
        }

        public virtual void OnInteractCompleted(Entity entity, bool success)
        {
        }
    }
}
