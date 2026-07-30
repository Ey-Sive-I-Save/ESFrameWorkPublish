using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// 一个逻辑 Owner 的资源域。同一 Scope 内同一身份最多持有一次，技能反复引用不会重复计数。
    /// Scope Dispose 只结束 Owner 记录，不会在游戏中卸载资源；AB 仅由安全点统一卸载。
    /// </summary>
    /// <summary>框架生命周期域；普通业务使用 ESAssetRefer 或 ResourcePlan，不直接创建或释放 Scope。</summary>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public sealed partial class ESAssetScope : IDisposable
    {
        // Value entry + Provider's existing shared lease state. This avoids allocating a
        // framework Entry object, ESAssetLease object and release delegate per loaded asset.
        private struct Entry { public UnityEngine.Object Asset; public IESRuntimeAssetLease Lease; }
        private readonly IESAssetRuntimeProvider provider;
        private readonly Dictionary<ESAssetIdentity, Entry> entries = new Dictionary<ESAssetIdentity, Entry>();
        private readonly Dictionary<ESAssetIdentity, UniTaskCompletionSource<UnityEngine.Object>> pending = new Dictionary<ESAssetIdentity, UniTaskCompletionSource<UnityEngine.Object>>();
        private readonly HashSet<Action> lifetimeReleaseListeners = new HashSet<Action>();
        private bool disposed;

        internal ESAssetScope(IESAssetRuntimeProvider runtimeProvider)
        {
            provider = runtimeProvider ?? throw new ArgumentNullException(nameof(runtimeProvider));
            ESAssets.RegisterScope(this);
        }

        public UniTask<T> LoadAsync<T>(ESAssetRefer<T> refer, CancellationToken cancellationToken = default) where T : UnityEngine.Object
        {
            if (disposed) return UniTask.FromException<T>(new ObjectDisposedException(nameof(ESAssetScope)));
            if (refer == null) return UniTask.FromException<T>(new ArgumentNullException(nameof(refer)));
            ESAssetIdentity id = refer.AssetIdentity;
            if (entries.TryGetValue(id, out Entry entry))
            {
                if (entry.Asset is T cached) return UniTask.FromResult(cached);
                return UniTask.FromException<T>(new InvalidCastException("[ESRes][Load] 同一 ESAssetIdentity 被请求为不兼容类型：" + typeof(T).Name));
            }
            if (pending.TryGetValue(id, out UniTaskCompletionSource<UnityEngine.Object> waiting))
                return AwaitPendingAsync<T>(waiting, cancellationToken);

            var completion = new UniTaskCompletionSource<UnityEngine.Object>();
            pending.Add(id, completion);
            LoadNewCoreAsync(refer, id, completion).Forget();
            return AwaitPendingAsync<T>(completion, cancellationToken);
        }

        /// <summary>
        /// 已由当前 AssetTable 解析出身份后的内部加载入口。
        /// 这样 ResourcePlan 可以只持有 ConfigKey，而不从配置对象读取 GUID 副本。
        /// </summary>
        // 供 ESLogic 的 ResourcePlan 服务调用；普通业务仍应使用 LoadAsync(ESAssetRefer)。
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public UniTask<T> LoadResolvedAsync<T>(ESAssetIdentity id, CancellationToken cancellationToken = default) where T : UnityEngine.Object
        {
            if (disposed) return UniTask.FromException<T>(new ObjectDisposedException(nameof(ESAssetScope)));
            if (!id.IsValid) return UniTask.FromException<T>(new ArgumentException("AssetTable 解析出的资产身份无效。", nameof(id)));
            if (entries.TryGetValue(id, out Entry entry))
            {
                if (entry.Asset is T cached) return UniTask.FromResult(cached);
                return UniTask.FromException<T>(new InvalidCastException("[ESRes][Load] 同一 ESAssetIdentity 被请求为不兼容类型：" + typeof(T).Name));
            }
            if (pending.TryGetValue(id, out UniTaskCompletionSource<UnityEngine.Object> waiting))
                return AwaitPendingAsync<T>(waiting, cancellationToken);

            var completion = new UniTaskCompletionSource<UnityEngine.Object>();
            pending.Add(id, completion);
            LoadResolvedCoreAsync<T>(id, completion).Forget();
            return AwaitPendingAsync<T>(completion, cancellationToken);
        }

        private static async UniTask<T> AwaitPendingAsync<T>(UniTaskCompletionSource<UnityEngine.Object> waiting, CancellationToken cancellationToken) where T : UnityEngine.Object
        {
            UnityEngine.Object result = await waiting.Task.AttachExternalCancellation(cancellationToken);
            if (result is T typed) return typed;
            throw new InvalidCastException("[ESRes][Load] 同一 ESAssetIdentity 被请求为不兼容类型：" + typeof(T).Name);
        }

        private async UniTask LoadNewCoreAsync<T>(ESAssetRefer<T> refer, ESAssetIdentity id, UniTaskCompletionSource<UnityEngine.Object> completion) where T : UnityEngine.Object
        {
            ESRuntimeAssetHandle<T> handle = default;
            try
            {
                // A merged operation must not inherit the first waiter's cancellation.
                handle = await refer.LoadWithProviderAsync(provider, CancellationToken.None);
                if (disposed) { handle.Dispose(); throw new ObjectDisposedException(nameof(ESAssetScope)); }
                entries.Add(id, new Entry { Asset = handle.Asset, Lease = handle.Lease });
                completion.TrySetResult(handle.Asset);
                handle = default;
            }
            catch (Exception exception) { handle.Dispose(); completion.TrySetException(exception); }
            finally { pending.Remove(id); }
        }

        private async UniTask LoadResolvedCoreAsync<T>(ESAssetIdentity id, UniTaskCompletionSource<UnityEngine.Object> completion) where T : UnityEngine.Object
        {
            ESRuntimeAssetHandle<T> handle = default;
            try
            {
                // 与现有 Scope 合并语义一致：底层操作不继承任一等待者的取消；
                // 等待者可以独立取消，Scope 销毁后完成的 Handle 会立即归还。
                handle = id.IsSubAsset
                    ? await provider.LoadSubAssetAsync<T>(id, CancellationToken.None)
                    : await provider.LoadMainAssetAsync<T>(id, CancellationToken.None);
                if (disposed)
                {
                    handle.Dispose();
                    throw new ObjectDisposedException(nameof(ESAssetScope));
                }

                entries.Add(id, new Entry { Asset = handle.Asset, Lease = handle.Lease });
                completion.TrySetResult(handle.Asset);
                handle = default;
            }
            catch (Exception exception)
            {
                handle.Dispose();
                completion.TrySetException(exception);
            }
            finally { pending.Remove(id); }
        }

        public bool TryGet<T>(ESAssetRefer<T> refer, out T asset) where T : UnityEngine.Object
        {
            if (refer != null && entries.TryGetValue(refer.AssetIdentity, out Entry entry) && entry.Asset is T typed) { asset = typed; return true; }
            asset = null;
            return false;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            // A Scope may be disposed by ResourcePlan/Consumer before the provider-transition
            // service takes its live-scope snapshot. Its merged request still has to remain
            // observable until the completion continuation has removed pending.
            if (pending.Count > 0)
                ESAssets.TrackDisposedPendingScope(this);
            NotifyLifetimeReleased();
            ReleaseEntries();
            ESAssets.UnregisterScope(this);
        }

        /// <summary>
        /// Framework lifecycle hook. A dependent subsystem can return its own child ownership
        /// when this Scope ends; it never receives an asset handle and cannot dispose this Scope.
        /// </summary>
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public void RegisterLifetimeReleaseListener(Action listener)
        {
            if (listener == null) return;
            if (disposed) { listener.Invoke(); return; }
            lifetimeReleaseListeners.Add(listener);
        }

        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public void UnregisterLifetimeReleaseListener(Action listener)
        {
            if (listener != null)
                lifetimeReleaseListeners.Remove(listener);
        }

        internal void InvalidateAtSafePoint()
        {
            if (disposed) return;
            if (pending.Count > 0) throw new InvalidOperationException("仍有资源请求进行中，不能执行资源安全点卸载。");
            ReleaseEntries();
        }

        internal bool HasPendingOperations => pending.Count > 0;
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public bool IsDisposed => disposed;

        private void ReleaseEntries()
        {
            foreach (Entry entry in entries.Values)
                entry.Lease?.Dispose();
            entries.Clear();
        }

        private void NotifyLifetimeReleased()
        {
            if (lifetimeReleaseListeners.Count == 0) return;
            var listeners = new Action[lifetimeReleaseListeners.Count];
            lifetimeReleaseListeners.CopyTo(listeners);
            lifetimeReleaseListeners.Clear();
            for (int i = 0; i < listeners.Length; i++)
            {
                try { listeners[i]?.Invoke(); }
                catch (Exception exception) { Debug.LogException(exception); }
            }
        }
    }

    /// <summary>挂在开发者传入的 Component/GameObject 上，销毁时自动释放其 Scope。</summary>
    [DisallowMultipleComponent]
    public sealed class ESAssetOwnerTracker : MonoBehaviour
    {
        private ESAssetScope scope;
        internal ESAssetScope GetScope()
        {
            if (scope == null || scope.IsDisposed)
            {
                IESAssetRuntimeProvider provider = ESAssets.RuntimeBackend;
                if (!ESAssets.IsReady || provider == null) throw new InvalidOperationException("ESRuntimeDataAssetLoadingService 尚未初始化或正在切换 Provider。");
                scope = new ESAssetScope(provider);
            }
            return scope;
        }
        private void OnDestroy() => scope?.Dispose();
    }

    /// <summary>业务层入口：默认 Owner 自动释放；Scope 仅用于批量/非 Unity Owner 的高级场景。</summary>
    public static partial class ESAssets
    {
        private static readonly HashSet<ESAssetScope> liveScopes = new HashSet<ESAssetScope>();
        // Active ResourcePlans publish the assets their internal Scope already owns here.
        // Gameplay reads this index without becoming another owner; the Plan remains the
        // sole authority until its lifecycle actually ends.
        private sealed class PlannedAssetEntry
        {
            public UnityEngine.Object Asset;
            public int PlanCount;
        }
        private static readonly Dictionary<ESAssetIdentity, PlannedAssetEntry> activePlanAssets = new Dictionary<ESAssetIdentity, PlannedAssetEntry>();
        // Provider transition disposes scopes to prevent them from accepting a new request,
        // but a merged load already issued with CancellationToken.None can still be completing.
        // Keep those scopes observable until their pending map drains; otherwise a table reset
        // could race a completion continuation that still belongs to the old provider.
        private static readonly HashSet<ESAssetScope> transitionScopes = new HashSet<ESAssetScope>();
        private static ESAssetScope residentScope;
        private static IESAssetRuntimeProvider runtimeProvider;
        private static bool providerTransitioning;
        private static bool runtimeBackendRebuiltDuringTransition;
        private static int runtimeBackendGeneration;

        /// <summary>Framework-only notification used by lifecycle binders after a Provider replacement.</summary>
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static event Action RuntimeBackendRebuilt;
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static int RuntimeBackendGeneration => runtimeBackendGeneration;

        internal static void RegisterScope(ESAssetScope scope) { if (scope != null) liveScopes.Add(scope); }
        internal static void UnregisterScope(ESAssetScope scope) { if (scope != null) liveScopes.Remove(scope); }
        /// <summary>框架内部的当前运行时加载后端；业务代码只使用 ESAssets/ResourcePlan。</summary>
        internal static IESAssetRuntimeProvider RuntimeBackend => runtimeProvider;
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static void AttachRuntimeBackend(IESAssetRuntimeProvider provider)
        {
            runtimeProvider = provider ?? throw new ArgumentNullException(nameof(provider));
            runtimeBackendRebuiltDuringTransition = true;
        }
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static void DetachRuntimeBackend(IESAssetRuntimeProvider provider)
        {
            if (ReferenceEquals(runtimeProvider, provider))
                runtimeProvider = null;
        }
        internal static void TrackDisposedPendingScope(ESAssetScope scope)
        {
            if (scope != null && scope.HasPendingOperations)
                transitionScopes.Add(scope);
        }

        /// <summary>当前 AssetTable Resolver 与底层 Provider 均已完成装配。</summary>
        public static bool IsReady => !providerTransitioning && runtimeProvider != null;

        /// <summary>当前 Provider 或任一 Scope 是否仍有在途资源请求。</summary>
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static bool HasPendingOperations
        {
            get
            {
                IESAssetRuntimeProvider provider = runtimeProvider;
                return (provider is IESRuntimeAssetOperationTracker tracker && tracker.HasPendingOperations)
                    || HasPendingScopeOperations();
            }
        }

        /// <summary>Provider 重建开始：立即阻止新业务请求进入旧 Provider。</summary>
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static void BeginProviderTransition() => providerTransitioning = true;

        /// <summary>释放所有旧 Provider Scope；OwnerTracker 会在下一次访问时自动创建新 Scope。</summary>
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static void ResetScopesForProviderTransition()
        {
            var scopes = new List<ESAssetScope>(liveScopes);
            for (int i = 0; i < scopes.Count; i++)
            {
                ESAssetScope scope = scopes[i];
                if (scope == null)
                    continue;
                TrackDisposedPendingScope(scope);
                scope.Dispose();
            }
            liveScopes.Clear();
            residentScope = null;
        }

        /// <summary>新 Resolver/Provider 装配完成后恢复业务加载。</summary>
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static void EndProviderTransition()
        {
            providerTransitioning = false;
            if (!runtimeBackendRebuiltDuringTransition || runtimeProvider == null)
                return;

            runtimeBackendRebuiltDuringTransition = false;
            runtimeBackendGeneration++;
            try { RuntimeBackendRebuilt?.Invoke(); }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        public static UniTask WaitUntilReadyAsync(CancellationToken cancellationToken = default)
            => UniTask.WaitUntil(() => IsReady, cancellationToken: cancellationToken);

        private static ESAssetScope GetResidentScope()
        {
            if (!IsReady)
                throw new InvalidOperationException("ESRuntimeDataAssetLoadingService 尚未初始化或正在切换 Provider。");
            if (residentScope == null)
                residentScope = CreateScope();
            return residentScope;
        }

        /// <summary>
        /// 默认、无显式持有业务入口：调用者不创建计数、不持有 Scope、也不需要 Release。
        /// 框架在内部常驻域按资产身份去重持有，直到显式安全点卸载；这不是取消
        /// RuntimeBackend 的保护计数，而是把计数责任完全收敛到底层。
        /// 角色技能、UI 与普通业务均可直接使用。
        /// </summary>
        public static UniTask<T> LoadAsync<T>(ESAssetRefer<T> refer, CancellationToken cancellationToken = default) where T : UnityEngine.Object
        {
            if (refer != null && TryGetActivePlanAsset(refer.AssetIdentity, out T planned))
                return UniTask.FromResult(planned);
            return GetResidentScope().LoadAsync(refer, cancellationToken);
        }

        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static ESAssetScope CreateScope()
        {
            IESAssetRuntimeProvider provider = runtimeProvider;
            if (!IsReady || provider == null) throw new InvalidOperationException("ESRuntimeDataAssetLoadingService 尚未初始化或正在切换 Provider。");
            return new ESAssetScope(provider);
        }

        /// <summary>对象生命周期入口：调用者只给出 Owner，Scope 与释放由框架自动管理。</summary>
        public static UniTask<T> LoadAsync<T>(ESAssetRefer<T> refer, Component owner, CancellationToken cancellationToken = default) where T : UnityEngine.Object
        {
            if (owner == null) return UniTask.FromException<T>(new ArgumentNullException(nameof(owner)));
            if (refer != null && TryGetActivePlanAsset(refer.AssetIdentity, out T planned))
                return UniTask.FromResult(planned);
            ESAssetOwnerTracker tracker = owner.GetComponent<ESAssetOwnerTracker>();
            if (tracker == null) tracker = owner.gameObject.AddComponent<ESAssetOwnerTracker>();
            return tracker.GetScope().LoadAsync(refer, cancellationToken);
        }

        /// <summary>ResourcePlan internal bridge: records an asset already protected by a Plan Scope.</summary>
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static void RegisterActivePlanAsset(ESAssetIdentity identity, UnityEngine.Object asset)
        {
            if (!identity.IsValid || asset == null)
                return;
            if (!activePlanAssets.TryGetValue(identity, out PlannedAssetEntry entry))
                activePlanAssets.Add(identity, entry = new PlannedAssetEntry { Asset = asset, PlanCount = 1 });
            else
            {
                entry.Asset = asset;
                entry.PlanCount++;
            }
        }

        /// <summary>ResourcePlan internal bridge: removes one Plan ownership from the fast-path index.</summary>
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static void UnregisterActivePlanAsset(ESAssetIdentity identity)
        {
            if (!activePlanAssets.TryGetValue(identity, out PlannedAssetEntry entry))
                return;
            if (--entry.PlanCount <= 0)
                activePlanAssets.Remove(identity);
        }

        internal static bool TryGetActivePlanAsset<T>(ESAssetIdentity identity, out T asset) where T : UnityEngine.Object
        {
            if (activePlanAssets.TryGetValue(identity, out PlannedAssetEntry entry)
                && entry.Asset is T typed && typed != null)
            {
                asset = typed;
                return true;
            }
            asset = null;
            return false;
        }

        /// <summary>
        /// 同步兼容入口。只允许在 Bootstrap、退出进程等已确认完全静默的阶段调用；
        /// 常规切换流程应使用 UnloadAllAtSafePointAsync。
        /// </summary>
        public static void UnloadAllAtSafePoint()
        {
            IESAssetRuntimeProvider provider = runtimeProvider;
            if (provider == null) throw new InvalidOperationException("ESRuntimeDataAssetLoadingService 尚未初始化。");
            if ((provider is IESRuntimeAssetOperationTracker tracker && tracker.HasPendingOperations) || HasPendingScopeOperations())
                throw new InvalidOperationException("[ESRes][SafePoint] 同步全量卸载只能在完全静默阶段调用；当前仍有资源请求，请改用 UnloadAllAtSafePointAsync。");
            foreach (ESAssetScope scope in liveScopes) scope.InvalidateAtSafePoint();
            residentScope?.Dispose();
            residentScope = null;
            // Direct full safe point invalidates every Scope. A caller that bypasses the
            // ResourcePlan coordinator must not leave a stale Plan fast-path entry behind.
            activePlanAssets.Clear();
            provider.UnloadAllAtSafePoint();
        }

        /// <summary>等待 Provider 与全部 Scope 的在途请求收尾后执行全量安全点卸载。</summary>
        public static async UniTask UnloadAllAtSafePointAsync(CancellationToken cancellationToken = default)
        {
            await WaitForPendingOperationsAsync(cancellationToken);
            UnloadAllAtSafePoint();
        }

        /// <summary>框架生命周期服务在清理 AssetTable 前使用。</summary>
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static async UniTask WaitForPendingOperationsAsync(CancellationToken cancellationToken = default)
        {
            IESAssetRuntimeProvider provider = runtimeProvider;
            if (provider == null) throw new InvalidOperationException("ESRuntimeDataAssetLoadingService 尚未初始化。");
            if (provider is IESRuntimeAssetOperationTracker tracker)
                await tracker.WaitForPendingOperationsAsync(cancellationToken);
            await UniTask.WaitUntil(() => !HasPendingScopeOperations(), cancellationToken: cancellationToken);
            transitionScopes.Clear();
        }

        private static bool HasPendingScopeOperations()
        {
            foreach (ESAssetScope scope in liveScopes)
                if (scope != null && scope.HasPendingOperations)
                    return true;

            if (transitionScopes.Count == 0)
                return false;

            var completed = new List<ESAssetScope>();
            foreach (ESAssetScope scope in transitionScopes)
            {
                if (scope != null && scope.HasPendingOperations)
                    return true;
                completed.Add(scope);
            }
            for (int i = 0; i < completed.Count; i++)
                transitionScopes.Remove(completed[i]);
            return false;
        }

        /// <summary>验证/内存整理用：仅清扫计数为 0 的对象缓存，不卸载 AB。</summary>
        public static async UniTask<ESRuntimeUnusedAssetUnloadResult> UnloadZeroReferenceAssetsAsync(CancellationToken cancellationToken = default)
        {
            IESAssetRuntimeProvider provider = runtimeProvider;
            if (provider == null)
                throw new InvalidOperationException("ESRuntimeDataAssetLoadingService 尚未初始化。");
            if (provider is IESRuntimeAssetOperationTracker tracker)
                await tracker.WaitForPendingOperationsAsync(cancellationToken);
            return await provider.UnloadZeroReferenceAssetsAsync(cancellationToken);
        }

        /// <summary>关卡切换后的增量安全点。仅卸载已无 Scope/Handle 租约的 AB。</summary>
        public static async UniTask<ESRuntimeUnusedAssetBundleUnloadResult> UnloadReleasedAssetBundlesAtSafePointAsync(CancellationToken cancellationToken = default)
        {
            IESAssetRuntimeProvider provider = runtimeProvider;
            if (provider == null)
                throw new InvalidOperationException("ESRuntimeDataAssetLoadingService 尚未初始化。");
            if (provider is IESRuntimeAssetOperationTracker tracker)
                await tracker.WaitForPendingOperationsAsync(cancellationToken);
            return await provider.UnloadZeroReferenceAssetBundlesAtSafePointAsync(cancellationToken);
        }
    }
}
