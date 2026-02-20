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

        private StateBase _activeState;
        private StateMachine _sm;
        private StateSupportFlags _prevSupportFlag = StateSupportFlags.None;
        private float _interactionStartTime = -999f;
        private Collider[] _overlapBuffer;

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

            _interactionStartTime = Time.time;
            activeInteractable = target;
            isInteracting = true;
            currentCandidate = target;

            if (_sm == null)
            {
                _sm = MyCore.stateDomain?.stateMachine;
            }

            _activeState = ResolveStateForInteractable(target);
            if (_activeState != null)
            {
                if (!_sm.TryActivateState(_activeState))
                {
                    EndInteraction(false);
                    return;
                }
            }

            if (overrideSupportFlag && _sm != null)
            {
                _prevSupportFlag = _sm.currentSupportFlags;
                _sm.SetSupportFlags(interactionSupportFlag);
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

            if (_activeState != null && _activeState.baseStatus != StateBaseStatus.Running)
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
            if (_activeState == null) return;
            if (!target.enableIK || target.ikTarget == null) return;

            float normalized01 = duration > 0.001f ? Mathf.Clamp01(elapsed / duration) : 0f;

            // 1) 总目标权重（targetWeight）：决定最终 IK 强度（会在状态内部 SmoothDamp 到 ik.weight）
            float targetWeight = Mathf.Clamp01(target.EvaluateIKTargetWeight(MyCore, normalized01));
            _activeState.SetIKExternalTargetWeight(targetWeight);

            // 2) 单肢体权重（limb weight）：控制某个手/脚的权重（通常用来控制“这次交互用哪只手、权重多少”）
            float limbWeight = Mathf.Clamp01(target.EvaluateIKLimbWeight(MyCore, normalized01));

            Vector3 pos = target.ikTarget.position;
            Quaternion rot = target.useIKRotation ? target.ikTarget.rotation : MyCore.transform.rotation;
            _activeState.SetIKGoal(target.ikGoal, pos, rot, limbWeight);

            if (target.ikHintTarget != null)
            {
                _activeState.SetIKHintPosition(target.ikGoal, target.ikHintTarget.position);
            }
        }

        private void ApplyMatchTargetIfNeeded(ESInteractable target)
        {
            if (_activeState == null) return;
            if (!target.enableMatchTarget || target.matchTarget == null) return;

            _activeState.StartMatchTarget(
                target.matchTarget.position,
                target.matchTarget.rotation,
                target.matchTargetBodyPart,
                target.matchTargetStartTime,
                target.matchTargetEndTime,
                target.matchTargetPosWeight,
                target.matchTargetRotWeight
            );
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
                if (_sm != null && _activeState.baseStatus == StateBaseStatus.Running)
                {
                    _sm.TryDeactivateState(_activeState.strKey);
                }
            }

            if (overrideSupportFlag && _sm != null)
            {
                _sm.SetSupportFlags(_prevSupportFlag);
            }

            isInteracting = false;
            activeInteractable = null;
            _activeState = null;
        }

        private void CancelInteraction(bool success)
        {
            EndInteraction(success);
        }

        private StateBase ResolveStateForInteractable(ESInteractable target)
        {
            if (_sm == null || target == null) return null;

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
    }
}
