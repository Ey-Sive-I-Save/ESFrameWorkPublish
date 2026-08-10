using KinematicCharacterController;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// Frame-aligned observation run controller. It samples existing authoritative state
    /// through ESGameManager and emits envelopes only while a run is active and trace is enabled.
    /// </summary>
    public static class ESDeveloperObservationController
    {
        private const string SourceId = "ES.DeveloperCockpit.CharacterControl";
        private const string SourceInstanceId = "CharacterControl.LateUpdate";
        private static bool observing;
        private static long sampleSequence;

        public static bool IsObserving => observing;
        public static ESCharacterControlFrameSnapshot LastSnapshot { get; private set; }

        public static bool TryBeginObservationRun(out string error)
        {
            error = string.Empty;
            if (observing)
            {
                error = "已有 Observation Run 正在运行。";
                return false;
            }

            if (!ESGameManager.LocalControl.HasControlledEntity)
            {
                error = "没有本地控制 Entity，无法开始角色控制 Observation Run。";
                return false;
            }

            observing = true;
            sampleSequence = 0;
            ESDeveloperTraceHost.BeginRun();
            ESDeveloperTraceHost.ResetSourceEpoch(SourceInstanceId);
            ESDeveloperTraceHost.Emit(
                ESDeveloperEventKind.ObservationRunStarted,
                SourceId,
                SourceInstanceId,
                "ESDeveloperCockpit",
                "observation-run/started");
            return true;
        }

        public static void StopObservationRun()
        {
            if (!observing)
            {
                return;
            }

            observing = false;
            ESDeveloperTraceHost.Emit(
                ESDeveloperEventKind.ObservationRunStopped,
                SourceId,
                SourceInstanceId,
                "ESDeveloperCockpit",
                "observation-run/stopped");
            ESDeveloperTraceHost.EndRun();
        }

        public static void SampleFrame()
        {
            if (!observing)
            {
                return;
            }

            Entity entity = ESGameManager.LocalControl?.ControlledEntity;
            if (entity == null)
            {
                return;
            }

            ESCharacterControlFrameSnapshot snapshot = BuildSnapshot(entity);
            if (snapshot.HasExclusion)
            {
                ESDeveloperTraceHost.Emit(
                    ESDeveloperEventKind.ObservationRunInvalid,
                    SourceId,
                    SourceInstanceId,
                    "ESDeveloperCockpit",
                    "observation-run/excluded:" + snapshot.ExclusionReason);
                return;
            }

            LastSnapshot = snapshot;
            ESDeveloperTraceHost.Emit(
                ESDeveloperEventKind.FrameSnapshot,
                SourceId,
                SourceInstanceId,
                "ESDeveloperCockpit",
                "observation-run/frame");
        }

        private static ESCharacterControlFrameSnapshot BuildSnapshot(Entity entity)
        {
            sampleSequence++;
            bool isGrounded = false;
            Vector3 velocity = Vector3.zero;
            if (entity.kcc != null && entity.kcc.motor != null)
            {
                KinematicCharacterMotor motor = entity.kcc.motor;
                isGrounded = motor.GroundingStatus.IsStableOnGround;
                velocity = motor.BaseVelocity;
            }

            bool isMoving = entity.kcc != null
                && entity.kcc.moveInput.sqrMagnitude > 0.0025f;
            Animator animator = entity.animator;
            string stateName = string.Empty;
            int animatorStateFrame = ESCharacterControlFrameSnapshot.UnknownFrame;
            if (animator != null && animator.isActiveAndEnabled)
            {
                AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
                stateName = state.fullPathHash.ToString();
                animatorStateFrame = Time.frameCount;
            }

            int cameraFrame = ESCharacterControlFrameSnapshot.UnknownFrame;
            if (ESGameManager.Camera != null)
            {
                cameraFrame = Time.frameCount;
            }

            string exclusionReason = ResolveExclusion(
                isGrounded,
                entity.kcc == null || entity.kcc.motor == null);
            return new ESCharacterControlFrameSnapshot(
                Time.frameCount,
                -1,
                sampleSequence,
                Time.unscaledTimeAsDouble,
                Time.deltaTime,
                Time.frameCount,
                Time.frameCount,
                Time.frameCount,
                Time.frameCount,
                animatorStateFrame,
                cameraFrame,
                entity.kcc == null ? Vector3.zero : entity.kcc.moveInput,
                entity.kcc == null ? Vector3.zero : entity.kcc.lookInput,
                velocity,
                isGrounded,
                isMoving,
                false,
                string.Empty,
                entity.GetInstanceID(),
                stateName,
                string.Empty,
                exclusionReason);
        }

        private static string ResolveExclusion(bool isGrounded, bool noMotor)
        {
            if (noMotor)
            {
                return "MissingKCCMotor";
            }

            if (!isGrounded)
            {
                return "NotStableGround";
            }

            return string.Empty;
        }
    }
}
