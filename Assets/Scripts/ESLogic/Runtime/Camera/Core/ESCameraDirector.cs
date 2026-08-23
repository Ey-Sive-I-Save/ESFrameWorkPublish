using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// 相机请求的唯一仲裁权威。它维护的是活跃请求集合，不使用“入栈、出栈恢复”作为权威状态；
    /// 每次 Push、Update、Release 或失效清理后都会重新选择获胜 Base/Shot。
    /// </summary>
    public sealed class ESCameraDirector : IDisposable
    {
        private const int DefaultRequestCapacity = 16;
        private static readonly ProfilerMarker LateTickMarker = new ProfilerMarker("ES.Camera.Director.LateTick");
        private readonly Dictionary<ESCameraViewId, ViewState> views = new Dictionary<ESCameraViewId, ViewState>(2);
        private int lastLateTickFrame = -1;
        private long nextSubmissionSequence;

        public bool RegisterView(ESCameraViewId viewId, int sceneEpoch, IESCameraViewAdapter adapter)
        {
            if (!viewId.IsValid || sceneEpoch <= 0 || adapter == null)
                return false;

            if (views.TryGetValue(viewId, out ViewState existing))
            {
                TryClearAdapter(existing.adapter);
                TryDisposeAdapter(existing.adapter);
                existing.Dispose();
                views.Remove(viewId);
            }

            views.Add(viewId, new ViewState(viewId, sceneEpoch, adapter, DefaultRequestCapacity));
            return true;
        }

        public void UnregisterView(ESCameraViewId viewId, int sceneEpoch, IESCameraViewAdapter adapter)
        {
            if (!views.TryGetValue(viewId, out ViewState view)
                || view.sceneEpoch != sceneEpoch
                || !ReferenceEquals(view.adapter, adapter))
            {
                return;
            }

            TryClearAdapter(view.adapter);
            view.Dispose();
            views.Remove(viewId);
        }

        public ESCameraLease Push(in ESCameraRequest request)
        {
            if (!request.IsStructurallyValid || !views.TryGetValue(request.viewId, out ViewState view) || !view.adapter.IsReady)
                return ESCameraLease.Invalid;

            if (!TryResolveRequest(view.adapter, request, out ESCameraRequest resolvedRequest))
                return ESCameraLease.Invalid;

            int slotIndex = view.AcquireSlot();
            RequestSlot slot = view.slots[slotIndex];
            slot.active = true;
            slot.generation = NextGeneration(slot.generation);
            slot.submissionSequence = ++nextSubmissionSequence;
            slot.request = resolvedRequest;
            view.slots[slotIndex] = slot;
            view.dirty = true;

            return new ESCameraLease(view.viewId, view.sceneEpoch, slotIndex, slot.generation);
        }

        public bool Update(ESCameraLease lease, in ESCameraRequest request)
        {
            if (!TryGetSlot(lease, out ViewState view, out RequestSlot slot)
                || !request.IsStructurallyValid
                || request.viewId != lease.viewId)
            {
                return false;
            }

            // Update preserves the original submission serial so a parameter refresh never changes a tie-break.
            if (!TryResolveRequest(view.adapter, request, out ESCameraRequest resolvedRequest))
                return false;

            slot.request = resolvedRequest;
            view.slots[lease.slot] = slot;
            view.dirty = true;
            return true;
        }

        public bool Release(ESCameraLease lease)
        {
            if (!TryGetSlot(lease, out ViewState view, out RequestSlot slot))
                return false;

            slot.active = false;
            slot.request = default;
            view.slots[lease.slot] = slot;
            view.freeSlots.Push(lease.slot);
            view.dirty = true;
            return true;
        }

        /// <summary>
        /// Returns the output transform for a View. Character systems may read this transform but
        /// never receive a Cinemachine rig or adapter reference.
        /// </summary>
        public bool TryGetOutputTransform(ESCameraViewId viewId, out Transform outputTransform)
        {
            outputTransform = null;
            if (!views.TryGetValue(viewId, out ViewState view) || !view.adapter.IsReady)
                return false;

            outputTransform = view.adapter.OutputTransform;
            return outputTransform != null;
        }

        /// <summary>查询有效 Lease 的 Owner，供运行时模块在边界处校验本地观测权。</summary>
        public bool TryGetOwner(ESCameraLease lease, out UnityEngine.Object owner)
        {
            owner = null;
            if (!TryGetSlot(lease, out _, out RequestSlot slot))
                return false;

            owner = slot.request.owner;
            return owner != null;
        }

        /// <summary>
        /// 用 Lease 提交 Look，不需要业务复传 Owner。只有该 Lease 的 Owner 与当前赢家
        /// Owner 一致时才接受，因此过期、失效或被其它 Owner 抢占的 Lease 无法驱动镜头。
        /// </summary>
        public bool TrySetLook(ESCameraLease lease, Vector2 lookInput)
        {
            if (!TryGetSlot(lease, out ViewState view, out RequestSlot slot))
                return false;

            if (view.dirty)
            {
                RecomputeWinner(view);
                ComposeModifiers(view);
                // Look authorization needs the current winner immediately, but this is not the
                // adapter commit boundary. Preserve dirty so LateTick still applies the new rig.
                view.dirty = true;
            }

            if (!view.hasWinner || !ReferenceEquals(slot.request.owner, view.winner.request.owner))
                return false;

            view.pendingLookOwner = slot.request.owner;
            view.pendingLookInput = lookInput;
            view.hasPendingLook = true;
            return true;
        }

        /// <summary>用 Lease 更新 Base/Shot 的目标；Modifier 没有 Follow/LookAt 语义。</summary>
        public bool TrySetTarget(ESCameraLease lease, Transform follow, Transform lookAt = null)
        {
            if (follow == null || !TryGetSlot(lease, out ViewState view, out RequestSlot slot))
                return false;

            if (slot.request.kind == ESCameraRequestKind.Modifier)
                return false;

            ESCameraRequest request = slot.request;
            request.follow = follow;
            request.lookAt = lookAt;
            if (!request.IsStructurallyValid)
                return false;

            slot.request = request;
            view.slots[lease.slot] = slot;
            view.dirty = true;
            return true;
        }

        public int ReleaseOwnedBy(UnityEngine.Object owner)
        {
            if (owner == null)
                return 0;

            int released = 0;
            foreach (ViewState view in views.Values)
            {
                for (int i = 0; i < view.slots.Count; i++)
                {
                    RequestSlot slot = view.slots[i];
                    if (!slot.active || !ReferenceEquals(slot.request.owner, owner))
                        continue;

                    slot.active = false;
                    slot.request = default;
                    view.slots[i] = slot;
                    view.freeSlots.Push(i);
                    view.dirty = true;
                    released++;
                }
            }

            return released;
        }

        /// <summary>
        /// ESGameManager 的唯一 LateUpdate 提交点。正常 Push/Update/Release 只标脏，
        /// 所有 View 在这里完成失效清理、获胜重算与对 Cinemachine Adapter 的一次写入。
        /// </summary>
        public void LateTick()
        {
            using (LateTickMarker.Auto())
            {
            int frame = Time.frameCount;
            if (lastLateTickFrame == frame)
                return;

            lastLateTickFrame = frame;
            foreach (ViewState view in views.Values)
                FlushView(view);
            }
        }

        /// <summary>仅供明确的剧情切镜边界使用；普通业务不得调用以避免同帧闪切。</summary>
        public bool FlushNow(ESCameraViewId viewId)
        {
            if (!views.TryGetValue(viewId, out ViewState view))
                return false;

            FlushView(view);
            return true;
        }

        public void Dispose()
        {
            foreach (ViewState view in views.Values)
            {
                TryClearAdapter(view.adapter);
                TryDisposeAdapter(view.adapter);
                view.Dispose();
            }

            views.Clear();
            lastLateTickFrame = -1;
            nextSubmissionSequence = 0;
        }

        private static int NextGeneration(int current) => current == int.MaxValue ? 1 : current + 1;

        private bool TryGetSlot(ESCameraLease lease, out ViewState view, out RequestSlot slot)
        {
            view = null;
            slot = default;
            if (!lease.IsValid
                || !views.TryGetValue(lease.viewId, out view)
                || view.sceneEpoch != lease.sceneEpoch
                || lease.slot < 0
                || lease.slot >= view.slots.Count)
            {
                return false;
            }

            slot = view.slots[lease.slot];
            return slot.active && slot.generation == lease.generation;
        }

        private static void FlushView(ViewState view)
        {
            PurgeInvalidRequests(view);
            bool needsApply = view.dirty;
            if (needsApply)
            {
                RecomputeWinner(view);
                ComposeModifiers(view);
            }

            bool hasLookInput = view.hasPendingLook
                                && view.hasWinner
                                && ReferenceEquals(view.pendingLookOwner, view.winner.request.owner);

            if (view.hasWinner)
            {
                if (needsApply || hasLookInput || !view.hasApplied)
                {
                    ESCameraRequest request = view.winner.request;
                    bool applied = TryApplyAdapter(view.adapter, new ESCameraResolvedView(
                        true,
                        needsApply || !view.hasApplied,
                        request.definition,
                        request.definitionHandle,
                        request.follow,
                        request.lookAt,
                        request.owner,
                        view.pendingLookInput,
                        hasLookInput,
                        view.modifiers));
                    view.hasApplied = applied;
                    if (!applied)
                        TryClearAdapter(view.adapter);
                }
            }
            else if (view.hasApplied)
            {
                TryClearAdapter(view.adapter);
                view.hasApplied = false;
            }

            view.hasPendingLook = false;
            view.pendingLookOwner = null;
            view.pendingLookInput = Vector2.zero;
        }

        private static void PurgeInvalidRequests(ViewState view)
        {
            for (int i = 0; i < view.slots.Count; i++)
            {
                RequestSlot slot = view.slots[i];
                if (!slot.active || IsRequestAlive(slot.request))
                    continue;

                slot.active = false;
                slot.request = default;
                view.slots[i] = slot;
                view.freeSlots.Push(i);
                view.dirty = true;
            }
        }

        private static bool IsRequestAlive(in ESCameraRequest request)
        {
            // Push/Update/TrySetTarget 已完成结构校验；LateUpdate 只处理运行中对象失效，
            // 避免在每个活动请求上重复读取 DefinitionKey 字符串。
            if (request.owner == null)
                return false;

            if (request.kind == ESCameraRequestKind.Modifier)
            {
                if (!request.modifier.IsValid)
                    return false;
            }
            else
            {
                if (request.kind != ESCameraRequestKind.Base && request.kind != ESCameraRequestKind.Shot)
                    return false;

                if (request.follow == null)
                    return false;
            }

            if (request.owner is Component component && !component.gameObject.activeInHierarchy)
                return false;

            if (request.owner is GameObject gameObject && !gameObject.activeInHierarchy)
                return false;

            return true;
        }

        private static void RecomputeWinner(ViewState view)
        {
            view.hasWinner = false;
            view.winner = default;
            for (int i = 0; i < view.slots.Count; i++)
            {
                RequestSlot candidate = view.slots[i];
                if (!candidate.active || candidate.request.kind == ESCameraRequestKind.Modifier)
                    continue;

                if (!view.hasWinner || IsCandidateBetter(candidate, view.winner))
                {
                    view.hasWinner = true;
                    view.winner = candidate;
                }
            }

            view.dirty = false;
        }

        private static void ComposeModifiers(ViewState view)
        {
            if (!view.hasWinner)
            {
                view.modifiers = ESCameraResolvedModifiers.Identity;
                return;
            }

            ESCameraDefinitionRuntimeHandle winningDefinition = view.winner.request.definitionHandle;
            ScalarAccumulator fieldOfView = new ScalarAccumulator();
            ScalarAccumulator distanceScale = new ScalarAccumulator();
            VectorAccumulator shoulderOffset = new VectorAccumulator();
            ScalarAccumulator shakeAmplitude = new ScalarAccumulator();

            for (int i = 0; i < view.slots.Count; i++)
            {
                RequestSlot slot = view.slots[i];
                ESCameraRequest request = slot.request;
                if (!slot.active
                    || request.kind != ESCameraRequestKind.Modifier
                    || (request.compatibleDefinition.IsConfigured
                        && request.compatibleDefinitionHandle != winningDefinition))
                {
                    continue;
                }

                fieldOfView.Apply(request.modifier.fieldOfView, request.priority, slot.submissionSequence);
                distanceScale.Apply(request.modifier.distanceScale, request.priority, slot.submissionSequence);
                shoulderOffset.Apply(request.modifier.shoulderOffset, request.priority, slot.submissionSequence);
                shakeAmplitude.Apply(request.modifier.shakeAmplitude, request.priority, slot.submissionSequence);
            }

            view.modifiers = new ESCameraResolvedModifiers(
                fieldOfView.ToResolved(),
                distanceScale.ToResolved(),
                shoulderOffset.ToResolved(),
                shakeAmplitude.ToResolved());
        }

        private static bool IsCandidateBetter(in RequestSlot candidate, in RequestSlot current)
        {
            int priorityCompare = candidate.request.priority.CompareTo(current.request.priority);
            if (priorityCompare != 0)
                return priorityCompare > 0;

            int kindCompare = GetKindRank(candidate.request.kind).CompareTo(GetKindRank(current.request.kind));
            if (kindCompare != 0)
                return kindCompare > 0;

            // Later submission wins ties deterministically. Update() deliberately preserves this serial.
            return candidate.submissionSequence > current.submissionSequence;
        }

        private static int GetKindRank(ESCameraRequestKind kind)
        {
            return kind == ESCameraRequestKind.Shot ? 2 : 1;
        }

        private static bool TryApplyAdapter(IESCameraViewAdapter adapter, in ESCameraResolvedView resolved)
        {
            try
            {
                return adapter.IsReady && adapter.Apply(resolved);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return false;
            }
        }

        private static bool TryResolveRequest(IESCameraViewAdapter adapter, in ESCameraRequest request, out ESCameraRequest resolved)
        {
            resolved = request;
            if (request.kind == ESCameraRequestKind.Modifier)
            {
                if (!request.compatibleDefinition.IsConfigured)
                    return true;

                return adapter.TryResolveDefinition(request.compatibleDefinition, out resolved.compatibleDefinitionHandle);
            }

            return adapter.TryResolveDefinition(request.definition, out resolved.definitionHandle);
        }

        private static void TryClearAdapter(IESCameraViewAdapter adapter)
        {
            try
            {
                adapter.Clear();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private static void TryDisposeAdapter(IESCameraViewAdapter adapter)
        {
            if (!(adapter is IDisposable disposable))
                return;

            try
            {
                disposable.Dispose();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private struct RequestSlot
        {
            public bool active;
            public int generation;
            public long submissionSequence;
            public ESCameraRequest request;
        }

        private struct ScalarAccumulator
        {
            private bool hasOverride;
            private int overridePriority;
            private long overrideSequence;
            private float overrideValue;
            private float additiveValue;
            private float multiplier;
            private bool hasMultiplier;

            public void Apply(ESCameraScalarModifier modifier, int priority, long sequence)
            {
                switch (modifier.operation)
                {
                    case ESCameraModifierOperation.Override:
                        if (!hasOverride || priority > overridePriority || (priority == overridePriority && sequence > overrideSequence))
                        {
                            hasOverride = true;
                            overridePriority = priority;
                            overrideSequence = sequence;
                            overrideValue = modifier.value;
                        }
                        break;
                    case ESCameraModifierOperation.Add:
                        additiveValue += modifier.value;
                        break;
                    case ESCameraModifierOperation.Multiply:
                        multiplier = hasMultiplier ? multiplier * modifier.value : modifier.value;
                        hasMultiplier = true;
                        break;
                }
            }

            public ESCameraScalarComposition ToResolved()
            {
                return new ESCameraScalarComposition(hasOverride, overrideValue, additiveValue, hasMultiplier ? multiplier : 1f);
            }
        }

        private struct VectorAccumulator
        {
            private bool hasOverride;
            private int overridePriority;
            private long overrideSequence;
            private Vector3 overrideValue;
            private Vector3 additiveValue;

            public void Apply(ESCameraVectorModifier modifier, int priority, long sequence)
            {
                switch (modifier.operation)
                {
                    case ESCameraModifierOperation.Override:
                        if (!hasOverride || priority > overridePriority || (priority == overridePriority && sequence > overrideSequence))
                        {
                            hasOverride = true;
                            overridePriority = priority;
                            overrideSequence = sequence;
                            overrideValue = modifier.value;
                        }
                        break;
                    case ESCameraModifierOperation.Add:
                        additiveValue += modifier.value;
                        break;
                }
            }

            public ESCameraVectorComposition ToResolved()
            {
                return new ESCameraVectorComposition(hasOverride, overrideValue, additiveValue);
            }
        }

        private sealed class ViewState : IDisposable
        {
            public readonly ESCameraViewId viewId;
            public readonly int sceneEpoch;
            public readonly IESCameraViewAdapter adapter;
            public readonly List<RequestSlot> slots;
            public readonly Stack<int> freeSlots;

            public bool dirty = true;
            public bool hasWinner;
            public bool hasApplied;
            public RequestSlot winner;
            public ESCameraResolvedModifiers modifiers;
            public bool hasPendingLook;
            public UnityEngine.Object pendingLookOwner;
            public Vector2 pendingLookInput;

            public ViewState(ESCameraViewId viewId, int sceneEpoch, IESCameraViewAdapter adapter, int requestCapacity)
            {
                this.viewId = viewId;
                this.sceneEpoch = sceneEpoch;
                this.adapter = adapter;
                slots = new List<RequestSlot>(requestCapacity);
                freeSlots = new Stack<int>(requestCapacity);
                modifiers = ESCameraResolvedModifiers.Identity;
            }

            public int AcquireSlot()
            {
                if (freeSlots.Count > 0)
                    return freeSlots.Pop();

                int index = slots.Count;
                slots.Add(default);
                return index;
            }

            public void Dispose()
            {
                slots.Clear();
                freeSlots.Clear();
                hasWinner = false;
                hasApplied = false;
                winner = default;
                modifiers = ESCameraResolvedModifiers.Identity;
                hasPendingLook = false;
                pendingLookOwner = null;
                pendingLookInput = Vector2.zero;
            }
        }
    }
}
