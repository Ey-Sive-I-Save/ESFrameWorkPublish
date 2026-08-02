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

        /// <summary>
        /// Scope 外壳会被调用方长期保存，不能直接回池，否则旧引用可能命中新 Owner，形成 ABA 串线。
        /// 这里只池化占主要分配的容器状态；外壳 Dispose 后永久失效。
        /// </summary>
        private sealed class PooledState : IPoolable
        {
            public readonly Dictionary<ESAssetIdentity, Entry> Entries = new Dictionary<ESAssetIdentity, Entry>();
            public readonly Dictionary<ESAssetIdentity, UniTaskCompletionSource<UnityEngine.Object>> Pending = new Dictionary<ESAssetIdentity, UniTaskCompletionSource<UnityEngine.Object>>();
            public readonly HashSet<Action> LifetimeReleaseListeners = new HashSet<Action>();

            public bool IsRecycled { get; set; }

            public void OnResetAsPoolable()
            {
                if (Pending.Count != 0)
                    throw new InvalidOperationException("[ESRes][ScopePool] 仍有在途请求的 Scope 状态不能回池。");
                Entries.Clear();
                LifetimeReleaseListeners.Clear();
            }
        }

        private static readonly ESSimplePool<PooledState> StatePool = new ESSimplePool<PooledState>(
            () => new PooledState(),
            initCount: 0,
            maxCount: 128,
            poolDisplayName: "ESAssetScope State",
            groupName: "ES Resource");

        private readonly IESAssetRuntimeProvider provider;
        private PooledState pooledState;
        private Dictionary<ESAssetIdentity, Entry> entries => pooledState.Entries;
        private Dictionary<ESAssetIdentity, UniTaskCompletionSource<UnityEngine.Object>> pending => pooledState.Pending;
        private HashSet<Action> lifetimeReleaseListeners => pooledState.LifetimeReleaseListeners;
        private bool disposed;

        internal ESAssetScope(IESAssetRuntimeProvider runtimeProvider)
        {
            provider = runtimeProvider ?? throw new ArgumentNullException(nameof(runtimeProvider));
            pooledState = StatePool.GetInPool();
            ESAssets.RegisterScope(this);
        }

        internal static int PooledStateCount => StatePool.CurCount;

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
            finally
            {
                pending.Remove(id);
                TryReturnPooledState();
            }
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
            finally
            {
                pending.Remove(id);
                TryReturnPooledState();
            }
        }

        public bool TryGet<T>(ESAssetRefer<T> refer, out T asset) where T : UnityEngine.Object
        {
            if (!disposed && refer != null && entries.TryGetValue(refer.AssetIdentity, out Entry entry) && entry.Asset is T typed) { asset = typed; return true; }
            asset = null;
            return false;
        }

        /// <summary>
        /// Framework-only read bridge for a caller that was explicitly handed this Owner Scope.
        /// It neither loads an asset nor changes the Scope's ownership; callers must stop using
        /// the borrowed asset when this Scope begins disposal.
        /// </summary>
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public bool TryGetResolved<T>(ESAssetIdentity identity, out T asset) where T : UnityEngine.Object
        {
            if (!disposed && identity.IsValid && entries.TryGetValue(identity, out Entry entry) && entry.Asset is T typed)
            {
                asset = typed;
                return true;
            }

            asset = null;
            return false;
        }

        internal bool Release(ESAssetIdentity identity)
        {
            if (disposed || pooledState == null || !entries.TryGetValue(identity, out Entry entry))
                return false;
            entries.Remove(identity);
            entry.Lease?.Dispose();
            return true;
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
            ESAssets.NotifyScopeOwnershipEnding(this);
            NotifyLifetimeReleased();
            ReleaseEntries();
            ESAssets.UnregisterScope(this);
            TryReturnPooledState();
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
            if (listener != null && pooledState != null)
                lifetimeReleaseListeners.Remove(listener);
        }

        internal void InvalidateAtSafePoint()
        {
            if (disposed) return;
            if (pending.Count > 0) throw new InvalidOperationException("仍有资源请求进行中，不能执行资源安全点卸载。");
            ReleaseEntries();
        }

        internal bool HasPendingOperations => pooledState != null && pending.Count > 0;
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public bool IsDisposed => disposed;

        private void TryReturnPooledState()
        {
            if (!disposed || pooledState == null || pending.Count != 0)
                return;

            PooledState state = pooledState;
            pooledState = null;
            ESAssets.UntrackDisposedPendingScope(this);
            StatePool.PushToPool(state);
        }

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

    /// <summary>
    /// 全局复用的短期任务资源域。每次 LoadAsync 都取得一次逻辑持有，Release 归还一次；
    /// 相同身份的并发请求共享底层加载和单份 Scope 持有，不为每次任务创建 Scope/Handle 包装对象。
    /// 若调用方需要“本次加载独立幂等释放”，应使用 LoadAsyncLease，而不是按身份 Release。
    /// </summary>
    public sealed class ESAssetTemporaryScope : IDisposable
    {
        private readonly struct LeaseRecord
        {
            public readonly ESAssetIdentity Identity;
            public readonly int Generation;
            public LeaseRecord(ESAssetIdentity identity, int generation) { Identity = identity; Generation = generation; }
        }

        private sealed class State
        {
            public readonly Type AssetType;
            public readonly UniTaskCompletionSource<UnityEngine.Object> Completion = new UniTaskCompletionSource<UnityEngine.Object>();
            public int ReferenceCount;
            public int LeaseCount;
            public UnityEngine.Object Asset;
            public bool Completed;

            public State(Type assetType) { AssetType = assetType; }
        }

        private readonly ESAssetScope scope;
        private readonly Dictionary<ESAssetIdentity, State> states = new Dictionary<ESAssetIdentity, State>(16);
        private readonly Dictionary<long, LeaseRecord> leases = new Dictionary<long, LeaseRecord>(16);
        private long nextLeaseToken;
        private int generation = 1;
        private bool disposed;

        internal ESAssetTemporaryScope(IESAssetRuntimeProvider provider)
        {
            scope = new ESAssetScope(provider);
        }

        public bool IsDisposed => disposed;

        public UniTask<T> LoadAsync<T>(ESAssetRefer<T> refer, CancellationToken cancellationToken = default)
            where T : UnityEngine.Object
        {
            if (disposed)
                return UniTask.FromException<T>(new ObjectDisposedException(nameof(ESAssetTemporaryScope)));
            if (refer == null)
                return UniTask.FromException<T>(new ArgumentNullException(nameof(refer)));
            if (!refer.IsValid)
                return UniTask.FromException<T>(new InvalidOperationException("[ESRes][TemporaryScope] ESAssetRefer 缺少有效资产身份。"));

            ESAssetIdentity identity = refer.AssetIdentity;
            if (states.TryGetValue(identity, out State state))
            {
                if (state.AssetType != typeof(T))
                    return UniTask.FromException<T>(new InvalidCastException(
                        "[ESRes][TemporaryScope] 同一 ESAssetIdentity 被请求为不兼容类型："
                        + state.AssetType.Name + " -> " + typeof(T).Name));
                state.ReferenceCount++;
                if (state.Completed && state.Asset is T ready)
                    return UniTask.FromResult(ready);
                return AwaitRetainedAsync<T>(identity, state, false, cancellationToken);
            }

            state = new State(typeof(T)) { ReferenceCount = 1 };
            states.Add(identity, state);
            LoadCoreAsync(refer, identity, state).Forget();
            return AwaitRetainedAsync<T>(identity, state, false, cancellationToken);
        }

        /// <summary>
        /// 严格的一次性租期入口。每次成功调用返回独立 Token；重复 Dispose 只会使该 Token 第一次生效，
        /// 不会影响同一资产的其他调用者。需要短期独立释放时优先使用此入口。
        /// </summary>
        public async UniTask<ESAssetTemporaryLease<T>> LoadAsyncLease<T>(
            ESAssetRefer<T> refer,
            CancellationToken cancellationToken = default)
            where T : UnityEngine.Object
        {
            await LoadLeaseAssetAsync(refer, cancellationToken);
            if (disposed || refer == null || !states.TryGetValue(refer.AssetIdentity, out State state) || state.LeaseCount <= 0)
                throw new ObjectDisposedException(nameof(ESAssetTemporaryScope));

            long token = NextLeaseToken();
            leases[token] = new LeaseRecord(refer.AssetIdentity, generation);
            return new ESAssetTemporaryLease<T>(this, token);
        }

        private UniTask<T> LoadLeaseAssetAsync<T>(ESAssetRefer<T> refer, CancellationToken cancellationToken)
            where T : UnityEngine.Object
        {
            if (disposed)
                return UniTask.FromException<T>(new ObjectDisposedException(nameof(ESAssetTemporaryScope)));
            if (refer == null)
                return UniTask.FromException<T>(new ArgumentNullException(nameof(refer)));
            if (!refer.IsValid)
                return UniTask.FromException<T>(new InvalidOperationException("[ESRes][TemporaryScope] ESAssetRefer 缺少有效资产身份。"));

            ESAssetIdentity identity = refer.AssetIdentity;
            if (states.TryGetValue(identity, out State state))
            {
                if (state.AssetType != typeof(T))
                    return UniTask.FromException<T>(new InvalidCastException(
                        "[ESRes][TemporaryScope] 同一 ESAssetIdentity 被请求为不兼容类型："
                        + state.AssetType.Name + " -> " + typeof(T).Name));
                state.LeaseCount++;
                if (state.Completed && state.Asset is T ready)
                    return UniTask.FromResult(ready);
                return AwaitRetainedAsync<T>(identity, state, true, cancellationToken);
            }

            state = new State(typeof(T)) { LeaseCount = 1 };
            states.Add(identity, state);
            LoadCoreAsync(refer, identity, state).Forget();
            return AwaitRetainedAsync<T>(identity, state, true, cancellationToken);
        }

        /// <summary>按资产身份归还一次引用计数；调用方必须与成功的 LoadAsync 一一配对。</summary>
        public bool Release<T>(ESAssetRefer<T> refer) where T : UnityEngine.Object
            => refer != null && Release(refer.AssetIdentity);

        public bool TryGet<T>(ESAssetRefer<T> refer, out T asset) where T : UnityEngine.Object
        {
            if (!disposed && refer != null && states.TryGetValue(refer.AssetIdentity, out State state)
                && state.Completed && state.Asset is T typed)
            {
                asset = typed;
                return true;
            }
            asset = null;
            return false;
        }

        private async UniTask<T> AwaitRetainedAsync<T>(
            ESAssetIdentity identity,
            State state,
            bool lease,
            CancellationToken cancellationToken)
            where T : UnityEngine.Object
        {
            try
            {
                UnityEngine.Object asset = await state.Completion.Task.AttachExternalCancellation(cancellationToken);
                if (asset is T typed)
                    return typed;
                throw new InvalidCastException("[ESRes][TemporaryScope] 已加载资产类型不兼容：" + typeof(T).Name);
            }
            catch
            {
                if (lease) ReleaseLease(identity);
                else Release(identity);
                throw;
            }
        }

        private async UniTask LoadCoreAsync<T>(ESAssetRefer<T> refer, ESAssetIdentity identity, State state)
            where T : UnityEngine.Object
        {
            try
            {
                T asset = await scope.LoadAsync(refer, CancellationToken.None);
                state.Asset = asset;
                state.Completed = true;
                state.Completion.TrySetResult(asset);
                if (state.ReferenceCount + state.LeaseCount <= 0 || disposed)
                {
                    scope.Release(identity);
                    if (states.TryGetValue(identity, out State current) && ReferenceEquals(current, state))
                        states.Remove(identity);
                }
            }
            catch (Exception exception)
            {
                if (states.TryGetValue(identity, out State current) && ReferenceEquals(current, state))
                    states.Remove(identity);
                state.Completion.TrySetException(exception);
            }
        }

        private bool Release(ESAssetIdentity identity)
        {
            if (disposed || !states.TryGetValue(identity, out State state) || state.ReferenceCount <= 0)
                return false;
            state.ReferenceCount--;
            TryFinalize(identity, state);
            return true;
        }

        private bool ReleaseLease(ESAssetIdentity identity)
        {
            if (disposed || !states.TryGetValue(identity, out State state) || state.LeaseCount <= 0)
                return false;
            state.LeaseCount--;
            TryFinalize(identity, state);
            return true;
        }

        private void TryFinalize(ESAssetIdentity identity, State state)
        {
            if (state.ReferenceCount > 0 || state.LeaseCount > 0 || !state.Completed)
                return;
            if (states.TryGetValue(identity, out State current) && ReferenceEquals(current, state))
            {
                states.Remove(identity);
                scope.Release(identity);
            }
        }

        internal bool ReleaseToken(long token)
        {
            if (!leases.TryGetValue(token, out LeaseRecord record) || record.Generation != generation)
                return false;
            leases.Remove(token);
            return ReleaseLease(record.Identity);
        }

        internal bool TryGetTokenAsset<T>(long token, out T asset) where T : UnityEngine.Object
        {
            if (!disposed && leases.TryGetValue(token, out LeaseRecord record) && record.Generation == generation
                && states.TryGetValue(record.Identity, out State state) && state.Asset is T typed)
            {
                asset = typed;
                return true;
            }
            asset = null;
            return false;
        }

        internal void InvalidateAtSafePoint()
        {
            generation = generation == int.MaxValue ? 1 : generation + 1;
            leases.Clear();
            states.Clear();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            generation = generation == int.MaxValue ? 1 : generation + 1;
            leases.Clear();
            states.Clear();
            scope.Dispose();
        }

        private long NextLeaseToken()
        {
            long token;
            do
            {
                token = ++nextLeaseToken;
                if (token == 0)
                    token = ++nextLeaseToken;
            }
            while (leases.ContainsKey(token));
            return token;
        }
    }

    /// <summary>
    /// 临时任务域的一次性租期 Token。它是轻量值类型；复制品共享同一个整数 Token，
    /// 因此无论 Dispose 多少次，底层持有都只会归还一次。
    /// </summary>
    public readonly struct ESAssetTemporaryLease<T> : IDisposable where T : UnityEngine.Object
    {
        private readonly ESAssetTemporaryScope scope;
        private readonly long token;

        internal ESAssetTemporaryLease(ESAssetTemporaryScope scope, long token)
        {
            this.scope = scope;
            this.token = token;
        }

        public T Asset
        {
            get
            {
                if (scope != null && scope.TryGetTokenAsset(token, out T asset))
                    return asset;
                return null;
            }
        }

        public bool IsValid => Asset != null;

        public void Dispose() => scope?.ReleaseToken(token);
    }

    /// <summary>挂在开发者传入的 Component/GameObject 上，销毁时自动释放其 Scope。</summary>
    [DisallowMultipleComponent]
    public sealed class ESAssetOwnerTracker : MonoBehaviour
    {
        private ESAssetScope scope;

        internal bool TryGetScope(out ESAssetScope result)
        {
            if (scope != null && !scope.IsDisposed)
            {
                result = scope;
                return true;
            }

            result = null;
            return false;
        }

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
        private static ESAssetTemporaryScope temporaryScope;
        private static IESAssetRuntimeProvider runtimeProvider;
        private static bool providerTransitioning;
        private static bool runtimeBackendRebuiltDuringTransition;
        private static int runtimeBackendGeneration;

        /// <summary>Framework-only notification used by lifecycle binders after a Provider replacement.</summary>
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static event Action RuntimeBackendTransitionStarting;
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static event Action RuntimeBackendRebuilt;
        /// <summary>
        /// ResourcePlan-only lifecycle edge raised synchronously before the final Plan owner stops
        /// publishing an asset. Borrowers may stop work that still uses the asset, but never gain
        /// a retain, load, or release right from this notification.
        /// </summary>
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static event Action<ESAssetIdentity> ActivePlanAssetOwnershipEnding;
        /// <summary>
        /// Framework-only lifecycle edge raised before an Owner Scope returns its loaded assets.
        /// Borrowers may only stop their own work; the event never transfers a retain or permits
        /// a new load.
        /// </summary>
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static event Action<ESAssetScope> ScopeOwnershipEnding;
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static int RuntimeBackendGeneration => runtimeBackendGeneration;

        internal static void RegisterScope(ESAssetScope scope) { if (scope != null) liveScopes.Add(scope); }
        internal static void UnregisterScope(ESAssetScope scope) { if (scope != null) liveScopes.Remove(scope); }
        internal static void NotifyScopeOwnershipEnding(ESAssetScope scope)
        {
            if (scope == null)
                return;
            try { ScopeOwnershipEnding?.Invoke(scope); }
            catch (Exception exception) { Debug.LogException(exception); }
        }
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
        internal static void UntrackDisposedPendingScope(ESAssetScope scope)
        {
            if (scope != null)
                transitionScopes.Remove(scope);
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
        public static void BeginProviderTransition()
        {
            if (providerTransitioning)
                return;

            providerTransitioning = true;
            try { RuntimeBackendTransitionStarting?.Invoke(); }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        /// <summary>释放所有旧 Provider Scope；OwnerTracker 会在下一次访问时自动创建新 Scope。</summary>
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static void ResetScopesForProviderTransition()
        {
            temporaryScope?.Dispose();
            temporaryScope = null;
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
        /// 全局高速临时任务域。调用方应捕获本次取得的 Scope，并用同一个实例 Release；
        /// Provider 切换会废弃旧实例，避免旧任务归还新一代资源。
        /// </summary>
        public static ESAssetTemporaryScope TemporaryScope
        {
            get
            {
                IESAssetRuntimeProvider provider = runtimeProvider;
                if (!IsReady || provider == null)
                    throw new InvalidOperationException("ESRuntimeDataAssetLoadingService 尚未初始化或正在切换 Provider。");
                if (temporaryScope == null || temporaryScope.IsDisposed)
                    temporaryScope = new ESAssetTemporaryScope(provider);
                return temporaryScope;
            }
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

        /// <summary>
        /// 对象生命周期入口：调用者只给出 Owner，Scope 与释放由框架自动管理。
        /// 此入口必须建立 Owner 自己的独立持有；即使活动 ResourcePlan 已经加载同一资产，
        /// 也只允许由 Provider 缓存复用物理对象，不得把 Plan 借用伪装成 Owner 所有权。
        /// </summary>
        public static UniTask<T> LoadAsync<T>(ESAssetRefer<T> refer, Component owner, CancellationToken cancellationToken = default) where T : UnityEngine.Object
        {
            if (owner == null) return UniTask.FromException<T>(new ArgumentNullException(nameof(owner)));
            ESAssetOwnerTracker tracker = owner.GetComponent<ESAssetOwnerTracker>();
            if (tracker == null) tracker = owner.gameObject.AddComponent<ESAssetOwnerTracker>();
            return tracker.GetScope().LoadAsync(refer, cancellationToken);
        }

        /// <summary>
        /// Owner 热路径查询：只返回该 Owner 现有 Scope 已经持有的资产。
        /// 查询失败不会创建 Tracker、Scope、Provider 请求或新的资源所有权。
        /// </summary>
        public static bool TryGetOwned<T>(ESAssetRefer<T> refer, Component owner, out T asset) where T : UnityEngine.Object
        {
            asset = null;
            if (refer == null || owner == null || !refer.IsValid)
                return false;

            ESAssetOwnerTracker tracker = owner.GetComponent<ESAssetOwnerTracker>();
            return tracker != null
                && tracker.TryGetScope(out ESAssetScope scope)
                && scope.TryGetResolved(refer.AssetIdentity, out asset);
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
            if (--entry.PlanCount > 0)
                return;

            // A Plan owns this asset until its internal Scope is disposed. Notify borrowers before
            // removing the final fast-path entry so they can end their own runtime work without
            // inventing a second resource retain or allowing a Voice to outlive the Plan.
            NotifyActivePlanAssetOwnershipEnding(identity);
            activePlanAssets.Remove(identity);
        }

        /// <summary>
        /// Read-only ResourcePlan fast path. A successful result is borrowed from an active Plan;
        /// this call never loads an asset or changes ownership. Runtime consumers must end their
        /// work before <see cref="ActivePlanAssetOwnershipEnding"/> for the same identity completes.
        /// </summary>
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static bool TryGetActivePlanAsset<T>(ESAssetIdentity identity, out T asset) where T : UnityEngine.Object
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
            temporaryScope?.InvalidateAtSafePoint();
            foreach (ESAssetScope scope in liveScopes) scope.InvalidateAtSafePoint();
            residentScope?.Dispose();
            residentScope = null;
            // Direct full safe point invalidates every Scope. A caller that bypasses the
            // ResourcePlan coordinator must not leave a stale Plan fast-path entry behind, and
            // borrowed runtime work must end before the index disappears.
            if (activePlanAssets.Count > 0)
            {
                var activeIdentities = new ESAssetIdentity[activePlanAssets.Count];
                activePlanAssets.Keys.CopyTo(activeIdentities, 0);
                for (int i = 0; i < activeIdentities.Length; i++)
                    NotifyActivePlanAssetOwnershipEnding(activeIdentities[i]);
            }
            activePlanAssets.Clear();
            provider.UnloadAllAtSafePoint();
        }

        private static void NotifyActivePlanAssetOwnershipEnding(ESAssetIdentity identity)
        {
            try { ActivePlanAssetOwnershipEnding?.Invoke(identity); }
            catch (Exception exception) { Debug.LogException(exception); }
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
