using UnityEngine;

namespace ES
{
    public sealed partial class StateFinalIKDriver
    {
        private void ApplyFinalIKExecutionPolicy()
        {
            FinalIKDriverBlockFlags flags = FinalIKDriverBlockFlags.None;

            if (_bipedIKReady)
            {
                if (_bipedIK.enabled)
                    _bipedIK.enabled = false;
                flags |= FinalIKDriverBlockFlags.BipedAutoLateUpdateDisabled;
            }

            if (_aimIKReady)
            {
                if (_aimIK.enabled)
                    _aimIK.enabled = false;
                flags |= FinalIKDriverBlockFlags.AimAutoLateUpdateDisabled;
            }

            if (_lookAtIKReady)
            {
                if (_lookAtIK.enabled)
                    _lookAtIK.enabled = false;
                flags |= FinalIKDriverBlockFlags.LookAtAutoLateUpdateDisabled;
            }

            if (_fullBodyBipedIKReady)
            {
                var fullBodyBipedIK = _refs.fullBodyBipedIK;
                if (fullBodyBipedIK.enabled)
                    fullBodyBipedIK.enabled = false;
                flags |= FinalIKDriverBlockFlags.FullBodyAutoLateUpdateDisabled;
            }

            bool allowProceduralDelegates = _scheduleMode == FinalIKDriverScheduleMode.DriverCoreManualProceduralDelegates;
            ApplyGrounderExecutionPolicy(allowProceduralDelegates, ref flags);
            ApplyProceduralExecutionPolicy(allowProceduralDelegates, ref flags);

            _scheduleBlockFlags = flags;
        }

        private void ApplyGrounderExecutionPolicy(bool allowProceduralDelegates, ref FinalIKDriverBlockFlags flags)
        {
            if (!_grounderReady)
                return;

            if (!_bipedIKReady)
            {
                if (_grounderBipedIK.enabled)
                    _grounderBipedIK.enabled = false;
                flags |= FinalIKDriverBlockFlags.GrounderBlockedNoBiped;
                return;
            }

            _grounderBipedIK.ik = _bipedIK;

            if (!allowProceduralDelegates)
            {
                if (_grounderBipedIK.enabled)
                    _grounderBipedIK.enabled = false;
                flags |= FinalIKDriverBlockFlags.ProceduralDelegatesDisabledByMode;
                return;
            }

            bool shouldRun = _grounderWeightCurrent > 0.001f || _grounderWeightTarget > 0.001f;
            if (_grounderBipedIK.enabled != shouldRun)
                _grounderBipedIK.enabled = shouldRun;

            if (!shouldRun)
                flags |= FinalIKDriverBlockFlags.GrounderWaitingForWeight;
        }

        private void ApplyProceduralExecutionPolicy(bool allowProceduralDelegates, ref FinalIKDriverBlockFlags flags)
        {
            bool fullBodyReady = _fullBodyBipedIKReady;

            if (enableHitReaction)
            {
                bool enable = allowProceduralDelegates && enableHitReaction && _hitReactionReady && fullBodyReady;
                if (_hitReactionReady && _hitReaction.enabled != enable)
                    _hitReaction.enabled = enable;

                if (!fullBodyReady && enableHitReaction)
                    flags |= FinalIKDriverBlockFlags.HitReactionBlockedNoFullBody;
            }

            if (enableRecoil)
            {
                bool enable = allowProceduralDelegates && enableRecoil && _recoilReady && fullBodyReady;
                if (_recoilReady && _recoil.enabled != enable)
                    _recoil.enabled = enable;

                if (!fullBodyReady && enableRecoil)
                    flags |= FinalIKDriverBlockFlags.RecoilBlockedNoFullBody;
            }

            if (!allowProceduralDelegates)
                flags |= FinalIKDriverBlockFlags.ProceduralDelegatesDisabledByMode;
        }

        private void SetFinalIKScheduleMode(FinalIKDriverScheduleMode mode)
        {
            _scheduleMode = mode;
            ApplyFinalIKExecutionPolicy();
        }

        public void IKUseHybridProceduralSchedule()
        {
            SetFinalIKScheduleMode(FinalIKDriverScheduleMode.DriverCoreManualProceduralDelegates);
        }

        public void IKUseCoreManualOnlySchedule()
        {
            SetFinalIKScheduleMode(FinalIKDriverScheduleMode.DriverCoreManualOnly);
        }
    }
}
