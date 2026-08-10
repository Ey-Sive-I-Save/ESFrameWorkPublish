using System;
using UnityEngine;

namespace ES
{
    [Serializable]
    public readonly struct ESCharacterControlFrameSnapshot
    {
        public const int UnknownFrame = -1;

        public readonly int FrameCount;
        public readonly long FixedTick;
        public readonly long SampleSequence;
        public readonly double UnscaledTime;
        public readonly float DeltaTime;
        public readonly int InputUpdatedFrame;
        public readonly int LocalControlChangedFrame;
        public readonly int AIDomainIntentFrame;
        public readonly int KccUpdatedFrame;
        public readonly int AnimatorStateFrame;
        public readonly int CameraArbitrationFrame;
        public readonly Vector3 MoveInput;
        public readonly Vector3 LookInput;
        public readonly Vector3 GroundTangentVelocity;
        public readonly bool IsGrounded;
        public readonly bool IsMoving;
        public readonly bool HasStableIdentity;
        public readonly string EntityStableId;
        public readonly int RuntimeHandle;
        public readonly string StateName;
        public readonly string CameraViewId;
        public readonly string ExclusionReason;

        public ESCharacterControlFrameSnapshot(
            int frameCount,
            long fixedTick,
            long sampleSequence,
            double unscaledTime,
            float deltaTime,
            int inputUpdatedFrame,
            int localControlChangedFrame,
            int aiDomainIntentFrame,
            int kccUpdatedFrame,
            int animatorStateFrame,
            int cameraArbitrationFrame,
            Vector3 moveInput,
            Vector3 lookInput,
            Vector3 groundTangentVelocity,
            bool isGrounded,
            bool isMoving,
            bool hasStableIdentity,
            string entityStableId,
            int runtimeHandle,
            string stateName,
            string cameraViewId,
            string exclusionReason)
        {
            FrameCount = frameCount;
            FixedTick = fixedTick;
            SampleSequence = sampleSequence;
            UnscaledTime = unscaledTime;
            DeltaTime = deltaTime;
            InputUpdatedFrame = inputUpdatedFrame;
            LocalControlChangedFrame = localControlChangedFrame;
            AIDomainIntentFrame = aiDomainIntentFrame;
            KccUpdatedFrame = kccUpdatedFrame;
            AnimatorStateFrame = animatorStateFrame;
            CameraArbitrationFrame = cameraArbitrationFrame;
            MoveInput = moveInput;
            LookInput = lookInput;
            GroundTangentVelocity = groundTangentVelocity;
            IsGrounded = isGrounded;
            IsMoving = isMoving;
            HasStableIdentity = hasStableIdentity;
            EntityStableId = entityStableId ?? string.Empty;
            RuntimeHandle = runtimeHandle;
            StateName = stateName ?? string.Empty;
            CameraViewId = cameraViewId ?? string.Empty;
            ExclusionReason = exclusionReason ?? string.Empty;
        }

        public bool IsSampled =>
            FrameCount >= 0
            && SampleSequence >= 1
            && !HasExclusion;

        public bool HasExclusion => !string.IsNullOrWhiteSpace(ExclusionReason);
    }
}
