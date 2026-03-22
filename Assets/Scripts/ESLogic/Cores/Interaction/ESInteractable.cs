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

        [LabelText("IK 目标权重")]
        [Range(0f, 1f)]
        public float ikTargetWeight = 1f;

        [LabelText("IK LerpingRate"), Tooltip("控制本次交互写入 Driver 的 lerping 速度倍率。它不是权重。1=默认，小于1更慢，大于1更快。")] 
        [Range(0.05f, 8f)]
        public float ikLerpingRate = 1f;

        [LabelText("Use IK Rotation")]
        public bool useIKRotation = true;

        /// <summary>
        /// 评估“目标权重”。
        /// 默认直接使用 Inspector 的 ikTargetWeight。
        /// 你可以在派生类里重写，实现：随进度/距离/曲线动态变化。
        /// </summary>
        public virtual float EvaluateIKTargetWeight(Entity entity, float normalized01)
        {
            return ikTargetWeight;
        }

        /// <summary>
        /// 评估“lerping 速度倍率”。
        /// 默认直接使用 Inspector 的 ikLerpingRate。
        /// 你可以在派生类里重写，实现：随进度/距离/曲线动态变化。
        /// </summary>
        public virtual float EvaluateIKLerpingRate(Entity entity, float normalized01)
        {
            return ikLerpingRate;
        }

        [Title("MatchTarget (Optional)")]
        public bool enableMatchTarget = false;

        [InfoBox("仅配置请求参数与偏移；目标位置/旋转由交互运行时传入（通常使用当前 Interactable 的 Transform）。", InfoMessageType.None, "enableMatchTarget")]
        [ShowIf("enableMatchTarget"), HideLabel]
        public MatchTargetRequest matchTargetRequest = MatchTargetRequest.Default;

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
