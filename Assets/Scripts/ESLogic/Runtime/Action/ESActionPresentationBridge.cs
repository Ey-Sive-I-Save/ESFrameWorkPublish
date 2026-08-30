using System;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{
    public sealed class ESActionPresentationBridge : IDisposable
    {
        private readonly ESActionEventChannel channel;
        private ESActionConfigKeyTable actionTable;
        private Entity owner;
        private ESWeaponConfigKey weaponKey;
        private Func<Transform> weaponMountResolver;
        private Func<ESWeaponConfigKey> weaponKeyResolver;
        private readonly ESActionHitstopRuntime hitstopRuntime = new ESActionHitstopRuntime();
        private readonly HashSet<string> reportedPresentationErrors = new HashSet<string>();
        private bool disposed;

        public ESActionPresentationBridge(ESActionEventChannel channel)
            : this(channel, null, null, null)
        {
        }

        public ESActionPresentationBridge(
            ESActionEventChannel channel,
            ESActionConfigKeyTable actionTable,
            Entity owner = null,
            ESWeaponConfigKey weaponKey = null,
            Func<Transform> weaponMountResolver = null,
            Func<ESWeaponConfigKey> weaponKeyResolver = null)
        {
            this.channel = channel ?? throw new ArgumentNullException(nameof(channel));
            this.actionTable = actionTable;
            this.owner = owner;
            this.weaponKey = weaponKey;
            this.weaponMountResolver = weaponMountResolver;
            this.weaponKeyResolver = weaponKeyResolver;
            this.channel.Published += HandlePublished;
        }

        public ESActionHitstopRuntime HitstopRuntime => hitstopRuntime;
        internal bool HasOwnerDependencies => actionTable != null || owner != null
            || weaponMountResolver != null || weaponKeyResolver != null;
        internal Func<Transform> WeaponMountResolver => weaponMountResolver;
        internal Func<ESWeaponConfigKey> WeaponKeyResolver => weaponKeyResolver;

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            channel.Published -= HandlePublished;
            actionTable = null;
            owner = null;
            weaponKey = null;
            weaponMountResolver = null;
            weaponKeyResolver = null;
            hitstopRuntime.Clear();
        }

        private void HandlePublished(ESActionEvent evt)
        {
            if (evt.kind == ESActionEventKind.ActionStarted)
                DispatchEvent(evt);
            else if (evt.kind == ESActionEventKind.HitResolved && evt.hitResult.isHit)
                DispatchEvent(evt);
        }

        private void DispatchEvent(in ESActionEvent evt)
        {
            if (actionTable == null || evt.actionKey == null || !evt.actionKey.IsConfigured)
                return;

            if (!actionTable.TryGet(evt.actionKey, out ESActionRuntimeData runtimeData)
                || runtimeData.presentationBindings == null)
                return;

            for (int i = 0; i < runtimeData.presentationBindings.Count; i++)
            {
                ESActionPresentationBindingData binding = runtimeData.presentationBindings[i];
                if (binding == null || binding.eventKind != evt.kind)
                    continue;

                var context = new ESActionEventContext(
                    evt.handle,
                    evt.emissionId,
                    binding.eventKind,
                    binding.channel,
                    evt.actionKey,
                    evt.weaponKey ?? (weaponKeyResolver != null ? weaponKeyResolver() : weaponKey));

                if (!ESActionPresentationMappingTable.TryResolve(
                        context,
                        binding.owner,
                        out ESActionResolvedPresentationPayload payload,
                        out string error))
                {
                    ReportPresentationError(
                        "Resolve." + binding.eventKind + "." + binding.channel,
                        error);
                    continue;
                }

                if (payload.owner != binding.owner)
                {
                    ReportPresentationError(
                        "OwnerMismatch." + binding.eventKind + "." + binding.channel,
                        "Presentation Binding 与 Mapping 的 Owner 不一致："
                        + binding.eventKind + "/" + binding.channel);
                    continue;
                }

                if (payload.owner != ESActionPresentationOwner.Direct)
                    continue;

                DispatchDirect(binding.channel, payload);
            }
        }

        private void DispatchDirect(
            ESActionPresentationChannel channel,
            in ESActionResolvedPresentationPayload payload)
        {
            // Slice A only owns direct swing audio. Other channels remain data-only until their
            // resource ownership and lifecycle cleanup have independently passed PlayMode.
            if (channel == ESActionPresentationChannel.Audio)
            {
                if (!payload.audioState.isDeclared
                    || !payload.audioState.requiresCatalogHandle
                    || payload.audioCueKey == null
                    || !payload.audioCueKey.IsConfigured)
                {
                    ReportPresentationError("AudioPayload", "Direct Audio Mapping 缺少有效 Cue Key。");
                    return;
                }

                PlayAudio(payload);
                return;
            }

            ReportPresentationError(
                "UnsupportedDirect." + channel,
                "切片 A 尚未启用 Direct 表现通道：" + channel);
        }

        private void PlayAudio(in ESActionResolvedPresentationPayload payload)
        {
            if (ESGameManager.Audio == null || payload.audioCueKey == null)
                return;

            Transform anchor = ResolvePresentationAnchor(payload.anchor);
            if (anchor == null)
            {
                ReportPresentationError("AudioAnchor", "PresentationAnchor 无法解析：" + payload.anchor);
                return;
            }

            var request = new ESAudioPlayRequest
            {
                owner = anchor,
                followOwner = true,
                hasPosition = false,
            };
            ESGameManager.Audio.PlayAttached(payload.audioCueKey, anchor, request);
        }

        private Transform ResolvePresentationAnchor(ESActionPresentationAnchor anchor)
        {
            if (owner == null)
                return null;

            if (anchor == ESActionPresentationAnchor.OwnerRoot)
                return owner.transform;

            if (anchor == ESActionPresentationAnchor.WeaponMount)
            {
                return weaponMountResolver != null ? weaponMountResolver() : null;
            }

            return null;
        }

        private void ReportPresentationError(string key, string detail)
        {
            if (!reportedPresentationErrors.Add(key))
                return;

            Debug.LogError("[ESAction] " + detail);
        }
    }

    public sealed class ESActionHitstopRuntime
    {
        public float PendingSeconds { get; private set; }

        public void Request(string ownerId, float seconds)
        {
            PendingSeconds = Mathf.Max(PendingSeconds, Mathf.Max(0f, seconds));
        }

        public float ConsumePending()
        {
            float value = PendingSeconds;
            PendingSeconds = 0f;
            return value;
        }

        public void Clear()
        {
            PendingSeconds = 0f;
        }
    }

}
