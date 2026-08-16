using System;
using UnityEngine;

namespace ES
{
    public readonly struct ESItemInstanceViewTransferRequest
    {
        public readonly Transform viewRoot;
        public readonly EntityWeaponBinding binding;
        public readonly Transform targetParent;
        public readonly int ownerId;
        public readonly ESItemInstanceLocation location;
        public readonly int relationSlot;
        public readonly bool visible;

        public ESItemInstanceViewTransferRequest(
            Transform viewRoot,
            EntityWeaponBinding binding,
            Transform targetParent,
            int ownerId,
            ESItemInstanceLocation location,
            int relationSlot,
            bool visible)
        {
            this.viewRoot = viewRoot;
            this.binding = binding;
            this.targetParent = targetParent;
            this.ownerId = ownerId;
            this.location = location;
            this.relationSlot = relationSlot;
            this.visible = visible;
        }
    }

    /// <summary>
    /// Commits a world/hidden Item view handoff together with its instance Owner/Location.
    /// Character hand/back transitions remain exclusively owned by EntityEquipmentDomain.
    /// </summary>
    public static class ESItemInstanceViewTransfer
    {
        public static bool TryCommit(
            ESItemInstanceTable table,
            ESInstanceHandle handle,
            in ESItemInstanceViewTransferRequest request,
            out string error)
        {
            if (table == null)
            {
                error = "Item instance table is missing.";
                return false;
            }
            if (!table.TryGet(handle, out _))
            {
                error = "Item instance handle is stale or invalid.";
                return false;
            }
            if (request.ownerId <= 0 || request.relationSlot < -1)
            {
                error = "Item view transfer owner or relation slot is invalid.";
                return false;
            }
            if (request.location == ESItemInstanceLocation.Equipped)
            {
                error = "Equipped view transfer must be committed by EntityEquipmentDomain.";
                return false;
            }
            if (request.viewRoot == null
                || request.binding == null
                || request.binding.transform != request.viewRoot)
            {
                error = "Item view transfer requires an authored view root and binding.";
                return false;
            }
            if (!request.binding.ValidateReferences(out error))
                return false;
            if (request.location == ESItemInstanceLocation.World && request.targetParent == null)
            {
                error = "World Item view transfer requires an authored world parent.";
                return false;
            }
            if (request.targetParent == request.viewRoot
                || (request.targetParent != null && request.targetParent.IsChildOf(request.viewRoot)))
            {
                error = "Item view transfer target parent cannot be the view or its child.";
                return false;
            }

            Transform rollbackParent = request.viewRoot.parent;
            Vector3 rollbackPosition = request.viewRoot.localPosition;
            Quaternion rollbackRotation = request.viewRoot.localRotation;
            Vector3 rollbackScale = request.viewRoot.localScale;
            bool rollbackVisible = request.binding.IsPresentationVisible;

            try
            {
                request.viewRoot.SetParent(request.targetParent, true);
                request.binding.SetPresentationVisible(request.visible);
                if (table.TryMove(handle, request.ownerId, request.location, request.relationSlot))
                {
                    error = null;
                    return true;
                }
            }
            catch (Exception exception)
            {
                error = "Item view transfer threw and was rolled back: " + exception.Message;
                Restore(
                    request.viewRoot,
                    request.binding,
                    rollbackParent,
                    rollbackPosition,
                    rollbackRotation,
                    rollbackScale,
                    rollbackVisible);
                return false;
            }

            Restore(
                request.viewRoot,
                request.binding,
                rollbackParent,
                rollbackPosition,
                rollbackRotation,
                rollbackScale,
                rollbackVisible);
            error = "Item instance ownership update failed; view was restored.";
            return false;
        }

        private static void Restore(
            Transform viewRoot,
            EntityWeaponBinding binding,
            Transform parent,
            Vector3 position,
            Quaternion rotation,
            Vector3 scale,
            bool visible)
        {
            viewRoot.SetParent(parent, false);
            viewRoot.localPosition = position;
            viewRoot.localRotation = rotation;
            viewRoot.localScale = scale;
            binding.SetPresentationVisible(visible);
        }
    }
}
