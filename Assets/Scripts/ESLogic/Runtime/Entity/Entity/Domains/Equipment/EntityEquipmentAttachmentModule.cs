using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [Serializable, TypeRegistryItem("装备挂点模块")]
    public sealed class EntityEquipmentAttachmentModule : EntityEquipmentModuleBase
    {
        private struct RuntimeOperation
        {
            public EntityEquipmentAttachmentOperation request;
            public Transform mount;
            public Transform rollbackParent;
            public Vector3 rollbackLocalPosition;
            public Quaternion rollbackLocalRotation;
            public Vector3 rollbackLocalScale;
            public bool rollbackVisible;
        }

        [NonSerialized] private EntityTransformMapping mapping;
        [NonSerialized] private int cachedMappingGeneration;
        [NonSerialized] private Transform mainHandSocket;
        [NonSerialized] private Transform offHandSocket;
        [NonSerialized] private Transform primaryBackSocket;
        [NonSerialized] private Transform secondaryBackSocket;
        [NonSerialized] private Transform hipSocket;
        [NonSerialized] private Transform temporaryHandSocket;

        [NonSerialized] private int nextTransitionId;
        [NonSerialized] private EntityEquipmentTransitionToken activeToken;
        [NonSerialized] private EntityEquipmentTransitionPhase activePhase;
        [NonSerialized] private int activeTargetRevision;
        [NonSerialized] private int activeOperationCount;
        [NonSerialized] private RuntimeOperation primaryOperation;
        [NonSerialized] private RuntimeOperation secondaryOperation;
        [NonSerialized] private StateBase expectedAnimationState;
        [NonSerialized] private float expectedAnimationActivationTime;
        [NonSerialized] private float transitionDeadline;
        [NonSerialized] private bool committed;

        [ShowInInspector, ReadOnly, LabelText("过渡阶段")]
        public EntityEquipmentTransitionPhase TransitionPhase => activePhase;

        [ShowInInspector, ReadOnly, LabelText("过渡操作数")]
        public int ActiveOperationCount => activeOperationCount;

        public EntityEquipmentTransitionToken ActiveToken => activeToken;
        public bool HasActiveTransition => activeToken.IsValid;
        public bool HasCommittedTransition => committed;

        public override void Start()
        {
            base.Start();
            TryRefreshMappingCache(out _);
        }

        public void OnPoolSpawned()
        {
            CancelActiveTransition(restoreCommittedPose: false);
            TryRefreshMappingCache(out _);
        }

        public void OnPoolDespawned()
        {
            CancelActiveTransition(restoreCommittedPose: true);
            ClearCache();
        }

        public bool TryPrepare(
            in EntityEquipmentTransitionRequest request,
            out EntityEquipmentTransitionToken token,
            out string error)
        {
            token = default;
            if (HasActiveTransition)
            {
                error = "An equipment attachment transition is already active: " + activeToken + ".";
                return false;
            }
            if (request.phase == EntityEquipmentTransitionPhase.Idle)
            {
                error = "Equipment transition phase cannot be Idle during Prepare.";
                return false;
            }
            if (request.targetRevision <= 0)
            {
                error = "Equipment target revision must be positive.";
                return false;
            }
            if (request.timeoutSeconds <= 0f)
            {
                error = "Equipment transition timeout must be positive.";
                return false;
            }
            if (!request.primary.IsConfigured)
            {
                error = "Equipment transition requires a primary attachment operation.";
                return false;
            }
            if (request.secondary.HasAnyReference && !request.secondary.IsConfigured)
            {
                error = "Secondary attachment operation is partially configured.";
                return false;
            }
            if (request.secondary.IsConfigured
                && request.secondary.viewRoot == request.primary.viewRoot)
            {
                error = "An equipment transition cannot modify the same view twice.";
                return false;
            }
            if (!TryRefreshMappingCache(out error)
                || !TryPrepareOperation(request.primary, out RuntimeOperation preparedPrimary, out error))
            {
                return false;
            }

            RuntimeOperation preparedSecondary = default;
            int operationCount = 1;
            if (request.secondary.IsConfigured)
            {
                if (!TryPrepareOperation(request.secondary, out preparedSecondary, out error))
                    return false;
                operationCount = 2;
            }

            nextTransitionId++;
            if (nextTransitionId <= 0)
                nextTransitionId = 1;

            activeToken = new EntityEquipmentTransitionToken(
                nextTransitionId,
                MyCore.LifecycleGeneration,
                cachedMappingGeneration,
                request.targetRevision);
            activePhase = request.phase;
            activeTargetRevision = request.targetRevision;
            activeOperationCount = operationCount;
            primaryOperation = preparedPrimary;
            secondaryOperation = preparedSecondary;
            expectedAnimationState = null;
            expectedAnimationActivationTime = 0f;
            transitionDeadline = Time.time + request.timeoutSeconds;
            committed = false;
            token = activeToken;
            error = null;
            return true;
        }

        public bool TryBindAnimationState(
            in EntityEquipmentTransitionToken token,
            StateBase state,
            out string error)
        {
            if (!IsCurrent(token))
            {
                error = "Equipment transition token is stale before animation binding: " + token + ".";
                return false;
            }
            if (state == null || state.baseStatus != StateBaseStatus.Running)
            {
                error = "Equipment transition requires a running animation state.";
                return false;
            }

            expectedAnimationState = state;
            expectedAnimationActivationTime = state.activationTime;
            error = null;
            return true;
        }

        public bool TryHandleAnimationEvent(
            StateBase state,
            string eventName,
            out EntityEquipmentTransitionSignal signal,
            out EntityEquipmentTransitionToken token,
            out EntityEquipmentTransitionPhase phase,
            out string error)
        {
            signal = EntityEquipmentTransitionSignal.None;
            token = activeToken;
            phase = activePhase;
            if (!HasActiveTransition)
            {
                error = "No active equipment transition.";
                return false;
            }
            if (state == null
                || !ReferenceEquals(state, expectedAnimationState)
                || state.activationTime != expectedAnimationActivationTime)
            {
                error = "Equipment animation event came from a stale state activation.";
                return false;
            }

            if (string.Equals(eventName, EntityEquipmentAnimationEvents.Commit, StringComparison.Ordinal))
            {
                if (!TryCommit(token, out error))
                    return false;
                signal = EntityEquipmentTransitionSignal.Committed;
                return true;
            }
            if (string.Equals(eventName, EntityEquipmentAnimationEvents.Complete, StringComparison.Ordinal))
            {
                if (!TryComplete(token, out error))
                    return false;
                signal = EntityEquipmentTransitionSignal.Completed;
                return true;
            }
            if (string.Equals(eventName, EntityEquipmentAnimationEvents.Cancel, StringComparison.Ordinal))
            {
                if (!TryCancel(token, out error))
                    return false;
                signal = EntityEquipmentTransitionSignal.Cancelled;
                return true;
            }

            error = "Animation event is not an equipment transition event: " + eventName + ".";
            return false;
        }

        public bool TryCommit(
            in EntityEquipmentTransitionToken token,
            out string error)
        {
            if (!IsCurrent(token))
            {
                error = "Equipment transition token is stale before commit: " + token + ".";
                return false;
            }
            if (expectedAnimationState == null
                || expectedAnimationState.baseStatus != StateBaseStatus.Running
                || expectedAnimationState.activationTime != expectedAnimationActivationTime)
            {
                error = "Equipment transition has no matching running animation state.";
                return false;
            }
            if (committed)
            {
                error = "Equipment transition has already committed: " + token + ".";
                return false;
            }
            if (!TryValidateRuntimeOperation(primaryOperation, out error)
                || (activeOperationCount == 2
                    && !TryValidateRuntimeOperation(secondaryOperation, out error)))
            {
                return false;
            }

            try
            {
                ApplyOperation(primaryOperation);
                if (activeOperationCount == 2)
                    ApplyOperation(secondaryOperation);
            }
            catch (Exception exception)
            {
                RestoreAllOperations();
                error = "Equipment attachment commit threw and was rolled back: " + exception.Message;
                return false;
            }

            if (!IsCurrent(token))
            {
                RestoreAllOperations();
                error = "Equipment transition changed during commit: " + token + ".";
                return false;
            }

            committed = true;
            error = null;
            return true;
        }

        public bool TryComplete(
            in EntityEquipmentTransitionToken token,
            out string error)
        {
            if (!IsCurrent(token))
            {
                error = "Equipment transition token is stale before completion: " + token + ".";
                return false;
            }
            if (!committed)
            {
                error = "Equipment transition must commit before completion: " + token + ".";
                return false;
            }

            CompleteVisibility(primaryOperation.request);
            if (activeOperationCount == 2)
                CompleteVisibility(secondaryOperation.request);
            ClearActiveTransition();
            error = null;
            return true;
        }

        public bool TryCancel(
            in EntityEquipmentTransitionToken token,
            out string error)
        {
            if (!IsCurrent(token))
            {
                error = "Equipment transition token is stale before cancellation: " + token + ".";
                return false;
            }

            CancelActiveTransition(restoreCommittedPose: true);
            error = null;
            return true;
        }

        public bool TryAbortInvalidOrExpired(
            out EntityEquipmentTransitionToken token,
            out EntityEquipmentTransitionPhase phase,
            out string error)
        {
            token = activeToken;
            phase = activePhase;
            if (!HasActiveTransition)
            {
                error = null;
                return false;
            }

            bool animationInvalid = expectedAnimationState != null
                && (expectedAnimationState.baseStatus != StateBaseStatus.Running
                    || expectedAnimationState.activationTime != expectedAnimationActivationTime);
            bool expired = Time.time >= transitionDeadline;
            bool tokenInvalid = !IsCurrent(activeToken);
            if (!animationInvalid && !expired && !tokenInvalid)
            {
                error = null;
                return false;
            }

            if (committed)
                RestoreAllOperations();
            ClearActiveTransition();
            error = tokenInvalid
                ? "Equipment transition authority changed before completion."
                : expired
                    ? "Equipment transition timed out before completion."
                    : "Equipment transition animation ended before completion.";
            return true;
        }

        public bool TryApplyInitialPose(
            in EntityEquipmentAttachmentOperation operation,
            out string error)
        {
            if (HasActiveTransition)
            {
                error = "Cannot apply an initial pose while a transition is active.";
                return false;
            }
            if (!TryRefreshMappingCache(out error)
                || !TryPrepareOperation(operation, out RuntimeOperation prepared, out error))
            {
                return false;
            }

            try
            {
                ApplyOperation(prepared);
            }
            catch (Exception exception)
            {
                RestoreOperation(prepared);
                error = "Initial equipment attachment threw and was rolled back: " + exception.Message;
                return false;
            }

            CompleteVisibility(operation);
            error = null;
            return true;
        }

        public bool IsCurrent(in EntityEquipmentTransitionToken token)
        {
            if (!token.IsValid || MyCore == null || token != activeToken)
                return false;

            EntityTransformMapping current = MyCore.EnsureTransformMapping();
            return current != null
                && current.IsMappingValid
                && token.entityGeneration == MyCore.LifecycleGeneration
                && token.mappingGeneration == current.MappingGeneration
                && token.targetRevision == activeTargetRevision;
        }

        private bool TryPrepareOperation(
            in EntityEquipmentAttachmentOperation operation,
            out RuntimeOperation prepared,
            out string error)
        {
            prepared = default;
            if (!TryValidateView(operation.viewRoot, operation.binding, out error)
                || !TryResolveSocket(operation.targetPose, out Transform mount, out error))
            {
                return false;
            }

            prepared.request = operation;
            prepared.mount = mount;
            prepared.rollbackParent = operation.viewRoot.parent;
            prepared.rollbackLocalPosition = operation.viewRoot.localPosition;
            prepared.rollbackLocalRotation = operation.viewRoot.localRotation;
            prepared.rollbackLocalScale = operation.viewRoot.localScale;
            prepared.rollbackVisible = operation.binding.IsPresentationVisible;
            error = null;
            return true;
        }

        private static bool TryValidateRuntimeOperation(
            in RuntimeOperation operation,
            out string error)
        {
            if (!TryValidateView(operation.request.viewRoot, operation.request.binding, out error))
                return false;
            if (operation.mount == null)
            {
                error = "Equipment transition lost its resolved business socket.";
                return false;
            }

            error = null;
            return true;
        }

        private bool TryRefreshMappingCache(out string error)
        {
            if (MyCore == null)
            {
                error = "Equipment attachment module has no Entity owner.";
                return false;
            }

            EntityTransformMapping current = MyCore.EnsureTransformMapping();
            if (current == null)
            {
                error = "EntityTransformMapping is missing.";
                return false;
            }

            if (!current.IsMappingValid)
            {
                error = "EntityTransformMap is invalid: "
                    + current.LastMappingConflict.Message + ".";
                return false;
            }

            int mappingGeneration = current.MappingGeneration;
            if (ReferenceEquals(mapping, current) && cachedMappingGeneration == mappingGeneration)
            {
                error = null;
                return true;
            }

            mapping = current;
            cachedMappingGeneration = mappingGeneration;
            mainHandSocket = current.Resolve(EntityEquipmentSocketKeys.MainHandSocket);
            offHandSocket = current.Resolve(EntityEquipmentSocketKeys.OffHandSocket);
            primaryBackSocket = current.Resolve(EntityEquipmentSocketKeys.PrimaryBackSocket);
            secondaryBackSocket = current.Resolve(EntityEquipmentSocketKeys.SecondaryBackSocket);
            hipSocket = current.Resolve(EntityEquipmentSocketKeys.HipSocket);
            temporaryHandSocket = current.Resolve(EntityEquipmentSocketKeys.TemporaryHandSocket);
            error = null;
            return true;
        }

        private bool TryResolveSocket(
            EntityEquipmentAttachmentPose pose,
            out Transform socket,
            out string error)
        {
            switch (pose)
            {
                case EntityEquipmentAttachmentPose.MainHand: socket = mainHandSocket; break;
                case EntityEquipmentAttachmentPose.OffHand: socket = offHandSocket; break;
                case EntityEquipmentAttachmentPose.PrimaryBack: socket = primaryBackSocket; break;
                case EntityEquipmentAttachmentPose.SecondaryBack: socket = secondaryBackSocket; break;
                case EntityEquipmentAttachmentPose.Hip: socket = hipSocket; break;
                case EntityEquipmentAttachmentPose.TemporaryHand: socket = temporaryHandSocket; break;
                default:
                    socket = null;
                    error = "Attachment pose does not identify a character business socket: " + pose + ".";
                    return false;
            }

            if (socket == null)
            {
                error = "Required character business socket is missing: " + pose + ".";
                return false;
            }

            error = null;
            return true;
        }

        private static bool TryValidateView(
            Transform viewRoot,
            EntityWeaponBinding binding,
            out string error)
        {
            if (viewRoot == null)
            {
                error = "Equipment view root is null.";
                return false;
            }
            if (binding == null)
            {
                error = "EntityWeaponBinding is missing.";
                return false;
            }
            if (binding.transform != viewRoot)
            {
                error = "EntityWeaponBinding must be on the equipment view root.";
                return false;
            }
            if (!binding.ValidateReferences(out error))
                return false;

            error = null;
            return true;
        }

        private static void ApplyOperation(in RuntimeOperation operation)
        {
            AlignGripPivot(
                operation.request.viewRoot,
                operation.request.binding.GripPivot,
                operation.mount);
            ApplyVisibilityAtCommit(
                operation.request.binding,
                operation.request.targetVisibility);
        }

        private static void RestoreOperation(in RuntimeOperation operation)
        {
            Transform viewRoot = operation.request.viewRoot;
            EntityWeaponBinding binding = operation.request.binding;
            if (viewRoot != null)
            {
                viewRoot.SetParent(operation.rollbackParent, false);
                viewRoot.localPosition = operation.rollbackLocalPosition;
                viewRoot.localRotation = operation.rollbackLocalRotation;
                viewRoot.localScale = operation.rollbackLocalScale;
            }
            if (binding != null)
                binding.SetPresentationVisible(operation.rollbackVisible);
        }

        private void RestoreAllOperations()
        {
            RestoreOperation(primaryOperation);
            if (activeOperationCount == 2)
                RestoreOperation(secondaryOperation);
        }

        private static void AlignGripPivot(
            Transform viewRoot,
            Transform gripPivot,
            Transform socket)
        {
            Vector3 gripLocalPosition = viewRoot.InverseTransformPoint(gripPivot.position);
            Quaternion gripLocalRotation = Quaternion.Inverse(viewRoot.rotation) * gripPivot.rotation;
            Vector3 authoredScale = viewRoot.localScale;

            viewRoot.SetParent(socket, false);
            viewRoot.localScale = authoredScale;
            viewRoot.localRotation = Quaternion.Inverse(gripLocalRotation);
            viewRoot.localPosition = -(viewRoot.localRotation
                * Vector3.Scale(authoredScale, gripLocalPosition));
        }

        private static void ApplyVisibilityAtCommit(
            EntityWeaponBinding binding,
            EntityEquipmentVisibilityState target)
        {
            binding.SetPresentationVisible(target != EntityEquipmentVisibilityState.Hidden);
        }

        private static void CompleteVisibility(in EntityEquipmentAttachmentOperation operation)
        {
            if (operation.targetVisibility == EntityEquipmentVisibilityState.FadingIn)
                operation.binding.SetPresentationVisible(true);
            else if (operation.targetVisibility == EntityEquipmentVisibilityState.FadingOut)
                operation.binding.SetPresentationVisible(false);
        }

        private void CancelActiveTransition(bool restoreCommittedPose)
        {
            if (HasActiveTransition && committed && restoreCommittedPose)
                RestoreAllOperations();
            ClearActiveTransition();
        }

        private void ClearActiveTransition()
        {
            activeToken = default;
            activePhase = EntityEquipmentTransitionPhase.Idle;
            activeTargetRevision = 0;
            activeOperationCount = 0;
            primaryOperation = default;
            secondaryOperation = default;
            expectedAnimationState = null;
            expectedAnimationActivationTime = 0f;
            transitionDeadline = 0f;
            committed = false;
        }

        private void ClearCache()
        {
            mapping = null;
            cachedMappingGeneration = 0;
            mainHandSocket = null;
            offHandSocket = null;
            primaryBackSocket = null;
            secondaryBackSocket = null;
            hipSocket = null;
            temporaryHandSocket = null;
        }
    }
}
