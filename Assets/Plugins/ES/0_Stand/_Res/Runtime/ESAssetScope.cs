using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace ES
{
    /// <summary>一次资源持有。仅框架创建；Dispose 幂等。</summary>
    public sealed class ESAssetLease<T> : IDisposable where T : UnityEngine.Object
    {
        private Action release;
        public T Asset { get; }
        internal ESAssetLease(T asset, Action releaseAction) { Asset = asset; release = releaseAction; }
        public void Dispose() { Action action = release; release = null; action?.Invoke(); }
    }

    /// <summary>
    /// 一个逻辑 Owner 的资源域。同一 Scope 内同一身份最多持有一次，技能反复引用不会重复计数。
    /// Scope Dispose 只结束 Owner 记录，不会在游戏中卸载资源；AB 仅由安全点统一卸载。
    /// </summary>
    public sealed class ESAssetScope : IDisposable
    {
        private sealed class Entry { public UnityEngine.Object Asset; public IDisposable Lease; }
        private readonly IESAssetRuntimeProvider provider;
        private readonly Dictionary<ESAssetIdentity, Entry> entries = new Dictionary<ESAssetIdentity, Entry>();
        private readonly Dictionary<ESAssetIdentity, UniTaskCompletionSource<UnityEngine.Object>> pending = new Dictionary<ESAssetIdentity, UniTaskCompletionSource<UnityEngine.Object>>();
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

        private static async UniTask<T> AwaitPendingAsync<T>(UniTaskCompletionSource<UnityEngine.Object> waiting, CancellationToken cancellationToken) where T : UnityEngine.Object
        {
            UnityEngine.Object result = await waiting.Task.AttachExternalCancellation(cancellationToken);
            if (result is T typed) return typed;
            throw new InvalidCastException("[ESRes][Load] 同一 ESAssetIdentity 被请求为不兼容类型：" + typeof(T).Name);
        }

        private async UniTask LoadNewCoreAsync<T>(ESAssetRefer<T> refer, ESAssetIdentity id, UniTaskCompletionSource<UnityEngine.Object> completion) where T : UnityEngine.Object
        {
            try
            {
                // A merged operation must not inherit the first waiter's cancellation.
                ESAssetLease<T> lease = await refer.AcquireAsync(provider, CancellationToken.None);
                if (disposed) { lease.Dispose(); throw new ObjectDisposedException(nameof(ESAssetScope)); }
                entries.Add(id, new Entry { Asset = lease.Asset, Lease = lease });
                completion.TrySetResult(lease.Asset);
            }
            catch (Exception exception) { completion.TrySetException(exception); }
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
            ReleaseEntries();
            ESAssets.UnregisterScope(this);
        }

        internal void InvalidateAtSafePoint()
        {
            if (disposed) return;
            if (pending.Count > 0) throw new InvalidOperationException("仍有资源请求进行中，不能执行资源安全点卸载。");
            ReleaseEntries();
        }

        private void ReleaseEntries()
        {
            foreach (Entry entry in entries.Values)
                entry.Lease?.Dispose();
            entries.Clear();
        }
    }

    /// <summary>挂在开发者传入的 Component/GameObject 上，销毁时自动释放其 Scope。</summary>
    [DisallowMultipleComponent]
    public sealed class ESAssetOwnerTracker : MonoBehaviour
    {
        private ESAssetScope scope;
        internal ESAssetScope GetScope()
        {
            if (scope == null)
            {
                IESAssetRuntimeProvider provider = ESAssetReferTableResolver.Current?.RuntimeProvider;
                if (provider == null) throw new InvalidOperationException("ESRuntimeDataAssetLoadingService 尚未初始化。");
                scope = new ESAssetScope(provider);
            }
            return scope;
        }
        private void OnDestroy() => scope?.Dispose();
    }

    /// <summary>业务层入口：默认 Owner 自动释放；Scope 仅用于批量/非 Unity Owner 的高级场景。</summary>
    public static class ESAssets
    {
        private static readonly HashSet<ESAssetScope> liveScopes = new HashSet<ESAssetScope>();
        private static ESAssetScope residentScope;

        internal static void RegisterScope(ESAssetScope scope) { if (scope != null) liveScopes.Add(scope); }
        internal static void UnregisterScope(ESAssetScope scope) { if (scope != null) liveScopes.Remove(scope); }

        private static ESAssetScope GetResidentScope()
        {
            if (residentScope == null)
                residentScope = CreateScope();
            return residentScope;
        }

        /// <summary>
        /// 默认、零计数业务入口：资源进入全局驻留缓存，直到显式安全点卸载。
        /// 角色技能、UI 与普通业务均可直接使用，不需要 Owner、Scope 或 Release。
        /// </summary>
        public static UniTask<T> LoadAsync<T>(ESAssetRefer<T> refer, CancellationToken cancellationToken = default) where T : UnityEngine.Object
            => GetResidentScope().LoadAsync(refer, cancellationToken);

        public static ESAssetScope CreateScope()
        {
            IESAssetRuntimeProvider provider = ESAssetReferTableResolver.Current?.RuntimeProvider;
            if (provider == null) throw new InvalidOperationException("ESRuntimeDataAssetLoadingService 尚未初始化。");
            return new ESAssetScope(provider);
        }

        public static UniTask<T> LoadAsync<T>(ESAssetRefer<T> refer, Component owner, CancellationToken cancellationToken = default) where T : UnityEngine.Object
        {
            if (owner == null) return UniTask.FromException<T>(new ArgumentNullException(nameof(owner)));
            ESAssetOwnerTracker tracker = owner.GetComponent<ESAssetOwnerTracker>();
            if (tracker == null) tracker = owner.gameObject.AddComponent<ESAssetOwnerTracker>();
            return tracker.GetScope().LoadAsync(refer, cancellationToken);
        }

        /// <summary>仅在开发者定义的安全点调用；不会由 Owner 生命周期隐式触发。</summary>
        public static void UnloadAllAtSafePoint()
        {
            IESAssetRuntimeProvider provider = ESAssetReferTableResolver.Current?.RuntimeProvider;
            if (provider == null) throw new InvalidOperationException("ESRuntimeDataAssetLoadingService 尚未初始化。");
            foreach (ESAssetScope scope in liveScopes) scope.InvalidateAtSafePoint();
            residentScope?.Dispose();
            residentScope = null;
            provider.UnloadAllAtSafePoint();
        }

        /// <summary>验证/内存整理用：仅清扫计数为 0 的对象缓存，不卸载 AB。</summary>
        public static UniTask<ESRuntimeUnusedAssetUnloadResult> UnloadZeroReferenceAssetsAsync(CancellationToken cancellationToken = default)
        {
            IESAssetRuntimeProvider provider = ESAssetReferTableResolver.Current?.RuntimeProvider;
            if (provider == null)
                return UniTask.FromException<ESRuntimeUnusedAssetUnloadResult>(new InvalidOperationException("ESRuntimeDataAssetLoadingService 尚未初始化。"));
            return provider.UnloadZeroReferenceAssetsAsync(cancellationToken);
        }
    }
}
