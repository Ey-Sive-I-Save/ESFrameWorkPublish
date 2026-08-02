using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ES
{
    public enum ESResourcePlanState : byte
    {
        Idle,
        Loading,
        Prewarming,
        Ready,
        ReleasePending,
        Released,
        Failed,
        Canceled
    }

    public readonly struct ESResourcePlanError
    {
        public readonly ESAssetReferKind Kind;
        public readonly string Key;
        public readonly bool Required;
        public readonly string Message;

        public ESResourcePlanError(ESAssetReferKind kind, string key, bool required, string message)
        {
            Kind = kind;
            Key = key;
            Required = required;
            Message = message;
        }
    }

    public sealed class ESResourcePlanReport
    {
        public ESResourcePlanInfo Plan { get; internal set; }
        public ESResourcePlanState State { get; internal set; }
        public int TotalCount { get; internal set; }
        public int SuccessCount { get; internal set; }
        public int FailureCount { get; internal set; }
        public int RequiredFailureCount { get; internal set; }
        public int OptionalPendingCount { get; internal set; }
        public int RetainCount { get; internal set; }
        public bool IsBackgroundComplete => OptionalPendingCount == 0;
        public IReadOnlyList<ESResourcePlanError> Errors => errors;

        internal readonly List<ESResourcePlanError> errors = new List<ESResourcePlanError>(4);
    }

    public sealed partial class ESResourcePlanRuntimeService : IDisposable
    {
        /// <summary>
        /// Lifecycle edge for authored deferred starts. It fires only when a whole ResourcePlan
        /// reaches Ready or finishes releasing, never once per individual asset publication.
        /// Callers must re-check their own Cue dependency; this event grants no loading or retain
        /// permission.
        /// </summary>
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static event Action PlanAvailabilityChanged;

        private sealed class PrewarmedPrefab
        {
            public GameObject prefab;
            public string poolKey;
            public int count;
        }

        private sealed class Context : IDisposable
        {
            public readonly ESAssetScope scope = ESAssets.CreateScope();
            public readonly CancellationTokenSource loadingCancellation = new CancellationTokenSource();
            public readonly List<PrewarmedPrefab> prefabs = new List<PrewarmedPrefab>(8);
            public readonly List<IESResourcePlanExtensionLease> extensionLeases = new List<IESResourcePlanExtensionLease>(2);
            public readonly ESResourcePlanReport report = new ESResourcePlanReport();
            public readonly object poolSource = new object();
            public readonly UniTaskCompletionSource<ESResourcePlanReport> completion = new UniTaskCompletionSource<ESResourcePlanReport>();
            public readonly UniTaskCompletionSource allLoadsCompletion = new UniTaskCompletionSource();
            public UniTaskCompletionSource<ESResourcePlanReport> releaseCompletion;
            // Plan assets live in this child Scope. Lifetime scopes only own Plan retains:
            // ending one releases its Plan retain but never disposes another owner's scope.
            public readonly Dictionary<ESAssetScope, int> lifetimeScopeRetains = new Dictionary<ESAssetScope, int>();
            public readonly Dictionary<ESAssetScope, Action> lifetimeScopeListeners = new Dictionary<ESAssetScope, Action>();
            private HashSet<ESAssetIdentity> publishedPlanAssets;
            private readonly Dictionary<ESAssetIdentity, UnityEngine.Object> loadedPlanAssets = new Dictionary<ESAssetIdentity, UnityEngine.Object>();
            public int retainCount;
            public int unownedRetainCount;
            public bool releaseRequested;
            public ESResourcePlanState stateBeforeRelease;
            private CancellationTokenSource releaseCancellation;
            private bool disposed;

            public bool CanRevive => !disposed && releaseRequested && allLoadsCompletion.Task.Status.IsCompleted();
            public bool IsDisposed => disposed;

            public void BeginRelease()
            {
                releaseRequested = true;
                stateBeforeRelease = report.State;
                releaseCancellation?.Dispose();
                releaseCancellation = new CancellationTokenSource();
                releaseCompletion = new UniTaskCompletionSource<ESResourcePlanReport>();
            }

            public void CancelReleaseDelay()
            {
                releaseRequested = false;
                releaseCancellation?.Cancel();
            }

            public CancellationToken ReleaseCancellationToken => releaseCancellation?.Token ?? CancellationToken.None;

            public void PublishPlanAsset(ESAssetIdentity identity, UnityEngine.Object asset)
            {
                if (!identity.IsValid || asset == null)
                    return;
                publishedPlanAssets ??= new HashSet<ESAssetIdentity>();
                if (publishedPlanAssets.Add(identity))
                    ESAssets.RegisterActivePlanAsset(identity, asset);
                loadedPlanAssets[identity] = asset;
            }

            public UnityEngine.Object GetLoadedPlanAsset(ESAssetIdentity identity)
                => loadedPlanAssets.TryGetValue(identity, out UnityEngine.Object asset) ? asset : null;

            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                loadingCancellation.Cancel();
                loadingCancellation.Dispose();
                releaseCancellation?.Cancel();
                releaseCancellation?.Dispose();
                ReleaseExtensionLeases(extensionLeases);
                if (publishedPlanAssets != null)
                {
                    foreach (ESAssetIdentity identity in publishedPlanAssets)
                        ESAssets.UnregisterActivePlanAsset(identity);
                    publishedPlanAssets.Clear();
                }
                loadedPlanAssets.Clear();
                scope.Dispose();
                ESResourcePlanRuntimeService.NotifyPlanAvailabilityChanged();
            }
        }

        private readonly Dictionary<ESResourcePlanInfo, Context> contexts = new Dictionary<ESResourcePlanInfo, Context>(16);
        private readonly Dictionary<ESResourcePlanInfo, Context> latestReleasingContexts = new Dictionary<ESResourcePlanInfo, Context>(4);
        private readonly HashSet<Context> releasingContexts = new HashSet<Context>();
        // ActiveLinkList is the simple gameplay-facing owner. This table is intentionally kept
        // private: it bridges those synchronous Link notifications to the existing Plan retain
        // system and also records holds made before the runtime provider becomes ready.
        private readonly Dictionary<ESResourcePlanInfo, int> activeLinkRetains = new Dictionary<ESResourcePlanInfo, int>(8);
        private bool suppressActiveLinkCallbacks;

        /// <summary>应用全程常驻的基础计划，例如 Boot、全局 UI 与通用 Shader。</summary>
        public ActiveLinkList<ESResourcePlanInfo> Core { get; } = new ActiveLinkList<ESResourcePlanInfo>(4);

        /// <summary>应用当前地图、区域、模式等游戏内容计划。</summary>
        public ActiveLinkList<ESResourcePlanInfo> Game { get; } = new ActiveLinkList<ESResourcePlanInfo>(4);

        /// <summary>应用临时覆盖计划；可使用 ReplaceExclusive 形成 Boss/剧情等切换效果。</summary>
        public ActiveLinkList<ESResourcePlanInfo> Override { get; } = new ActiveLinkList<ESResourcePlanInfo>(4);

        /// <summary>
        /// IReceiveActiveLink 的内部桥接。每一次来自任意 ActiveLinkList 的真实激活都对应
        /// 一次既有 Plan retain；Provider 未就绪时只记录持有，待恢复阶段统一应用。
        /// </summary>
        internal void RetainFromActiveLink(ESResourcePlanInfo plan)
        {
            if (suppressActiveLinkCallbacks || plan == null)
                return;

            activeLinkRetains.TryGetValue(plan, out int retainCount);
            activeLinkRetains[plan] = retainCount + 1;
            if (CanApplyActiveLinkPlans())
                ApplyActiveLinkRetainAsync(plan).Forget();
        }

        /// <summary>归还一次来自 ActiveLinkList 的 Plan retain；不会影响 Binder、直接调用或 Scope 的其他持有。</summary>
        internal void ReleaseFromActiveLink(ESResourcePlanInfo plan)
        {
            if (suppressActiveLinkCallbacks || plan == null || !activeLinkRetains.TryGetValue(plan, out int retainCount) || retainCount <= 0)
                return;

            if (retainCount == 1)
                activeLinkRetains.Remove(plan);
            else
                activeLinkRetains[plan] = retainCount - 1;

            if (CanApplyActiveLinkPlans())
                ReleaseActiveLinkRetainAsync(plan).Forget();
        }

        private static bool CanApplyActiveLinkPlans()
        {
            return ESAssets.IsReady && ESRuntimeAssetCatalog.Current != null;
        }

        private async UniTaskVoid ApplyActiveLinkRetainAsync(ESResourcePlanInfo plan)
        {
            try
            {
                await ApplyAsync(plan, CancellationToken.None);
            }
            catch (Exception exception)
            {
                Debug.LogError("[ESRes][ActiveLinkList] 激活资源计划失败：" + exception.Message, plan);
            }
        }

        private async UniTaskVoid ReleaseActiveLinkRetainAsync(ESResourcePlanInfo plan)
        {
            try
            {
                await ReleaseAsync(plan, CancellationToken.None);
            }
            catch (Exception exception)
            {
                Debug.LogError("[ESRes][ActiveLinkList] 释放资源计划失败：" + exception.Message, plan);
            }
        }

        public bool TryGetStatus(ESResourcePlanInfo plan, out ESResourcePlanReport report)
        {
            if (plan != null && contexts.TryGetValue(plan, out Context context))
            {
                report = context.report;
                return true;
            }
            if (plan != null && latestReleasingContexts.TryGetValue(plan, out context))
            {
                report = context.report;
                return true;
            }
            report = null;
            return false;
        }

        /// <summary>
        /// 等待当前已经存在的持有完成，不新增 retain，也不在等待取消时归还既有所有权。
        /// Binder 等幂等 Owner 用它合并重复 Apply 调用。
        /// </summary>
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public UniTask<ESResourcePlanReport> WaitForCurrentApplyAsync(
            ESResourcePlanInfo plan,
            CancellationToken cancellationToken = default)
        {
            if (plan != null && contexts.TryGetValue(plan, out Context context))
                return context.completion.Task.AttachExternalCancellation(cancellationToken);
            if (plan != null && latestReleasingContexts.TryGetValue(plan, out context))
                return context.releaseCompletion.Task.AttachExternalCancellation(cancellationToken);
            return UniTask.FromResult(new ESResourcePlanReport { Plan = plan, State = ESResourcePlanState.Released });
        }

        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public void SuspendForProviderTransition()
        {
            DisposeRuntimeState(clearActiveLinkState: false);
        }

        /// <summary>Provider 重建后恢复仍由 ActiveLinkList 持有的资源计划。</summary>
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public UniTask RestoreAfterProviderTransitionAsync(CancellationToken cancellationToken = default)
            => RestoreActiveLinkRetainsAsync(cancellationToken);

        private async UniTask RestoreActiveLinkRetainsAsync(CancellationToken cancellationToken)
        {
            if (!CanApplyActiveLinkPlans() || activeLinkRetains.Count == 0)
                return;

            foreach (KeyValuePair<ESResourcePlanInfo, int> pair in activeLinkRetains)
            {
                ESResourcePlanInfo plan = pair.Key;
                int retainCount = pair.Value;
                if (plan == null || retainCount <= 0)
                    continue;

                for (int i = 0; i < retainCount; i++)
                    await ApplyAsync(plan, cancellationToken);
            }
        }

        public UniTask<ESResourcePlanReport> ApplyAsync(ESResourcePlanInfo plan, CancellationToken cancellationToken = default)
            => ApplyAsync(plan, null, cancellationToken);

        /// <summary>
        /// Advanced lifecycle binding. The Plan still owns its child resource Scope, while this
        /// scope owns one Plan retain. Releasing either side removes only that relationship.
        /// </summary>
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public UniTask<ESResourcePlanReport> ApplyAsync(ESResourcePlanInfo plan, ESAssetScope lifetimeScope, CancellationToken cancellationToken = default)
        {
            if (plan == null)
                return UniTask.FromException<ESResourcePlanReport>(new ArgumentNullException(nameof(plan)));
            if (lifetimeScope != null && lifetimeScope.IsDisposed)
                return UniTask.FromException<ESResourcePlanReport>(new ObjectDisposedException(nameof(lifetimeScope)));

            if (!ESAssets.IsReady || ESRuntimeAssetCatalog.Current == null)
                return UniTask.FromException<ESResourcePlanReport>(new InvalidOperationException(
                    "[ESRes][Plan] AssetTable/Runtime Provider 尚未就绪，不能创建资源计划 Scope。"));

            if (contexts.TryGetValue(plan, out Context existing))
            {
                existing.retainCount++;
                AttachLifetimeRetain(plan, existing, lifetimeScope);
                existing.report.RetainCount = existing.retainCount;
                return AwaitApplyWithCancellationRollbackAsync(plan, existing, lifetimeScope, cancellationToken);
            }

            // A region/map can be left and immediately re-entered during its release cooldown.
            // Its assets and pool warmup are still valid, so reclaim the same Context instead of
            // creating a second scope and churning the pool. Contexts still cancelling loads are
            // deliberately not revived: their work has already been invalidated.
            if (latestReleasingContexts.TryGetValue(plan, out Context cooling) && TryReviveCoolingContext(plan, cooling))
            {
                cooling.retainCount++;
                AttachLifetimeRetain(plan, cooling, lifetimeScope);
                cooling.report.RetainCount = cooling.retainCount;
                return AwaitApplyWithCancellationRollbackAsync(plan, cooling, lifetimeScope, cancellationToken);
            }

            var context = new Context();
            context.report.Plan = plan;
            context.report.State = ESResourcePlanState.Loading;
            context.retainCount = 1;
            AttachLifetimeRetain(plan, context, lifetimeScope);
            context.report.RetainCount = context.retainCount;
            contexts.Add(plan, context);
            ApplyCoreAsync(plan, context).Forget();
            return AwaitApplyWithCancellationRollbackAsync(plan, context, lifetimeScope, cancellationToken);
        }

        private bool TryReviveCoolingContext(ESResourcePlanInfo plan, Context context)
        {
            if (context == null || !context.CanRevive)
                return false;

            context.CancelReleaseDelay();
            context.report.State = context.stateBeforeRelease == ESResourcePlanState.ReleasePending
                ? ESResourcePlanState.Ready
                : context.stateBeforeRelease;
            contexts[plan] = context;
            releasingContexts.Remove(context);
            if (latestReleasingContexts.TryGetValue(plan, out Context latest) && ReferenceEquals(latest, context))
                latestReleasingContexts.Remove(plan);
            return true;
        }

        private async UniTask<ESResourcePlanReport> AwaitApplyWithCancellationRollbackAsync(
            ESResourcePlanInfo plan, Context context, ESAssetScope lifetimeScope, CancellationToken cancellationToken)
        {
            try
            {
                return await context.completion.Task.AttachExternalCancellation(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Apply creates ownership before its loading task becomes awaitable.  External
                // cancellation therefore means this particular caller gave up that ownership,
                // rather than merely stopping observation of a Plan which it still retains.
                // ReleaseRetainsAsync removes the retain synchronously before its delayed
                // cleanup begins, so this preserves cancellation responsiveness.
                ReleaseAsync(plan, lifetimeScope, CancellationToken.None).Forget();
                throw;
            }
        }

        private void AttachLifetimeRetain(ESResourcePlanInfo plan, Context context, ESAssetScope lifetimeScope)
        {
            if (lifetimeScope == null)
            {
                context.unownedRetainCount++;
                return;
            }

            context.lifetimeScopeRetains.TryGetValue(lifetimeScope, out int count);
            context.lifetimeScopeRetains[lifetimeScope] = count + 1;
            if (count != 0)
                return;

            Action listener = () => ReleaseFromLifetimeScope(plan, context, lifetimeScope);
            context.lifetimeScopeListeners.Add(lifetimeScope, listener);
            lifetimeScope.RegisterLifetimeReleaseListener(listener);
        }

        private bool DetachLifetimeRetain(Context context, ESAssetScope lifetimeScope)
        {
            if (lifetimeScope == null || !context.lifetimeScopeRetains.TryGetValue(lifetimeScope, out int count) || count <= 0)
                return false;

            if (count > 1)
            {
                context.lifetimeScopeRetains[lifetimeScope] = count - 1;
                return true;
            }

            context.lifetimeScopeRetains.Remove(lifetimeScope);
            if (context.lifetimeScopeListeners.TryGetValue(lifetimeScope, out Action listener))
            {
                context.lifetimeScopeListeners.Remove(lifetimeScope);
                lifetimeScope.UnregisterLifetimeReleaseListener(listener);
            }
            return true;
        }

        private void ReleaseFromLifetimeScope(ESResourcePlanInfo plan, Context context, ESAssetScope lifetimeScope)
        {
            if (!context.lifetimeScopeRetains.TryGetValue(lifetimeScope, out int count) || count <= 0)
                return;

            // The lifetime scope is already disposing, so only forget our registration; do not
            // call back into it. A Scope-owned retain may have been registered more than once.
            context.lifetimeScopeRetains.Remove(lifetimeScope);
            context.lifetimeScopeListeners.Remove(lifetimeScope);
            ReleaseRetainsAsync(plan, context, count, CancellationToken.None).Forget();
        }

        private UniTask<ESResourcePlanReport> ReleaseRetainsAsync(ESResourcePlanInfo plan, Context context, int count, CancellationToken cancellationToken)
        {
            if (count <= 0)
                return UniTask.FromResult(context.report);

            context.retainCount = Math.Max(0, context.retainCount - count);
            context.report.RetainCount = context.retainCount;
            if (context.retainCount > 0)
                return UniTask.FromResult(context.report);

            // Detach first: a subsequent Apply receives a fresh scope and cancellation lifetime.
            contexts.Remove(plan);
            latestReleasingContexts[plan] = context;
            releasingContexts.Add(context);
            context.BeginRelease();
            context.report.State = ESResourcePlanState.ReleasePending;
            context.loadingCancellation.Cancel();
            UniTaskCompletionSource<ESResourcePlanReport> releaseCompletion = context.releaseCompletion;
            ReleaseCoreAsync(plan, context, releaseCompletion, context.ReleaseCancellationToken).Forget();
            return AwaitReleaseAsync(context.releaseCompletion.Task, cancellationToken);
        }

        private static void DetachAllLifetimeScopes(Context context)
        {
            foreach (KeyValuePair<ESAssetScope, Action> pair in context.lifetimeScopeListeners)
                pair.Key?.UnregisterLifetimeReleaseListener(pair.Value);
            context.lifetimeScopeListeners.Clear();
            context.lifetimeScopeRetains.Clear();
            context.unownedRetainCount = 0;
        }

        public UniTask<ESResourcePlanReport> ReleaseAsync(ESResourcePlanInfo plan, CancellationToken cancellationToken = default)
            => ReleaseAsync(plan, null, cancellationToken);

        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public UniTask<ESResourcePlanReport> ReleaseAsync(ESResourcePlanInfo plan, ESAssetScope lifetimeScope, CancellationToken cancellationToken = default)
        {
            if (plan == null)
                return UniTask.FromResult(new ESResourcePlanReport { Plan = plan, State = ESResourcePlanState.Released });

            if (!contexts.TryGetValue(plan, out Context context))
            {
                if (latestReleasingContexts.TryGetValue(plan, out context))
                    return AwaitReleaseAsync(context.releaseCompletion.Task, cancellationToken);
                return UniTask.FromResult(new ESResourcePlanReport { Plan = plan, State = ESResourcePlanState.Released });
            }

            if (lifetimeScope == null)
            {
                if (context.unownedRetainCount <= 0)
                    return UniTask.FromResult(context.report);
                context.unownedRetainCount--;
            }
            else if (!DetachLifetimeRetain(context, lifetimeScope))
            {
                return UniTask.FromResult(context.report);
            }

            return ReleaseRetainsAsync(plan, context, 1, cancellationToken);
        }

        private static async UniTask<ESResourcePlanReport> AwaitReleaseAsync(UniTask<ESResourcePlanReport> releaseTask, CancellationToken cancellationToken)
        {
            return await releaseTask.AttachExternalCancellation(cancellationToken);
        }

        private async UniTaskVoid ApplyCoreAsync(ESResourcePlanInfo plan, Context context)
        {
            CancellationToken loadingToken = context.loadingCancellation.Token;
            try
            {
                UniTask optionalWork = UniTask.CompletedTask;
                try
                {
                    var requiredTasks = new List<UniTask>(16);
                    var optionalTasks = new List<UniTask>(16);
                    ScheduleEntries(plan, context, requiredTasks, optionalTasks, loadingToken);
                    context.report.TotalCount = requiredTasks.Count + optionalTasks.Count;
                    UniTask[] required = requiredTasks.ToArray();
                    UniTask[] optional = optionalTasks.ToArray();
                    context.report.OptionalPendingCount = optional.Length;
                    if (optional.Length > 0)
                        optionalWork = ObserveOptionalTasksAsync(optional, context, loadingToken);
                    await UniTask.WhenAll(required);

                    if (!context.releaseRequested)
                        context.report.State = context.report.RequiredFailureCount > 0
                            ? ESResourcePlanState.Failed
                            : ESResourcePlanState.Ready;
                    context.completion.TrySetResult(context.report);
                    if (context.report.State == ESResourcePlanState.Ready)
                        NotifyPlanAvailabilityChanged();
                }
                catch (OperationCanceledException)
                {
                    if (!context.releaseRequested)
                        context.report.State = ESResourcePlanState.Canceled;
                    context.completion.TrySetResult(context.report);
                }
                catch (Exception exception)
                {
                    RecordFailure(context, ESAssetReferKind.None, plan.name, true, exception);
                    if (!context.releaseRequested)
                        context.report.State = ESResourcePlanState.Failed;
                    context.completion.TrySetResult(context.report);
                }

                try
                {
                    await optionalWork;
                }
                finally
                {
                    bool hadOptionalLoads = context.report.OptionalPendingCount > 0;
                    context.report.OptionalPendingCount = 0;
                    context.allLoadsCompletion.TrySetResult();
                    // A Cue may intentionally be an optional prewarm entry. Notify once more at
                    // the plan-level optional-complete edge so a waiting authored emitter can
                    // re-check its exact Clip set without subscribing to every asset publication.
                    if (hadOptionalLoads && !context.releaseRequested && context.report.State == ESResourcePlanState.Ready)
                        NotifyPlanAvailabilityChanged();
                }
            }
            catch (Exception exception)
            {
                RecordFailure(context, ESAssetReferKind.None, plan.name, true, exception);
                if (!context.releaseRequested)
                    context.report.State = ESResourcePlanState.Failed;
                context.completion.TrySetResult(context.report);
                context.allLoadsCompletion.TrySetResult();
            }
        }

        private async UniTask ObserveOptionalTasksAsync(UniTask[] tasks, Context context, CancellationToken token)
        {
            var observers = new UniTask[tasks.Length];
            for (int i = 0; i < tasks.Length; i++)
                observers[i] = ObserveOptionalTaskAsync(tasks[i], context, token);
            await UniTask.WhenAll(observers);
        }

        private async UniTask ObserveOptionalTaskAsync(UniTask task, Context context, CancellationToken token)
        {
            try { await task; }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            catch (Exception exception)
            {
                RecordFailure(context, ESAssetReferKind.None, "optional-background", false, exception);
            }
            finally
            {
                context.report.OptionalPendingCount--;
            }
        }

        private void ScheduleEntries(ESResourcePlanInfo plan, Context context, List<UniTask> requiredTasks, List<UniTask> optionalTasks, CancellationToken token)
        {
            AddPrefabTasks(plan.prefabs, context, requiredTasks, optionalTasks, token);
            AddPrefabPrewarmTasks(plan.prefabPrewarms, context, requiredTasks, optionalTasks, token);
            AddTasks<ESResourcePlanSpriteEntry, ESAssetReferSpriteConfigKey, ESAssetReferSprite, Sprite, ESAssetReferSpriteEnumKey>(plan.sprites, ESAssetReferKind.Sprite, e => e.key, context, requiredTasks, optionalTasks, token);
            AddTasks<ESResourcePlanAudioEntry, ESAssetReferAudioClipConfigKey, ESAssetReferAudioClip, AudioClip, ESAssetReferAudioClipEnumKey>(plan.audioClips, ESAssetReferKind.AudioClip, e => e.key, context, requiredTasks, optionalTasks, token);
            AddTasks<ESResourcePlanAnimationEntry, ESAssetReferAnimationClipConfigKey, ESAssetReferAnimationClip, AnimationClip, ESAssetReferAnimationClipEnumKey>(plan.animationClips, ESAssetReferKind.AnimationClip, e => e.key, context, requiredTasks, optionalTasks, token);
            AddTasks<ESResourcePlanAnimatorEntry, ESAssetReferAnimatorControllerConfigKey, ESAssetReferAnimatorController, RuntimeAnimatorController, ESAssetReferAnimatorControllerEnumKey>(plan.animatorControllers, ESAssetReferKind.AnimatorController, e => e.key, context, requiredTasks, optionalTasks, token);
            AddTasks<ESResourcePlanMaterialEntry, ESAssetReferMaterialConfigKey, ESAssetReferMaterial, Material, ESAssetReferMaterialEnumKey>(plan.materials, ESAssetReferKind.Material, e => e.key, context, requiredTasks, optionalTasks, token);
            AddTasks<ESResourcePlanMeshEntry, ESAssetReferMeshConfigKey, ESAssetReferMesh, Mesh, ESAssetReferMeshEnumKey>(plan.meshes, ESAssetReferKind.Mesh, e => e.key, context, requiredTasks, optionalTasks, token);
            AddTasks<ESResourcePlanTextureEntry, ESAssetReferTextureConfigKey, ESAssetReferTexture, Texture, ESAssetReferTextureEnumKey>(plan.textures, ESAssetReferKind.Texture, e => e.key, context, requiredTasks, optionalTasks, token);
            AddTasks<ESResourcePlanTexture2DEntry, ESAssetReferTexture2DConfigKey, ESAssetReferTexture2D, Texture2D, ESAssetReferTexture2DEnumKey>(plan.texture2Ds, ESAssetReferKind.Texture2D, e => e.key, context, requiredTasks, optionalTasks, token);
            AddTasks<ESResourcePlanSpriteAtlasEntry, ESAssetReferSpriteAtlasConfigKey, ESAssetReferSpriteAtlas, UnityEngine.U2D.SpriteAtlas, ESAssetReferSpriteAtlasEnumKey>(plan.spriteAtlases, ESAssetReferKind.SpriteAtlas, e => e.key, context, requiredTasks, optionalTasks, token);
            AddTasks<ESResourcePlanAvatarEntry, ESAssetReferAvatarConfigKey, ESAssetReferAvatar, Avatar, ESAssetReferAvatarEnumKey>(plan.avatars, ESAssetReferKind.Avatar, e => e.key, context, requiredTasks, optionalTasks, token);
            AddTasks<ESResourcePlanPlayableEntry, ESAssetReferPlayableAssetConfigKey, ESAssetReferPlayableAsset, UnityEngine.Playables.PlayableAsset, ESAssetReferPlayableAssetEnumKey>(plan.playableAssets, ESAssetReferKind.PlayableAsset, e => e.key, context, requiredTasks, optionalTasks, token);
            AddTasks<ESResourcePlanScriptableObjectEntry, ESAssetReferScriptableObjectConfigKey, ESAssetReferScriptableObject, ScriptableObject, ESAssetReferScriptableObjectEnumKey>(plan.scriptableObjects, ESAssetReferKind.ScriptableObject, e => e.key, context, requiredTasks, optionalTasks, token);
            AddTasks<ESResourcePlanTimelineEntry, ESAssetReferTimelineAssetConfigKey, ESAssetReferUnityObject, UnityEngine.Object, ESAssetReferTimelineAssetEnumKey>(plan.timelineAssets, ESAssetReferKind.TimelineAsset, e => e.key, context, requiredTasks, optionalTasks, token);
            AddTasks<ESResourcePlanVideoEntry, ESAssetReferVideoClipConfigKey, ESAssetReferVideoClip, UnityEngine.Video.VideoClip, ESAssetReferVideoClipEnumKey>(plan.videoClips, ESAssetReferKind.VideoClip, e => e.key, context, requiredTasks, optionalTasks, token);
            AddTasks<ESResourcePlanTerrainEntry, ESAssetReferTerrainDataConfigKey, ESAssetReferTerrainData, TerrainData, ESAssetReferTerrainDataEnumKey>(plan.terrainDatas, ESAssetReferKind.TerrainData, e => e.key, context, requiredTasks, optionalTasks, token);
            AddTasks<ESResourcePlanRawEntry, ESAssetReferRawConfigKey, ESAssetReferRaw, TextAsset, ESAssetReferRawEnumKey>(plan.rawAssets, ESAssetReferKind.Raw, e => e.key, context, requiredTasks, optionalTasks, token);
            AddBakedAssetTasks(plan.BakedAssets, context, requiredTasks, optionalTasks, token);
            AddExtensionTasks(plan.BakedExtensions, plan, context, requiredTasks, optionalTasks, token);
        }

        private void AddExtensionTasks(IReadOnlyList<ESResourcePlanBakedExtensionEntry> entries, ESResourcePlanInfo plan, Context context, List<UniTask> requiredTasks, List<UniTask> optionalTasks, CancellationToken token)
        {
            if (entries == null) return;
            for (int i = 0; i < entries.Count; i++)
            {
                ESResourcePlanBakedExtensionEntry entry = entries[i];
                if (entry != null) (entry.required ? requiredTasks : optionalTasks).Add(PrepareExtensionAsync(plan, entry, context, token));
            }
        }

        private async UniTask PrepareExtensionAsync(ESResourcePlanInfo plan, ESResourcePlanBakedExtensionEntry entry, Context context, CancellationToken token)
        {
            try
            {
                int failuresBefore = context.report.FailureCount;
                for (int i = 0; i < (entry.assets?.Count ?? 0); i++)
                    if (entry.assets[i] != null) await LoadBakedAssetAsync(entry.assets[i], context, token);
                if (context.report.FailureCount != failuresBefore)
                    throw new InvalidOperationException("扩展资源未全部准备成功：" + entry.providerId);
                var extensionContext = new ESResourcePlanExtensionContext(context.GetLoadedPlanAsset);
                IESResourcePlanExtensionLease lease = await ESResourcePlanRuntimeExtensions.Resolve(entry.providerId, entry.schemaVersion).PrepareAsync(plan, entry, extensionContext, token);
                if (lease == null) throw new InvalidOperationException("ResourcePlan Runtime extension 未返回 Lease：" + entry.providerId);
                context.extensionLeases.Add(lease);
                context.report.SuccessCount++;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception exception) { RecordFailure(context, ESAssetReferKind.None, entry.providerId, entry.required, exception); }
        }

        private void AddBakedAssetTasks(IReadOnlyList<ESResourcePlanBakedAssetEntry> entries, Context context, List<UniTask> requiredTasks, List<UniTask> optionalTasks, CancellationToken token)
        {
            if (entries == null) return;
            for (int i = 0; i < entries.Count; i++)
            {
                ESResourcePlanBakedAssetEntry entry = entries[i];
                if (entry == null) continue;
                (entry.required ? requiredTasks : optionalTasks).Add(LoadBakedAssetAsync(entry, context, token));
            }
        }

        private async UniTask LoadBakedAssetAsync(ESResourcePlanBakedAssetEntry entry, Context context, CancellationToken token)
        {
            try
            {
                if (!entry.HasConfiguredKey)
                    throw new InvalidOperationException("Baked ConfigKey is empty.");
                ESRuntimeAssetCatalog catalog = ESRuntimeAssetCatalog.Current;
                if (catalog == null || !catalog.TryResolveAssetIdentity(entry.kind, entry.enumKey, entry.stringKey, out ESAssetIdentity identity))
                    throw new KeyNotFoundException("Current AssetTable does not contain baked ConfigKey: " + entry.kind + "/" + ESConfigKeyMatch.Describe(entry.enumKey, entry.stringKey));
                UnityEngine.Object asset = await context.scope.LoadResolvedAsync<UnityEngine.Object>(identity, token);
                context.PublishPlanAsset(identity, asset);
                context.report.SuccessCount++;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception exception)
            {
                RecordFailure(context, entry.kind, ESConfigKeyMatch.Describe(entry.enumKey, entry.stringKey), entry.required, exception);
            }
        }

        private void AddPrefabTasks(List<ESResourcePlanPrefabEntry> entries, Context context, List<UniTask> requiredTasks, List<UniTask> optionalTasks, CancellationToken token)
        {
            if (entries == null) return;
            for (int i = 0; i < entries.Count; i++)
                if (entries[i] != null)
                    (entries[i].required ? requiredTasks : optionalTasks).Add(LoadPrefabAsync(entries[i], context, token));
        }

        private async UniTask LoadPrefabAsync(ESResourcePlanPrefabEntry entry, Context context, CancellationToken token)
        {
            try
            {
                await LoadAsync<ESAssetReferPrefab, GameObject, ESAssetReferPrefabEnumKey>(entry.key, ESAssetReferKind.Prefab, context, token);
                context.report.SuccessCount++;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception exception) { RecordFailure(context, ESAssetReferKind.Prefab, FormatKey(entry.key), entry.required, exception); }
        }

        private void AddPrefabPrewarmTasks(List<ESResourcePlanPrefabPrewarmEntry> entries, Context context, List<UniTask> requiredTasks, List<UniTask> optionalTasks, CancellationToken token)
        {
            if (entries == null) return;
            var scheduled = new HashSet<PrefabPrewarmDataInfo>();
            for (int i = 0; i < entries.Count; i++)
            {
                ESResourcePlanPrefabPrewarmEntry entry = entries[i];
                if (entry == null) continue;
                if (entry.data != null && !scheduled.Add(entry.data))
                {
                    Debug.LogWarning($"[ESRes][Plan] 已忽略重复的 Prefab 预热配置：Plan={context.report.Plan.name}, Prewarm={entry.data.name}", context.report.Plan);
                    continue;
                }
                (entry.required ? requiredTasks : optionalTasks).Add(LoadPrefabPrewarmAsync(entry, context, token));
            }
        }

        private async UniTask LoadPrefabPrewarmAsync(ESResourcePlanPrefabPrewarmEntry entry, Context context, CancellationToken token)
        {
            try
            {
                PrefabPrewarmDataInfo data = entry.data;
                if (data == null)
                    throw new InvalidOperationException("PrefabPrewarmDataInfo 未配置。");

                ESGameObjectPoolModule pool = ESGameManager.PoolModule;
                if (pool == null)
                    throw new InvalidOperationException("对象池模块尚未就绪。");

                string sceneName = SceneManager.GetActiveScene().name;
                if (!data.Supports(sceneName, pool.currentSpaceName))
                {
                    context.report.SuccessCount++;
                    return;
                }

                if (data.entries != null)
                    for (int i = 0; i < data.entries.Count; i++)
                    {
                        token.ThrowIfCancellationRequested();
                        PrefabPrewarmEntry prefabEntry = data.entries[i];
                        if (prefabEntry == null || !prefabEntry.enabled || prefabEntry.prewarmCount <= 0)
                            continue;

                        GameObject prefab = await LoadAsync<ESAssetReferPrefab, GameObject, ESAssetReferPrefabEnumKey>(
                            prefabEntry.prefabKey, ESAssetReferKind.Prefab, context, token);
                        ESGameObjectPoolConfig config = prefabEntry.useCustomConfig ? prefabEntry.config : pool.defaultConfig;
                        pool.PrewarmOwned(prefab, prefabEntry.prewarmCount, context.poolSource, prefabEntry.key, config);
                        context.prefabs.Add(new PrewarmedPrefab
                        {
                            prefab = prefab,
                            poolKey = prefabEntry.key,
                            count = prefabEntry.prewarmCount
                        });
                    }

                context.report.SuccessCount++;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception exception)
            {
                RecordFailure(context, ESAssetReferKind.Prefab, entry.data != null ? entry.data.name : "<null-prewarm>", entry.required, exception);
            }
        }

        private void AddTasks<TEntry, TKey, TRefer, TAsset, TEnum>(List<TEntry> entries, ESAssetReferKind kind, Func<TEntry, TKey> getKey, Context context, List<UniTask> requiredTasks, List<UniTask> optionalTasks, CancellationToken token)
            where TEntry : ESResourcePlanEntryBase
            where TKey : ESAssetConfigKey<TEnum>
            where TRefer : ESAssetRefer<TAsset>, new()
            where TAsset : UnityEngine.Object
            where TEnum : struct, Enum
        {
            if (entries == null) return;
            for (int i = 0; i < entries.Count; i++)
                if (entries[i] != null)
                    (entries[i].required ? requiredTasks : optionalTasks).Add(LoadEntryAsync<TEntry, TKey, TRefer, TAsset, TEnum>(entries[i], getKey(entries[i]), kind, context, token));
        }

        private async UniTask LoadEntryAsync<TEntry, TKey, TRefer, TAsset, TEnum>(TEntry entry, TKey key, ESAssetReferKind kind, Context context, CancellationToken token)
            where TEntry : ESResourcePlanEntryBase
            where TKey : ESAssetConfigKey<TEnum>
            where TRefer : ESAssetRefer<TAsset>, new()
            where TAsset : UnityEngine.Object
            where TEnum : struct, Enum
        {
            try
            {
                await LoadAsync<TRefer, TAsset, TEnum>(key, kind, context, token);
                context.report.SuccessCount++;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception exception) { RecordFailure(context, kind, FormatKey(key), entry.required, exception); }
        }

        private static async UniTask<TAsset> LoadAsync<TRefer, TAsset, TEnum>(ESAssetConfigKey<TEnum> key, ESAssetReferKind kind, Context context, CancellationToken token)
            where TRefer : ESAssetRefer<TAsset>, new()
            where TAsset : UnityEngine.Object
            where TEnum : struct, Enum
        {
            if (key == null || (key.EnumKeyInt == 0 && string.IsNullOrEmpty(key.StringKey)))
                throw new InvalidOperationException("ConfigKey 缺少 EnumKey/StringKey。");

            ESRuntimeAssetCatalog catalog = ESRuntimeAssetCatalog.Current;
            if (catalog == null)
                throw new InvalidOperationException("当前 AssetTable Resolver 不支持 ResourcePlan 的 Key-only 解析。");
            if (!catalog.TryResolveAssetIdentity(kind, key.EnumKeyInt, key.StringKey, out ESAssetIdentity identity))
                throw new KeyNotFoundException("当前 AssetTable 未找到 ConfigKey：Kind=" + kind + ", Key=" + FormatKey(key));

            TAsset asset = await context.scope.LoadResolvedAsync<TAsset>(identity, token);
            context.PublishPlanAsset(identity, asset);
            return asset;
        }

        private static string FormatKey<TEnum>(ESAssetConfigKey<TEnum> key) where TEnum : struct, Enum
            => key == null ? "<null>" : key.EnumKeyInt != 0 ? key.EnumKeyInt.ToString() : key.StringKey;

        private static void RecordFailure(Context context, ESAssetReferKind kind, string key, bool required, Exception exception)
        {
            context.report.FailureCount++;
            if (required) context.report.RequiredFailureCount++;
            string message = exception?.Message ?? "Unknown error";
            context.report.errors.Add(new ESResourcePlanError(kind, key, required, message));
            Debug.LogError($"[ESRes][Plan] 资源处理失败：Kind={kind}, Key={key}, Required={required}, Error={message}", context.report.Plan);
        }

        private static void NotifyPlanAvailabilityChanged()
        {
            try { PlanAvailabilityChanged?.Invoke(); }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        private static void ReleaseExtensionLeases(List<IESResourcePlanExtensionLease> leases)
        {
            if (leases == null || leases.Count == 0) return;
            IESResourcePlanExtensionLease[] snapshot = leases.ToArray();
            leases.Clear();
            for (int i = snapshot.Length - 1; i >= 0; i--)
            {
                try { snapshot[i]?.Release(); }
                catch (Exception exception) { Debug.LogException(exception); }
            }
        }

        private async UniTaskVoid ReleaseCoreAsync(
            ESResourcePlanInfo plan,
            Context context,
            UniTaskCompletionSource<ESResourcePlanReport> releaseCompletion,
            CancellationToken releaseCancellationToken)
        {
            try
            {
                await context.allLoadsCompletion.Task;

                // Explicit releases and Scope endings share the same anti-thrash cooldown.
                float delay = Mathf.Max(0f, plan.releaseDelaySeconds);
                if (delay > 0f)
                    await UniTask.Delay(TimeSpan.FromSeconds(delay), DelayType.Realtime, PlayerLoopTiming.Update, releaseCancellationToken);

                ESGameObjectPoolModule pool = ESGameManager.PoolModule;
                if (pool != null)
                    for (int i = 0; i < context.prefabs.Count; i++)
                    {
                        PrewarmedPrefab prefab = context.prefabs[i];
                        pool.ReleaseOwnedPrewarm(prefab.prefab, prefab.count, context.poolSource, prefab.poolKey);
                    }
                ReleaseExtensionLeases(context.extensionLeases);

                context.report.State = ESResourcePlanState.Released;
                RemoveReleasingContext(plan, context);
                DetachAllLifetimeScopes(context);
                context.Dispose();
                releaseCompletion.TrySetResult(context.report);
            }
            catch (OperationCanceledException) when (!context.releaseRequested && context.retainCount > 0)
            {
                // Re-entry reclaimed this Context during its release cooldown. Complete the
                // old release waiter with the current usable state; a future final release
                // creates a fresh completion source.
                context.report.State = context.stateBeforeRelease == ESResourcePlanState.ReleasePending
                    ? ESResourcePlanState.Ready
                    : context.stateBeforeRelease;
                releaseCompletion.TrySetResult(context.report);
            }
            catch (OperationCanceledException) when (context.IsDisposed)
            {
                // Provider transition/service disposal deliberately cancels a cooling delay.
                // The owning service has already detached scopes; this is normal teardown,
                // not a Plan loading failure.
                context.report.State = ESResourcePlanState.Released;
                RemoveReleasingContext(plan, context);
                releaseCompletion.TrySetResult(context.report);
            }
            catch (Exception exception)
            {
                RecordFailure(context, ESAssetReferKind.None, plan.name, false, exception);
                context.report.State = ESResourcePlanState.Failed;
                RemoveReleasingContext(plan, context);
                DetachAllLifetimeScopes(context);
                context.Dispose();
                releaseCompletion.TrySetResult(context.report);
            }
        }

        private void RemoveReleasingContext(ESResourcePlanInfo plan, Context context)
        {
            releasingContexts.Remove(context);
            if (latestReleasingContexts.TryGetValue(plan, out Context latest) && ReferenceEquals(latest, context))
                latestReleasingContexts.Remove(plan);
        }

        public void Dispose()
        {
            DisposeRuntimeState(clearActiveLinkState: true);
        }

        private void DisposeRuntimeState(bool clearActiveLinkState)
        {
            if (clearActiveLinkState)
                ClearActiveLinkState();

            ESGameObjectPoolModule pool = ESGameManager.PoolModule;
            foreach (Context context in contexts.Values)
            {
                if (pool != null)
                    for (int i = 0; i < context.prefabs.Count; i++)
                    {
                        PrewarmedPrefab prefab = context.prefabs[i];
                        pool.ReleaseOwnedPrewarm(prefab.prefab, prefab.count, context.poolSource, prefab.poolKey);
                    }
                DetachAllLifetimeScopes(context);
                context.Dispose();
            }
            contexts.Clear();

            foreach (Context context in releasingContexts)
            {
                DetachAllLifetimeScopes(context);
                context.Dispose();
            }
            releasingContexts.Clear();
            latestReleasingContexts.Clear();
        }

        private void ClearActiveLinkState()
        {
            suppressActiveLinkCallbacks = true;
            try
            {
                Core.DeactivateAll();
                Game.DeactivateAll();
                Override.DeactivateAll();
                activeLinkRetains.Clear();
            }
            finally
            {
                suppressActiveLinkCallbacks = false;
            }
        }
    }

    /// <summary>资源计划的业务层入口。普通代码无需接触 Service、Scope 或对象池。</summary>
    public static class ESResourcePlanExtensions
    {
        /// <summary>
        /// 准备并持有计划中的资源。普通业务优先使用此入口；无需访问 ResourcePlans 服务、Scope 或 Handle。
        /// </summary>
        public static UniTask<ESResourcePlanReport> PrepareAsync(this ESResourcePlanInfo plan, CancellationToken cancellationToken = default)
        {
            if (plan == null)
                return UniTask.FromException<ESResourcePlanReport>(new ArgumentNullException(nameof(plan)));

            ESResourcePlanRuntimeService service = ESGameManager.ResourcePlans;
            if (service == null)
                return UniTask.FromException<ESResourcePlanReport>(new InvalidOperationException("[ESRes][Plan] 资源系统尚未就绪。"));
            return service.ApplyAsync(plan, cancellationToken);
        }

        /// <summary>兼容旧代码。新业务请使用语义更明确的 PrepareAsync。</summary>
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static UniTask<ESResourcePlanReport> ApplyAsync(this ESResourcePlanInfo plan, CancellationToken cancellationToken = default)
        {
            return plan.PrepareAsync(cancellationToken);
        }

        /// <summary>释放本次对资源计划的持有。短时间内再次准备时可按计划的缓冲时间复用资源。</summary>
        public static UniTask<ESResourcePlanReport> ReleaseAsync(this ESResourcePlanInfo plan, CancellationToken cancellationToken = default)
        {
            if (plan == null)
                return UniTask.FromResult(new ESResourcePlanReport { State = ESResourcePlanState.Released });

            ESResourcePlanRuntimeService service = ESGameManager.ResourcePlans;
            if (service == null)
                return UniTask.FromResult(new ESResourcePlanReport { Plan = plan, State = ESResourcePlanState.Released });
            return service.ReleaseAsync(plan, cancellationToken);
        }

        public static bool TryGetStatus(this ESResourcePlanInfo plan, out ESResourcePlanReport report)
        {
            ESResourcePlanRuntimeService service = ESGameManager.ResourcePlans;
            if (plan != null && service != null)
                return service.TryGetStatus(plan, out report);
            report = null;
            return false;
        }
    }

    /// <summary>将资源计划绑定到 GameObject 生命周期，无需编写加载和释放代码。</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("【ES】/资源与发布/运行时组件/资源计划绑定器")]
    public sealed class ESResourcePlanBinder : MonoBehaviour
    {
        [SerializeField, LabelText("资源计划"), Tooltip("此对象启用期间需要使用的资源计划。")]
        private ESResourcePlanInfo plan;

        [SerializeField, LabelText("启用时自动准备"), Tooltip("启用此对象后自动准备资源；通常保持开启。")]
        private bool applyOnEnable = true;

        private int activationVersion;
        private CancellationTokenSource activationCancellation;
        // Binder owns this only for the normal "release on exit" lifecycle.  It is not an
        // asset handle: it is the private owner which returns this Binder's Plan retain when
        // the GameObject leaves its lifecycle.
        private ESAssetScope lifetimeScope;
        private bool ownsPlanRetain;

        public ESResourcePlanInfo Plan => plan;
        public ESResourcePlanReport LastReport { get; private set; }
        public bool IsReady => LastReport != null && LastReport.State == ESResourcePlanState.Ready;

        private void OnEnable()
        {
            ESAssets.RuntimeBackendRebuilt -= OnRuntimeBackendRebuilt;
            ESAssets.RuntimeBackendRebuilt += OnRuntimeBackendRebuilt;
            if (!applyOnEnable || plan == null)
                return;

            int version = ++activationVersion;
            activationCancellation?.Dispose();
            activationCancellation = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
            ApplyWhenReadyAsync(version, activationCancellation.Token).Forget();
        }

        private void OnDisable()
        {
            ESAssets.RuntimeBackendRebuilt -= OnRuntimeBackendRebuilt;
            int version = ++activationVersion;
            activationCancellation?.Cancel();
            activationCancellation?.Dispose();
            activationCancellation = null;

            ESAssetScope scope = lifetimeScope;
            lifetimeScope = null;
            scope?.Dispose();

            // releaseOnExit plans use lifetimeScope above. Manual-release plans intentionally
            // remain warm and retain the existing explicit-release behavior.
            if (ownsPlanRetain && plan != null && plan.releaseOnExit)
            {
                ownsPlanRetain = false;
                if (scope == null)
                    ReleaseSafelyAsync(version).Forget();
            }
        }

        private void OnRuntimeBackendRebuilt()
        {
            // First boot is handled by OnEnable. Reapply only a Binder that really owned a
            // Plan before the old Provider was quiesced; otherwise the notification could
            // duplicate a normal startup application.
            if (!isActiveAndEnabled || !ownsPlanRetain || plan == null)
                return;

            int version = ++activationVersion;
            activationCancellation?.Cancel();
            activationCancellation?.Dispose();
            activationCancellation = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
            ReapplyAfterProviderTransitionAsync(version, activationCancellation.Token).Forget();
        }

        /// <summary>准备并持有绑定的资源计划。</summary>
        public UniTask<ESResourcePlanReport> PrepareAsync(CancellationToken cancellationToken = default)
        {
            if (plan == null)
                return UniTask.FromException<ESResourcePlanReport>(new InvalidOperationException("[ESRes][Plan] 资源计划绑定器尚未指定资源计划。"));
            return ApplyOwnedAndRememberAsync(cancellationToken);
        }

        /// <summary>兼容旧代码。新业务请使用语义更明确的 PrepareAsync。</summary>
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public UniTask<ESResourcePlanReport> ApplyAsync(CancellationToken cancellationToken = default)
            => PrepareAsync(cancellationToken);

        public UniTask<ESResourcePlanReport> ReleaseAsync(CancellationToken cancellationToken = default)
        {
            if (plan == null)
                return UniTask.FromResult(new ESResourcePlanReport { State = ESResourcePlanState.Released });
            return ReleaseOwnedAndRememberAsync(cancellationToken);
        }

        private async UniTaskVoid ApplyWhenReadyAsync(int version, CancellationToken cancellationToken)
        {
            try
            {
                await ESAssets.WaitUntilReadyAsync(cancellationToken);
                await UniTask.WaitUntil(() => ESGameManager.ResourcePlans != null, cancellationToken: cancellationToken);
                ESResourcePlanReport report = await AcquireOwnedRetainAsync(cancellationToken);
                if (version != activationVersion)
                    return;
                LastReport = report;
                ReportRequiredFailures(report);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
            catch (Exception exception)
            {
                Debug.LogError("[ESRes][Plan] 自动准备资源失败：" + exception.Message, this);
            }
        }

        private async UniTaskVoid ReapplyAfterProviderTransitionAsync(int version, CancellationToken cancellationToken)
        {
            try
            {
                await ESAssets.WaitUntilReadyAsync(cancellationToken);
                await UniTask.WaitUntil(() => ESGameManager.ResourcePlans != null, cancellationToken: cancellationToken);
                if (lifetimeScope != null && lifetimeScope.IsDisposed)
                    lifetimeScope = null;
                if (plan.releaseOnExit)
                    lifetimeScope ??= ESAssets.CreateScope();
                ESResourcePlanReport report = await ESGameManager.ResourcePlans.ApplyAsync(plan, lifetimeScope, cancellationToken);
                if (version != activationVersion)
                    return;
                LastReport = report;
                ReportRequiredFailures(report);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
            catch (Exception exception)
            {
                Debug.LogError("[ESRes][Plan] Provider 重建后自动恢复失败：" + exception.Message, this);
            }
        }

        private async UniTask<ESResourcePlanReport> ApplyOwnedAndRememberAsync(CancellationToken cancellationToken)
        {
            LastReport = await AcquireOwnedRetainAsync(cancellationToken);
            ReportRequiredFailures(LastReport);
            return LastReport;
        }

        private async UniTask<ESResourcePlanReport> AcquireOwnedRetainAsync(CancellationToken cancellationToken)
        {
            ESResourcePlanRuntimeService service = ESGameManager.ResourcePlans;
            if (service == null)
                throw new InvalidOperationException("[ESRes][Plan] 资源系统尚未就绪。");

            // A Binder represents one lifecycle owner. Re-enable or a repeated public Apply
            // must not accumulate another retain. If Provider reconstruction cleared the old
            // service context, forget the stale local flag and acquire against the new tables.
            if (ownsPlanRetain)
            {
                if (service.TryGetStatus(plan, out ESResourcePlanReport active)
                    && active.State != ESResourcePlanState.Released
                    && active.RetainCount > 0)
                    return await service.WaitForCurrentApplyAsync(plan, cancellationToken);
                ownsPlanRetain = false;
            }

            // Keep the Inspector workflow simple: only releaseOnExit plans bind to this
            // GameObject lifecycle. Manual-release plans intentionally use one unowned retain
            // which survives disable until ReleaseAsync is called.
            if (plan.releaseOnExit)
            {
                if (lifetimeScope == null || lifetimeScope.IsDisposed)
                    lifetimeScope = ESAssets.CreateScope();
            }

            ownsPlanRetain = true;
            try
            {
                return await service.ApplyAsync(plan, plan.releaseOnExit ? lifetimeScope : null, cancellationToken);
            }
            catch
            {
                // ApplyAsync has already rolled this acquisition back on external cancellation.
                ownsPlanRetain = false;
                throw;
            }
        }

        private async UniTask<ESResourcePlanReport> ReleaseOwnedAndRememberAsync(CancellationToken cancellationToken)
        {
            ESResourcePlanRuntimeService service = ESGameManager.ResourcePlans;
            if (!ownsPlanRetain || service == null)
            {
                LastReport = service != null && service.TryGetStatus(plan, out ESResourcePlanReport current)
                    ? current
                    : new ESResourcePlanReport { Plan = plan, State = ESResourcePlanState.Released };
                return LastReport;
            }

            ownsPlanRetain = false;
            ESAssetScope scope = plan.releaseOnExit ? lifetimeScope : null;
            UniTask<ESResourcePlanReport> releaseTask = service.ReleaseAsync(plan, scope, cancellationToken);
            if (scope != null)
            {
                lifetimeScope = null;
                scope.Dispose();
            }
            LastReport = await releaseTask;
            return LastReport;
        }

        private void ReportRequiredFailures(ESResourcePlanReport report)
        {
            if (report != null && report.RequiredFailureCount > 0)
                Debug.LogError($"[ESRes][Plan] 必需资源准备失败：Plan={plan.name}, Count={report.RequiredFailureCount}", this);
        }

        private async UniTaskVoid ReleaseSafelyAsync(int version)
        {
            try
            {
                ESResourcePlanReport report = await plan.ReleaseAsync();
                if (version == activationVersion)
                    LastReport = report;
            }
            catch (Exception exception)
            {
                Debug.LogError("[ESRes][Plan] 自动释放资源失败：" + exception.Message, this);
            }
        }
    }
}
