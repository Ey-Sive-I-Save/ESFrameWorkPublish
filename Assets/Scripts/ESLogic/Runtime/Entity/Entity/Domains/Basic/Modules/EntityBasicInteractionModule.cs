using System;
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

        [LabelText("Interactable Layers")]
        public LayerMask interactableLayers = ~0;

        [LabelText("Require Facing")]
        public bool requireFacing = true;

        [LabelText("Max Facing Angle"), Range(0f, 180f)]
        public float maxFacingAngle = 75f;

        [LabelText("Require Grounded")]
        public bool requireGrounded = true;

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
            _overlapBuffer = new Collider[Mathf.Max(4, detectMaxCount)];
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
                currentCandidate = FindBestInteractable();
            }
        }

        public void RequestInteract()
        {
            if (!enableInteraction || MyCore == null) return;

            if (isInteracting)
            {
                CancelInteraction(false);
                return;
            }

            if (currentCandidate == null)
            {
                currentCandidate = FindBestInteractable();
            }

            if (currentCandidate != null)
            {
                BeginInteraction(currentCandidate);
            }
        }

        private ESInteractable FindBestInteractable()
        {
            var motor = MyCore.kcc?.motor;
            Vector3 origin = motor != null ? motor.TransientPosition : MyCore.transform.position;

            int count = Physics.OverlapSphereNonAlloc(origin, detectRadius, _overlapBuffer, interactableLayers);
            ESInteractable best = null;
            float bestDist = float.MaxValue;
            Vector3 forward = MyCore.transform.forward;

            for (int i = 0; i < count; i++)
            {
                var col = _overlapBuffer[i];
                if (col == null) continue;
                var interactable = col.GetComponentInParent<ESInteractable>();
                if (interactable == null) continue;
                if (!interactable.CanInteract(MyCore)) continue;

                Vector3 targetPos = interactable.transform.position;
                float dist = Vector3.SqrMagnitude(targetPos - origin);
                if (dist >= bestDist) continue;
                if (!IsFacingTarget(forward, origin, targetPos)) continue;

                bestDist = dist;
                best = interactable;
            }

            return best;
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
            if (target == null || !target.CanInteract(MyCore)) return;
            if (requireGrounded && !MyCore.kcc.monitor.isStableOnGround) return;
            if (!EnsureStateMachineReady()) return;

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
                    EndInteraction(false);
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
                EndInteraction(false);
                return;
            }

            if (_interactionLifecycle.CheckExit())
            {
                EndInteraction(false);
                return;
            }

            if (cancelOnMoveInput && MyCore.kcc.moveInput.sqrMagnitude >= cancelMoveThreshold * cancelMoveThreshold)
            {
                CancelInteraction(false);
                return;
            }
            float elapsed = Time.time - _interactionStartTime;
            float duration = Mathf.Max(0f, activeInteractable.interactDuration);
            float timeout = activeInteractable.interactTimeout > 0f ? activeInteractable.interactTimeout : defaultInteractTimeout;

            ApplyIK(activeInteractable, elapsed, duration);
            activeInteractable.OnInteractUpdate(MyCore, deltaTime);

            if (duration > 0f && elapsed >= duration)
            {
                EndInteraction(true);
                return;
            }

            if (timeout > 0f && elapsed >= timeout)
            {
                EndInteraction(false);
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

        private void EndInteraction(bool success)
        {
            if (activeInteractable != null)
            {
                activeInteractable.OnInteractCompleted(MyCore, success);
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
        }

        private void CancelInteraction(bool success)
        {
            EndInteraction(success);
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
            _interactionLifecycle.Release();
            base.OnDestroy();
        }
    }
}
