using UnityEngine;
using RootMotion.FinalIK;

namespace ES
{
    public sealed partial class StateFinalIKDriver
    {
        private struct LimbRuntimeWeights
        {
            public float positionCurrent;
            public float positionTarget;
            public float rotationCurrent;
            public float rotationTarget;
            public float bendCurrent;
            public float bendTarget;
            public float lerpingRate;

            public void Reset()
            {
                positionCurrent = 0f;
                positionTarget = 0f;
                rotationCurrent = 0f;
                rotationTarget = 0f;
                bendCurrent = 1f;
                bendTarget = 1f;
                lerpingRate = 1f;
            }
        }

        private StateMachine _stateMachine;
        private Animator _animator;
        private readonly FinalIKComponentRefs _refs = new FinalIKComponentRefs();
        private FinalIKCapabilityFlags _caps;
        private FinalIKCapabilityFlags _presentButBad;
        private string _driverEnableStateSummary = "未绑定";
        private bool _isInternalEnableStateChange;
        private bool _runtimeBindingReady;

        private BipedIK _bipedIK;
        private bool _bipedIKReady;
        private string _bipedIKError = string.Empty;

        private Transform _lhTarget, _rhTarget, _lfTarget, _rfTarget;
        private bool _goalTargetsInit;
        private bool _goalTargetsReady;

        private Transform _lhHint, _rhHint, _lfHint, _rfHint;
        private bool _hintTargetsReady;

        private StateGeneralFinalIKDriverPose _lastPose;
        private bool _hasLastPose;
        private bool _wasBipedIKBound;

        private Vector3 _sn_lhP, _sn_rhP, _sn_lfP, _sn_rfP;
        private Quaternion _sn_lhR, _sn_rhR, _sn_lfR, _sn_rfR;

        private LookAtIK _lookAtIK;
        private bool _lookAtIKReady;
        private IKSolverLookAt.LookAtBone[] _lookAtSpineBones;
        private int _lookAtSpineBoneCount;

        private AimIK _aimIK;
        private bool _aimIKReady;
        private string _aimIKError = string.Empty;
        private Transform _aimSourceTarget;
        private Transform _aimTargetProxy;
        private Transform _aimPeekViewReference;
        private Transform _aimPeekRuntimeReference;
        private float _aimPeek;
        private float _aimWeightCurrent;
        private float _aimWeightTarget;
        private float _aimPoleWeightCurrent;
        private float _aimPoleWeightTarget;
        private float _aimIKLastHeartbeatTime = -1f;
        private float _aimIKDecayStartTime;
        private float _aimIKDecayStartWeight;
        private bool _aimIKDecaying;

        private float _lookAtWeightCurrent;
        private float _lookAtWeightTarget;
        private float _lookAtBodyWeightCurrent;
        private float _lookAtBodyWeightTarget;
        private float _lookAtHeadWeightCurrent;
        private float _lookAtHeadWeightTarget;
        private float _lookAtEyesWeightCurrent;
        private float _lookAtEyesWeightTarget;
        private float _lookAtClampWeightCurrent;
        private float _lookAtClampWeightTarget;
        private LimbRuntimeWeights _lhWeights;
        private LimbRuntimeWeights _rhWeights;
        private LimbRuntimeWeights _lfWeights;
        private LimbRuntimeWeights _rfWeights;
        private float _grounderWeightCurrent;
        private float _grounderWeightTarget;
        private float _ikWeightSmoothDeltaTime;
        private float _aimLerpingRate = 1f;
        private float _lookAtLerpingRate = 1f;
        private float _limbLerpingRate = 1f;
        private float _grounderLerpingRate = 1f;

        private GrounderBipedIK _grounderBipedIK;
        private bool _grounderReady;

        private HitReaction _hitReaction;
        private bool _hitReactionReady;
        private Recoil _recoil;
        private bool _recoilReady;

        private bool _fullLookAtCompare = true;

        private float _lastBindTryTime;
        private float _lastWarnTime;
        private int _bindTryCount;
        private int _bindSuccessCount;
        private int _applyCount;
        private int _solverUpdateCount;
        private float _lastApplyTime;
        private float _lastSolverUpdateTime;

        private const float WarnInterval = 2.0f;
    }
}