using Sirenix.OdinInspector;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{
    public enum ESInteractionKind : byte
    {
        Use = 0,
        Pickup = 1,
        OpenClose = 2,
        Mount = 3,
        Talk = 4,
        Inspect = 5,
        Custom = 255,
    }

    public enum ESInteractionCheckResult : byte
    {
        Allowed = 0,
        TargetDisabled = 1,
        Cooldown = 2,
        Occupied = 3,
        Locked = 4,
        EntityPermitDenied = 5,
        TooFar = 6,
        NotFacing = 7,
        RequiresGrounded = 8,
        StateUnavailable = 9,
        TargetUnavailable = 10,
        EntityTagDenied = 11,
        CustomDenied = 255,
    }

    public enum ESInteractionEndReason : byte
    {
        Completed = 0,
        UserCancelled = 1,
        MovementCancelled = 2,
        Timeout = 3,
        TargetLost = 4,
        StateExited = 5,
        ModuleDisabled = 6,
        BeginRejected = 7,
    }

    [DisallowMultipleComponent]
    public class ESInteractable : MonoBehaviour
    {
        public enum IKWriteBuildResult
        {
            Success = 0,
            Disabled = 1,
            MissingTarget = 2,
        }

        public struct IKWriteRequest
        {
            public IKGoal goal;
            public Transform target;
            public Transform hintTarget;
            public float weight;
            public float lerpingRate;
            public bool useTargetRotation;
        }

        [Title("交互基础")]
        public bool isInteractable = true;

        [LabelText("交互类型")]
        public ESInteractionKind interactionKind = ESInteractionKind.Use;

        [LabelText("显示名称")]
        public string displayName;

        [LabelText("交互提示")]
        public string interactionPrompt = "交互";

        [LabelText("交互优先级"), Tooltip("数值越高，多个目标重叠时越优先。")]
        public int interactionPriority;

        [LabelText("交互点"), Tooltip("为空时使用命中 Collider 的最近点。IK 与 MatchTarget 仍使用各自配置。")]
        public Transform interactionPoint;

        [LabelText("最大交互距离"), Tooltip("<=0 时使用 Entity 交互模块的探测距离。")]
        public float maxInteractionDistance;

        [LabelText("Duration"), Tooltip("<=0 means no auto-complete")]
        public float interactDuration = 1f;

        [LabelText("Timeout"), Tooltip("<=0 means no timeout")]
        public float interactTimeout = 3f;

        [LabelText("Cooldown")]
        public float interactCooldown = 0.2f;

        [Title("Tag Gate")]
        [LabelText("交互者 Tag 条件")]
        [Tooltip("为空时不限制。条件只判断发起交互的 Entity，不改变交互目标自身的业务状态。")]
        public ESTagConditionConfig actorTagCondition = new ESTagConditionConfig();

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

        /// <summary>
        /// 组装本帧 IK 写入请求。交互模块只需消费该请求即可。
        /// </summary>
        public virtual IKWriteBuildResult TryBuildIKWriteRequest(Entity entity, float normalized01, out IKWriteRequest request)
        {
            request = default;

            if (!enableIK)
                return IKWriteBuildResult.Disabled;

            if (ikTarget == null)
                return IKWriteBuildResult.MissingTarget;

            request.goal = ikGoal;
            request.target = ikTarget;
            request.hintTarget = ikHintTarget;
            request.weight = Mathf.Clamp01(EvaluateIKTargetWeight(entity, normalized01));
            request.lerpingRate = Mathf.Clamp(EvaluateIKLerpingRate(entity, normalized01), 0.05f, 8f);
            request.useTargetRotation = useIKRotation;
            return IKWriteBuildResult.Success;
        }

        [Title("MatchTarget (Optional)")]
        public bool enableMatchTarget = false;

        [InfoBox("仅配置请求参数与偏移；目标位置/旋转由交互运行时传入（通常使用当前 Interactable 的 Transform）。", InfoMessageType.None, "enableMatchTarget")]
        [ShowIf("enableMatchTarget"), HideLabel]
        public MatchTargetRequest matchTargetRequest = MatchTargetRequest.Default;

        private float _lastInteractTime = -999f;

        [ShowInInspector, ReadOnly, LabelText("当前占用者")]
        private Entity _interactionOwner;

        public Entity InteractionOwner => _interactionOwner;
        public bool IsOccupied => _interactionOwner != null;

        public Vector3 ResolveInteractionPoint(Vector3 origin, Collider sourceCollider)
        {
            if (interactionPoint != null)
                return interactionPoint.position;

            if (sourceCollider != null)
                return sourceCollider.ClosestPoint(origin);

            return transform.position;
        }

        public virtual bool CanInteract(Entity entity)
        {
            return CanInteract(entity, out _);
        }

        public virtual bool CanInteract(Entity entity, out ESInteractionCheckResult result)
        {
            if (!isActiveAndEnabled || !isInteractable)
            {
                result = ESInteractionCheckResult.TargetDisabled;
                return false;
            }

            if (actorTagCondition != null
                && !actorTagCondition.IsEmpty
                && (entity == null || !entity.Tags.Matches(actorTagCondition)))
            {
                result = ESInteractionCheckResult.EntityTagDenied;
                return false;
            }

            if (_interactionOwner != null && _interactionOwner != entity)
            {
                result = ESInteractionCheckResult.Occupied;
                return false;
            }

            if (Time.time - _lastInteractTime < Mathf.Max(0f, interactCooldown))
            {
                result = ESInteractionCheckResult.Cooldown;
                return false;
            }

            result = ESInteractionCheckResult.Allowed;
            return true;
        }

        public bool TryAcquireInteraction(Entity entity, out ESInteractionCheckResult result)
        {
            if (entity == null)
            {
                result = ESInteractionCheckResult.TargetUnavailable;
                return false;
            }

            if (!CanInteract(entity, out result))
                return false;

            _interactionOwner = entity;
            return true;
        }

        public void ReleaseInteraction(Entity entity)
        {
            if (_interactionOwner == entity)
                _interactionOwner = null;
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

        public virtual void OnInteractEnded(Entity entity, bool success, ESInteractionEndReason reason)
        {
            OnInteractCompleted(entity, success);
        }

        protected virtual void OnDisable()
        {
            _interactionOwner = null;
        }
    }

    /// <summary>
    /// Trigger-zone Tag writer. Each entering Entity owns one LeaseSet regardless of how many of
    /// its child colliders overlap the zone. It deliberately does not configure Physics layers;
    /// layer policy stays in GameCore's Physics Layer settings.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class ESTagGrantZone : MonoBehaviour
    {
        private sealed class Occupant
        {
            public int colliderCount;
            public readonly ESTagLeaseSet leases = new ESTagLeaseSet();
        }

        [LabelText("区域内授予")]
        public ESTagGrantConfig tagGrants = new ESTagGrantConfig();

        [LabelText("输出配置告警")]
        public bool logGrantFailures = true;

        private readonly Dictionary<Entity, Occupant> occupants = new Dictionary<Entity, Occupant>();
        private readonly List<KeyValuePair<Entity, Occupant>> cleanupBuffer = new List<KeyValuePair<Entity, Occupant>>();

#if UNITY_EDITOR
        private void OnValidate()
        {
            Collider zoneCollider = GetComponent<Collider>();
            if (zoneCollider != null && !zoneCollider.isTrigger)
            {
                Debug.LogWarning("[ESTagGrantZone] 区域授予依赖 Trigger Collider；请按 GameCore Layer 规则使用 TriggerZone 层。", this);
            }
        }
#endif

        private void OnTriggerEnter(Collider other)
        {
            Entity entity = other != null ? other.GetComponentInParent<Entity>() : null;
            if (entity == null || tagGrants == null || tagGrants.IsEmpty)
                return;

            if (occupants.TryGetValue(entity, out Occupant occupant))
            {
                occupant.colliderCount++;
                return;
            }

            occupant = new Occupant();
            if (!occupant.leases.TryAcquire(entity.Tags, tagGrants, this, out string error))
            {
                if (logGrantFailures)
                    Debug.LogWarning("[ESTagGrantZone] Tag 授予失败: " + error, this);
                occupant.leases.Dispose();
                return;
            }

            occupant.colliderCount = 1;
            occupants.Add(entity, occupant);
        }

        private void OnTriggerExit(Collider other)
        {
            Entity entity = other != null ? other.GetComponentInParent<Entity>() : null;
            if (entity == null || !occupants.TryGetValue(entity, out Occupant occupant))
                return;

            occupant.colliderCount--;
            if (occupant.colliderCount > 0)
                return;

            occupant.leases.Dispose();
            occupants.Remove(entity);
        }

        private void LateUpdate()
        {
            if (occupants.Count == 0)
                return;

            cleanupBuffer.Clear();
            foreach (KeyValuePair<Entity, Occupant> pair in occupants)
            {
                if (pair.Key == null)
                    cleanupBuffer.Add(pair);
            }

            for (int i = 0; i < cleanupBuffer.Count; i++)
            {
                KeyValuePair<Entity, Occupant> pair = cleanupBuffer[i];
                pair.Value.leases.Dispose();
                occupants.Remove(pair.Key);
            }
        }

        private void OnDisable()
        {
            foreach (Occupant occupant in occupants.Values)
                occupant.leases.Dispose();
            occupants.Clear();
        }
    }
}
