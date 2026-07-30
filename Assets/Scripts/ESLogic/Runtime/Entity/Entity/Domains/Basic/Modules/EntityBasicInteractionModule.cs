using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [Serializable, TypeRegistryItem("基础交互模块")]
    public class EntityBasicInteractionModule : EntityBasicModuleBase
    {
        [Title("Interaction")]
        public bool enableInteraction = true;

        [LabelText("Auto Detect")]
        public bool autoDetect = true;

        [LabelText("Detect Radius")]
        public float detectRadius = 1.5f;

        [LabelText("Max Detect Count")]
        public int detectMaxCount = 8;

        [LabelText("探测间隔"), Tooltip("0=每帧。默认 0.05 秒可降低场景交互扫描成本，按键时仍会立即刷新。")]
        [MinValue(0f)]
        public float detectInterval = 0.05f;

        [LabelText("Interactable Layers")]
        public LayerMask interactableLayers = ~0;

        [LabelText("Require Facing")]
        public bool requireFacing = true;

        [LabelText("Max Facing Angle"), Range(0f, 180f)]
        public float maxFacingAngle = 75f;

        [LabelText("候选黏性"), Tooltip("当前候选获得少量评分优势，避免两个物体边界处反复跳变。")]
        [MinValue(0f)]
        public float candidateStickiness = 0.15f;

        [LabelText("Require Grounded")]
        public bool requireGrounded = true;

        [Title("行为许可")]
        [LabelText("检查 Buff 许可")]
        public bool requireEntityPermit = true;

        [LabelText("许可键")]
        public string entityPermitKey = "Entity.Interaction";

        [Title("State")]
        public bool overrideSupportFlag = false;

        public StateSupportFlags interactionSupportFlag = StateSupportFlags.SpecialInteraction;

        [Title("Timeout")]
        public float defaultInteractTimeout = 3f;

        [Title("Cancel")]
        public bool cancelOnMoveInput = true;

        public float cancelMoveThreshold = 0.2f;

        [ShowInInspector, ReadOnly]
        public ESInteractable currentCandidate;

        [ShowInInspector, ReadOnly]
        public ESInteractable activeInteractable;

        [ShowInInspector, ReadOnly]
        public bool isInteracting;

        [ShowInInspector, ReadOnly, LabelText("最近检查结果")]
        public ESInteractionCheckResult lastCheckResult = ESInteractionCheckResult.Allowed;

        [ShowInInspector, ReadOnly, LabelText("最近结束原因")]
        public ESInteractionEndReason lastEndReason = ESInteractionEndReason.Completed;

        [Title("IK Debug")]
        [ShowInInspector, ReadOnly]
        public string ikLastStatus = "Idle";

        [ShowInInspector, ReadOnly]
        public float ikLastNormalized01;

        [ShowInInspector, ReadOnly]
        public float ikLastEvaluatedWeight;

        [ShowInInspector, ReadOnly]
        public float ikLastEvaluatedLerpingRate;

        [ShowInInspector, ReadOnly]
        public Transform ikLastTarget;

        [ShowInInspector, ReadOnly]
        public Transform ikLastHintTarget;

        [ShowInInspector, ReadOnly]
        public float ikLastTargetMoveDistance;

        [ShowInInspector, ReadOnly]
        public float ikLastWriteTime;

        private StateBase _activeState;
        private StateMachine _sm;
        private StateSupportFlags _prevSupportFlag = StateSupportFlags.None;
        private bool _hasOverriddenSupportFlag;
        private float _interactionStartTime = -999f;
        private Collider[] _overlapBuffer;
        [NonSerialized] private Dictionary<int, ESInteractable> _interactableByColliderId;
        private float _nextDetectTime;
        private bool _ikHasPrevTargetPos;
        private Vector3 _ikPrevTargetPos;
        private StateLifecycleTracker _interactionLifecycle = new StateLifecycleTracker();

        private bool EnsureStateMachineReady()
        {
            if (MyCore == null) return false;

            if (_sm == null)
            {
                var domain = MyCore.stateDomain;
                if (domain != null)
                {
                    _sm = domain.stateMachine;
                }
            }

            if (_sm == null) return false;

            return _sm.BoundAnimator != null && _sm.isRunning;
        }

        public override void Start()
        {
            base.Start();
            _sm = MyCore?.stateDomain?.stateMachine;
            EnsureOverlapBuffer();
            EnsureInteractableCache();
        }

        protected override void Update()
        {
            if (!enableInteraction || MyCore == null) return;

            if (isInteracting)
            {
                UpdateInteraction(Time.deltaTime);
                return;
            }

            if (autoDetect)
            {
                float now = Time.unscaledTime;
                if (detectInterval <= 0f || now >= _nextDetectTime)
                {
                    _nextDetectTime = now + Mathf.Max(0f, detectInterval);
                    currentCandidate = FindBestInteractable();
                }
            }
        }

        public void RequestInteract()
        {
            if (!enableInteraction || MyCore == null) return;

            if (isInteracting)
            {
                EndInteraction(false, ESInteractionEndReason.UserCancelled);
                return;
            }

            if (!CanEntityInteract(out lastCheckResult))
                return;

            currentCandidate = FindBestInteractable();

            if (currentCandidate != null)
            {
                BeginInteraction(currentCandidate);
            }
        }

        private ESInteractable FindBestInteractable()
        {
            if (!CanEntityInteract(out lastCheckResult))
                return null;

            EnsureOverlapBuffer();
            var motor = MyCore.kcc?.motor;
            Vector3 origin = motor != null ? motor.TransientPosition : MyCore.transform.position;

            ESPhysicsQueryModule physicsQuery = ESGameManager.PhysicsQueryModule;
            int count = physicsQuery != null
                ? physicsQuery.OverlapSphere(origin, detectRadius, interactableLayers, _overlapBuffer, QueryTriggerInteraction.Collide)
                : Physics.OverlapSphereNonAlloc(origin, detectRadius, _overlapBuffer, interactableLayers, QueryTriggerInteraction.Collide);
            ESInteractable best = null;
            int bestPriority = int.MinValue;
            float bestScore = float.MaxValue;
            Vector3 forward = MyCore.transform.forward;

            int safeCount = Mathf.Min(count, _overlapBuffer.Length);
            for (int i = 0; i < safeCount; i++)
            {
                var col = _overlapBuffer[i];
                if (col == null) continue;
                ESInteractable interactable = ResolveInteractable(col);
                if (interactable == null) continue;
                if (!interactable.CanInteract(MyCore, out _)) continue;

                Vector3 targetPos = interactable.ResolveInteractionPoint(origin, col);
                float dist = Vector3.Distance(targetPos, origin);
                float allowedDistance = interactable.maxInteractionDistance > 0f
                    ? Mathf.Min(detectRadius, interactable.maxInteractionDistance)
                    : detectRadius;
                if (dist > allowedDistance) continue;
                if (!IsFacingTarget(forward, origin, targetPos)) continue;

                int priority = interactable.interactionPriority;
                float score = dist;
                if (interactable == currentCandidate)
                    score -= candidateStickiness;

                if (priority < bestPriority || (priority == bestPriority && score >= bestScore))
                    continue;

                bestPriority = priority;
                bestScore = score;
                best = interactable;
            }

            lastCheckResult = best != null ? ESInteractionCheckResult.Allowed : ESInteractionCheckResult.TargetUnavailable;
            return best;
        }

        private ESInteractable ResolveInteractable(Collider collider)
        {
            EnsureInteractableCache();
            int id = collider.GetInstanceID();
            if (_interactableByColliderId.TryGetValue(id, out ESInteractable cached))
            {
                if (cached != null)
                    return cached;

                _interactableByColliderId.Remove(id);
            }

            ESInteractable resolved = collider.GetComponentInParent<ESInteractable>();
            _interactableByColliderId[id] = resolved;
            return resolved;
        }

        private void EnsureInteractableCache()
        {
            if (_interactableByColliderId == null)
                _interactableByColliderId = new Dictionary<int, ESInteractable>(32);
        }

        private void EnsureOverlapBuffer()
        {
            int capacity = Mathf.Max(4, detectMaxCount);
            if (_overlapBuffer == null || _overlapBuffer.Length != capacity)
                _overlapBuffer = new Collider[capacity];
        }

        private bool CanEntityInteract(out ESInteractionCheckResult result)
        {
            if (MyCore == null)
            {
                result = ESInteractionCheckResult.TargetUnavailable;
                return false;
            }

            if (requireEntityPermit
                && MyCore.TryGetPermit(entityPermitKey, out ESPermitSet permit)
                && !permit.Value)
            {
                result = ESInteractionCheckResult.EntityPermitDenied;
                return false;
            }

            result = ESInteractionCheckResult.Allowed;
            return true;
        }

        private bool IsFacingTarget(Vector3 forward, Vector3 origin, Vector3 targetPos)
        {
            if (!requireFacing) return true;
            Vector3 dir = targetPos - origin;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return true;
            float angle = Vector3.Angle(forward, dir.normalized);
            return angle <= maxFacingAngle;
        }

        private void BeginInteraction(ESInteractable target)
        {
            if (target == null || !CanEntityInteract(out lastCheckResult)) return;
            if (!target.TryAcquireInteraction(MyCore, out lastCheckResult)) return;
            if (requireGrounded && !MyCore.kcc.monitor.isStableOnGround)
            {
                lastCheckResult = ESInteractionCheckResult.RequiresGrounded;
                target.ReleaseInteraction(MyCore);
                return;
            }
            if (!EnsureStateMachineReady())
            {
                lastCheckResult = ESInteractionCheckResult.StateUnavailable;
                target.ReleaseInteraction(MyCore);
                return;
            }

            ikLastStatus = "BeginInteraction";
            ikLastTargetMoveDistance = 0f;
            _ikHasPrevTargetPos = false;

            _interactionStartTime = Time.time;
            activeInteractable = target;
            isInteracting = true;
            currentCandidate = target;

            _activeState = ResolveStateForInteractable(target);
            if (_activeState != null)
            {
                string stateKey = ResolveInteractionStateKey(target, _activeState);
                _interactionLifecycle.SetTarget(_sm, _activeState, stateKey);
                bool activated = _activeState.baseStatus == StateBaseStatus.Running || _sm.TryActivateState(_activeState);
                if (!_interactionLifecycle.TryEnter(activated))
                {
                    EndInteraction(false, ESInteractionEndReason.BeginRejected);
                    return;
                }
            }

            if (overrideSupportFlag && _sm != null)
            {
                _prevSupportFlag = _sm.currentSupportFlags;
                _sm.SetSupportFlags(interactionSupportFlag);
                _hasOverriddenSupportFlag = true;
            }

            ApplyMatchTargetIfNeeded(target);
            target.OnInteractStarted(MyCore);
        }

        private void UpdateInteraction(float deltaTime)
        {
            if (activeInteractable == null)
            {
                EndInteraction(false, ESInteractionEndReason.TargetLost);
                return;
            }

            if (!activeInteractable.isActiveAndEnabled || activeInteractable.InteractionOwner != MyCore)
            {
                EndInteraction(false, ESInteractionEndReason.TargetLost);
                return;
            }

            if (_interactionLifecycle.CheckExit())
            {
                EndInteraction(false, ESInteractionEndReason.StateExited);
                return;
            }

            if (cancelOnMoveInput && MyCore.kcc.moveInput.sqrMagnitude >= cancelMoveThreshold * cancelMoveThreshold)
            {
                EndInteraction(false, ESInteractionEndReason.MovementCancelled);
                return;
            }
            float elapsed = Time.time - _interactionStartTime;
            float duration = Mathf.Max(0f, activeInteractable.interactDuration);
            float timeout = activeInteractable.interactTimeout > 0f ? activeInteractable.interactTimeout : defaultInteractTimeout;

            ApplyIK(activeInteractable, elapsed, duration);
            activeInteractable.OnInteractUpdate(MyCore, deltaTime);

            if (duration > 0f && elapsed >= duration)
            {
                EndInteraction(true, ESInteractionEndReason.Completed);
                return;
            }

            if (timeout > 0f && elapsed >= timeout)
            {
                EndInteraction(false, ESInteractionEndReason.Timeout);
                return;
            }
        }

        private void ApplyIK(ESInteractable target, float elapsed, float duration)
        {
            if (_activeState == null)
            {
                ikLastStatus = "Blocked: ActiveState is null";
                return;
            }

            float normalized01 = duration > 0.001f ? Mathf.Clamp01(elapsed / duration) : 0f;

            ESInteractable.IKWriteBuildResult buildResult = target.TryBuildIKWriteRequest(MyCore, normalized01, out var req);
            if (buildResult != ESInteractable.IKWriteBuildResult.Success)
            {
                ikLastStatus = buildResult == ESInteractable.IKWriteBuildResult.Disabled
                    ? "Blocked: Interactable.enableIK == false"
                    : "Blocked: Interactable.ikTarget is null";
                ikLastTarget = null;
                ikLastHintTarget = target.ikHintTarget;
                return;
            }

            ApplyIKDebugSnapshot(normalized01, in req);

            if (req.weight <= 0.0001f)
                ikLastStatus = "Applied with near-zero weight (check curve/config)";
            else
                ikLastStatus = "Applied";

            ikLastWriteTime = Time.time;
            _activeState.SetIKGoal(req.goal, req.target, req.weight, req.lerpingRate, req.hintTarget, req.useTargetRotation);
        }

        private void ApplyIKDebugSnapshot(float normalized01, in ESInteractable.IKWriteRequest req)
        {
            ikLastNormalized01 = normalized01;
            ikLastEvaluatedWeight = req.weight;
            ikLastEvaluatedLerpingRate = req.lerpingRate;
            ikLastTarget = req.target;
            ikLastHintTarget = req.hintTarget;

            if (_ikHasPrevTargetPos)
                ikLastTargetMoveDistance = Vector3.Distance(_ikPrevTargetPos, req.target.position);
            else
            {
                ikLastTargetMoveDistance = 0f;
                _ikHasPrevTargetPos = true;
            }

            _ikPrevTargetPos = req.target.position;
        }

        private void ApplyMatchTargetIfNeeded(ESInteractable target)
        {
            if (_activeState == null) return;
            if (!target.enableMatchTarget) return;

            // MatchTargetRequest 仅承载请求参数与偏移；目标位姿由运行时传入。
            _activeState.ApplyMatchTarget(
                target.matchTargetRequest,
                target.transform.position,
                target.transform.rotation);
        }

        private void EndInteraction(bool success, ESInteractionEndReason reason)
        {
            ESInteractable endingTarget = activeInteractable;
            if (activeInteractable != null)
            {
                activeInteractable.OnInteractEnded(MyCore, success, reason);
            }

            if (_activeState != null)
            {
                _activeState.DisableIK();
                _activeState.CancelMatchTarget();

                if (_sm != null)
                {
                    _interactionLifecycle.SetTarget(_sm, _activeState, ResolveInteractionStateKey(activeInteractable, _activeState));
                    if (!_interactionLifecycle.RequestExit() && _activeState.baseStatus == StateBaseStatus.Running)
                        _sm.TryDeactivateState(_activeState.strKey);
                }
            }

            if (_hasOverriddenSupportFlag && _sm != null)
            {
                _sm.SetSupportFlags(_prevSupportFlag);
                _hasOverriddenSupportFlag = false;
            }

            isInteracting = false;
            activeInteractable = null;
            _activeState = null;
            _ikHasPrevTargetPos = false;
            lastEndReason = reason;
            if (endingTarget != null)
                endingTarget.ReleaseInteraction(MyCore);
        }

        private StateBase ResolveStateForInteractable(ESInteractable target)
        {
            if (target == null) return null;
            if (!EnsureStateMachineReady()) return null;

            string desiredKey = target.stateKeyOverride;
            if (string.IsNullOrEmpty(desiredKey) && target.interactionStateInfo != null)
            {
                var shared = target.interactionStateInfo.sharedData;
                if (shared != null && shared.basicConfig != null)
                {
                    desiredKey = shared.basicConfig.stateName;
                }
            }

            StateBase state = null;
            if (!string.IsNullOrEmpty(desiredKey))
            {
                state = _sm.GetStateByString(desiredKey);
            }

            if (state == null && target.allowStateInjection && target.interactionStateInfo != null)
            {
                string keyOverride = string.IsNullOrEmpty(desiredKey) ? null : desiredKey;
                state = _sm.RegisterStateFromInfo(target.interactionStateInfo, keyOverride, false);
            }

            return state;
        }

        private string ResolveInteractionStateKey(ESInteractable target, StateBase state)
        {
            if (state != null && !string.IsNullOrEmpty(state.strKey))
                return state.strKey;

            if (target != null && !string.IsNullOrEmpty(target.stateKeyOverride))
                return target.stateKeyOverride;

            if (target != null && target.interactionStateInfo != null
                && target.interactionStateInfo.sharedData != null
                && target.interactionStateInfo.sharedData.basicConfig != null)
                return target.interactionStateInfo.sharedData.basicConfig.stateName;

            return string.Empty;
        }

        public override void OnDestroy()
        {
            if (isInteracting)
                EndInteraction(false, ESInteractionEndReason.ModuleDisabled);
            _interactionLifecycle.Release();
            _interactableByColliderId?.Clear();
            base.OnDestroy();
        }

        protected override void OnDisable()
        {
            if (isInteracting)
                EndInteraction(false, ESInteractionEndReason.ModuleDisabled);
            currentCandidate = null;
            base.OnDisable();
        }
    }
}
