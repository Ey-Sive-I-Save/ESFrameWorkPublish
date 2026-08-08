using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ES
{
    /// <summary>Provider 与 Scope 之间的内部一次性释放状态；不向业务层暴露 Handle。</summary>
    internal interface IESRuntimeAssetLease : IDisposable { }

    public enum ESRuntimeAssetLoadState : byte { None, Resolving, LoadingDependencies, LoadingAssetBundle, LoadingAsset, Ready, Failed, Released }

    public readonly struct ESAssetIdentity : IEquatable<ESAssetIdentity>
    {
        public readonly string Guid;
        public readonly long LocalFileId;
        public ESAssetIdentity(string guid, long localFileId = 0) { Guid = guid ?? string.Empty; LocalFileId = localFileId; }
        public bool IsValid => !string.IsNullOrEmpty(Guid) && LocalFileId >= 0;
        public bool IsSubAsset => LocalFileId != 0;
        public bool Equals(ESAssetIdentity other) => LocalFileId == other.LocalFileId && string.Equals(Guid, other.Guid, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is ESAssetIdentity other && Equals(other);
        public override int GetHashCode() => (StringComparer.Ordinal.GetHashCode(Guid ?? string.Empty) * 397) ^ LocalFileId.GetHashCode();
        public override string ToString() => IsSubAsset ? Guid + ":" + LocalFileId : Guid;
    }

    public readonly struct ESRuntimeAssetLoadStatus
    {
        public readonly ESAssetIdentity AssetId;
        public readonly ESRuntimeAssetLoadState State;
        public readonly float Progress;
        public readonly int Attempt;
        public readonly string Message;
        public ESRuntimeAssetLoadStatus(ESAssetIdentity id, ESRuntimeAssetLoadState state, float progress, int attempt, string message)
        { AssetId = id; State = state; Progress = progress; Attempt = attempt; Message = message; }
    }

    /// <summary>零引用对象缓存清扫结果。AssetBundle 不在此操作中卸载。</summary>
    public readonly struct ESRuntimeUnusedAssetUnloadResult
    {
        public readonly int EvictedCachedAssetCount;
        public readonly bool NativeUnloadRequested;
        public ESRuntimeUnusedAssetUnloadResult(int evictedCachedAssetCount, bool nativeUnloadRequested)
        {
            EvictedCachedAssetCount = evictedCachedAssetCount;
            NativeUnloadRequested = nativeUnloadRequested;
        }
    }

    /// <summary>资源安全点的增量清理结果。仅卸载没有活动租约的 AssetBundle。</summary>
    public readonly struct ESRuntimeUnusedAssetBundleUnloadResult
    {
        public readonly int UnloadedAssetBundleCount;
        public readonly int EvictedCachedAssetCount;
        public ESRuntimeUnusedAssetBundleUnloadResult(int unloadedAssetBundleCount, int evictedCachedAssetCount)
        {
            UnloadedAssetBundleCount = unloadedAssetBundleCount;
            EvictedCachedAssetCount = evictedCachedAssetCount;
        }
    }

    [Serializable]
    public struct ESRuntimeRetryPolicy
    {
        [Min(1)] public int MaxAttempts;
        [Min(0)] public float InitialDelaySeconds;
        [Min(1f)] public float BackoffMultiplier;
        public static ESRuntimeRetryPolicy Default => new ESRuntimeRetryPolicy { MaxAttempts = 3, InitialDelaySeconds = .25f, BackoffMultiplier = 2f };
    }

    public interface IESRuntimeAssetBundleProvider
    {
        UniTask<AssetBundle> LoadAssetBundleAsync(ESRuntimeAssetBundleRecord record, IProgress<float> progress, CancellationToken cancellationToken);
    }

    /// <summary>
    /// 业务层唯一依赖的资源获取接口。实现可来自 EditorDirect 或 AssetBundle 链路；
    /// AssetBundle 的下载、依赖和缓存均是实现细节。
    /// </summary>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public interface IESAssetRuntimeProvider : IDisposable
    {
        UniTask<ESRuntimeAssetHandle<T>> LoadMainAssetAsync<T>(ESAssetIdentity id, CancellationToken cancellationToken = default) where T : UnityEngine.Object;
        UniTask<ESRuntimeAssetHandle<T>> LoadSubAssetAsync<T>(ESAssetIdentity id, CancellationToken cancellationToken = default) where T : UnityEngine.Object;
        UniTask<ESRuntimeSceneHandle> LoadSceneAsync(ESAssetIdentity id, LoadSceneMode mode = LoadSceneMode.Single, CancellationToken cancellationToken = default);
        bool TryGetLoaded<T>(ESAssetIdentity id, out T asset) where T : UnityEngine.Object;
        bool TryGetStatus(ESAssetIdentity id, out ESRuntimeAssetLoadStatus status);
        void Release(ESAssetIdentity id);
        UniTask<ESRuntimeUnusedAssetUnloadResult> UnloadZeroReferenceAssetsAsync(CancellationToken cancellationToken = default);
        UniTask<ESRuntimeUnusedAssetBundleUnloadResult> UnloadZeroReferenceAssetBundlesAtSafePointAsync(CancellationToken cancellationToken = default);
        void UnloadAllAtSafePoint();
    }

    public interface IESRuntimeDirectAssetProvider
    {
        UniTask<UnityEngine.Object> LoadAsync(ESAssetIdentity id, CancellationToken cancellationToken);
        UniTask<Scene> LoadSceneAsync(ESAssetIdentity id, LoadSceneMode mode, CancellationToken cancellationToken);
    }

    /// <summary>安全点内部使用的可选能力，不扩展公开 Provider 契约。</summary>
    internal interface IESRuntimeAssetOperationTracker
    {
        bool HasPendingOperations { get; }
        UniTask WaitForPendingOperationsAsync(CancellationToken cancellationToken = default);
    }

    internal interface IESRuntimeAssetUnloadDiagnostics
    {
        bool HasUnloadFailure { get; }
        int UnloadFailureCount { get; }
        string LastUnloadError { get; }
    }

    /// <summary>GUID/GUID+LocalFileId 到物理加载位置的运行时加载器。RuntimeKey 不参与全局寻址。</summary>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public sealed class ESRuntimeAssetLoader : IESAssetRuntimeProvider, IESRuntimeAssetOperationTracker, IESRuntimeAssetUnloadDiagnostics
    {
        private sealed class SceneLoadCommit
        {
            private readonly object sync = new object();
            private bool waiterCanceled;
            private byte ownershipState;

            public bool WaiterCanceled
            {
                get { lock (sync) return waiterCanceled; }
            }

            public void CancelWaiter()
            {
                lock (sync) waiterCanceled = true;
            }

            public bool TryCommitOwnership()
            {
                lock (sync)
                {
                    if (waiterCanceled || ownershipState != 0)
                        return false;
                    ownershipState = 1;
                    return true;
                }
            }

            public bool TryReleaseOwnership()
            {
                lock (sync)
                {
                    if (ownershipState != 1)
                        return false;
                    ownershipState = 2;
                    return true;
                }
            }
        }

        private sealed class AssetBundleLease
        {
            public AssetBundle Bundle;
            public int RefCount;
            public UniTaskCompletionSource<AssetBundle> InFlight;
        }

        private readonly ESGlobalAssetRuntimeMap runtimeMap;
        private readonly IESRuntimeAssetBundleProvider assetBundleProvider;
        private readonly IESRuntimeDirectAssetProvider directAssetProvider;
        private readonly bool requireRuntimeMapIdentity;
        private readonly ESRuntimeRetryPolicy retry;
        private readonly Dictionary<string, AssetBundleLease> assetBundles = new Dictionary<string, AssetBundleLease>(StringComparer.Ordinal);
        private readonly Dictionary<ESAssetIdentity, UnityEngine.Object> cachedObjects = new Dictionary<ESAssetIdentity, UnityEngine.Object>();
        private readonly Dictionary<ESAssetIdentity, int> objectRefCounts = new Dictionary<ESAssetIdentity, int>();
        private readonly Dictionary<ESAssetIdentity, UniTaskCompletionSource<UnityEngine.Object>> loadingObjects = new Dictionary<ESAssetIdentity, UniTaskCompletionSource<UnityEngine.Object>>();
        private readonly Dictionary<ESAssetIdentity, ESRuntimeAssetLoadStatus> statusById = new Dictionary<ESAssetIdentity, ESRuntimeAssetLoadStatus>();
        private int pendingSceneOperations;
        private bool disposed;
        private bool disposeWhenIdle;
        private int unloadFailureCount;
        private string lastUnloadError = string.Empty;

        public event Action<ESRuntimeAssetLoadStatus> StatusChanged;

        public bool HasUnloadFailure => unloadFailureCount > 0;
        public int UnloadFailureCount => unloadFailureCount;
        public string LastUnloadError => lastUnloadError ?? string.Empty;

        public bool HasPendingOperations
        {
            get
            {
                if (loadingObjects.Count > 0) return true;
                if (pendingSceneOperations > 0) return true;
                foreach (AssetBundleLease lease in assetBundles.Values)
                    if (lease.InFlight != null) return true;
                return false;
            }
        }

        public UniTask WaitForPendingOperationsAsync(CancellationToken cancellationToken = default)
            => UniTask.WaitUntil(() => !HasPendingOperations, cancellationToken: cancellationToken);

        public ESRuntimeAssetLoader(ESGlobalAssetRuntimeMap globalRuntimeMap, IESRuntimeAssetBundleProvider provider, ESRuntimeRetryPolicy retryPolicy, IESRuntimeDirectAssetProvider directProvider = null, bool requireRuntimeMapIdentity = false)
        {
            runtimeMap = globalRuntimeMap ? globalRuntimeMap : throw new ArgumentNullException(nameof(globalRuntimeMap), "[ESRes][RuntimeMap] Global Runtime Map 不能为空。");
            if (provider == null && directProvider == null) throw new ArgumentNullException(nameof(provider), "[ESRes][Load] AssetBundle Provider 与 Direct Provider 不能同时为空。");
            assetBundleProvider = provider;
            directAssetProvider = directProvider;
            this.requireRuntimeMapIdentity = requireRuntimeMapIdentity;
            retry = retryPolicy.MaxAttempts > 0 ? retryPolicy : ESRuntimeRetryPolicy.Default;
            runtimeMap.RebuildRuntimeIndex();
        }

        public bool TryGetLoaded<T>(string guid, out T asset) where T : UnityEngine.Object => TryGetLoaded(new ESAssetIdentity(guid), out asset);
        public bool TryGetLoaded<T>(ESSubAssetId id, out T asset) where T : UnityEngine.Object => TryGetLoaded(new ESAssetIdentity(id.Guid, id.LocalFileId), out asset);

        public bool TryGetLoaded<T>(ESAssetIdentity id, out T asset) where T : UnityEngine.Object
        {
            if (cachedObjects.TryGetValue(id, out var value) && value is T typed) { asset = typed; return true; }
            asset = null;
            return false;
        }

        public bool TryGetStatus(ESAssetIdentity id, out ESRuntimeAssetLoadStatus status) => statusById.TryGetValue(id, out status);

        public async UniTask<ESRuntimeAssetHandle<T>> LoadAsync<T>(string guid, CancellationToken cancellationToken = default) where T : UnityEngine.Object
            => await LoadMainAssetAsync<T>(new ESAssetIdentity(guid), cancellationToken);

        public async UniTask<ESRuntimeAssetHandle<T>> LoadMainAssetAsync<T>(ESAssetIdentity id, CancellationToken cancellationToken = default) where T : UnityEngine.Object
        {
            ThrowIfDisposed();
            if (directAssetProvider != null)
            {
                if (requireRuntimeMapIdentity && !runtimeMap.TryGetMainAsset(id.Guid, out _))
                    throw new KeyNotFoundException($"[ESRes][SimulateBuild] 主资产 GUID 未登记：GUID={id.Guid}");
                return await LoadDirectAsync<T>(id, cancellationToken);
            }
            if (id.IsSubAsset) throw new ArgumentException("[ESRes][Load] 子资产身份必须使用 LoadSubAssetAsync。", nameof(id));
            if (!runtimeMap.TryGetMainAsset(id.Guid, out var record)) throw new KeyNotFoundException($"[ESRes][Load] 主资产 GUID 未登记：GUID={id.Guid}");
            await AcquireAssetBundleTreeAsync(record.AssetBundleKey, id, cancellationToken);
            try
            {
                var asset = await GetOrLoadAssetAsync<T>(id, record.AssetBundleKey, record.InternalName, cancellationToken);
                return new ESRuntimeAssetHandle<T>(this, id, record.AssetBundleKey, asset);
            }
            catch { ReleaseAssetBundleTree(record.AssetBundleKey); throw; }
        }

        public async UniTask<ESRuntimeAssetHandle<T>> LoadSubAssetAsync<T>(ESSubAssetId subAssetId, CancellationToken cancellationToken = default) where T : UnityEngine.Object
            => await LoadSubAssetAsync<T>(new ESAssetIdentity(subAssetId.Guid, subAssetId.LocalFileId), cancellationToken);

        public async UniTask<ESRuntimeAssetHandle<T>> LoadSubAssetAsync<T>(ESAssetIdentity id, CancellationToken cancellationToken = default) where T : UnityEngine.Object
        {
            ThrowIfDisposed();
            if (!id.IsSubAsset) throw new ArgumentException("[ESRes][SubAsset] 主资产身份必须使用 LoadMainAssetAsync。", nameof(id));
            if (directAssetProvider != null)
            {
                if (requireRuntimeMapIdentity && (!runtimeMap.TryGetSubAsset(new ESSubAssetId(id.Guid, id.LocalFileId), out _)))
                    throw new KeyNotFoundException($"[ESRes][SimulateBuild] 子资产身份未登记：GUID={id.Guid}, LocalFileId={id.LocalFileId}");
                return await LoadDirectAsync<T>(id, cancellationToken);
            }
            var subAssetId = new ESSubAssetId(id.Guid, id.LocalFileId);
            if (!runtimeMap.TryGetSubAsset(subAssetId, out var sub) || string.IsNullOrEmpty(sub.AssetBundleKey))
                throw new KeyNotFoundException($"[ESRes][SubAsset] 子资产身份或 BundleKey 未登记：GUID={subAssetId.Guid}, LocalFileId={subAssetId.LocalFileId}");
            await AcquireAssetBundleTreeAsync(sub.AssetBundleKey, id, cancellationToken);
            try
            {
                var asset = await GetOrLoadSubAssetAsync<T>(id, sub.AssetBundleKey, sub.InternalName, sub.Selector, sub.TypeName, cancellationToken);
                return new ESRuntimeAssetHandle<T>(this, id, sub.AssetBundleKey, asset);
            }
            catch { ReleaseAssetBundleTree(sub.AssetBundleKey); throw; }
        }

        public async UniTask<ESRuntimeSceneHandle> LoadSceneAsync(ESAssetIdentity id, LoadSceneMode mode = LoadSceneMode.Single, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (id.IsSubAsset) throw new ArgumentException("[ESRes][Load] Scene 不能使用子资产身份。", nameof(id));
            if (directAssetProvider != null)
            {
                if (requireRuntimeMapIdentity && !runtimeMap.TryGetMainAsset(id.Guid, out _))
                    throw new KeyNotFoundException($"[ESRes][SimulateBuild] 场景 GUID 未登记：GUID={id.Guid}");
                // Unity's AsyncOperation cannot be stopped once created. The core request
                // therefore continues with CancellationToken.None while the caller may cancel
                // only its wait. Keeping this request tracked prevents a Provider transition
                // from disposing the old runtime while Unity is still entering the scene.
                cancellationToken.ThrowIfCancellationRequested();
                pendingSceneOperations++;
                var completion = new UniTaskCompletionSource<Scene>();
                var commit = new SceneLoadCommit();
                LoadDirectSceneCoreAsync(id, mode, cancellationToken, directAssetProvider, completion, commit).Forget();
                Scene directScene;
                try
                {
                    directScene = await completion.Task.AttachExternalCancellation(cancellationToken);
                }
                catch
                {
                    commit.CancelWaiter();
                    if (commit.TryReleaseOwnership())
                        ReleaseLoaded(id, string.Empty);
                    throw;
                }
                if (cancellationToken.IsCancellationRequested)
                {
                    commit.CancelWaiter();
                    if (commit.TryReleaseOwnership())
                        ReleaseLoaded(id, string.Empty);
                    throw new OperationCanceledException(cancellationToken);
                }
                return new ESRuntimeSceneHandle(this, id, string.Empty, directScene);
            }
            if (!runtimeMap.TryGetMainAsset(id.Guid, out ESRuntimeAssetRecord record)) throw new KeyNotFoundException($"[ESRes][Load] Scene GUID 未登记：GUID={id.Guid}");
            cancellationToken.ThrowIfCancellationRequested();
            pendingSceneOperations++;
            var sceneCompletion = new UniTaskCompletionSource<Scene>();
            var sceneCommit = new SceneLoadCommit();
            LoadBundleSceneCoreAsync(id, record, mode, cancellationToken, sceneCompletion, sceneCommit).Forget();
            Scene loadedScene;
            try
            {
                loadedScene = await sceneCompletion.Task.AttachExternalCancellation(cancellationToken);
            }
            catch
            {
                sceneCommit.CancelWaiter();
                if (sceneCommit.TryReleaseOwnership())
                    ReleaseLoaded(id, record.AssetBundleKey);
                throw;
            }
            if (cancellationToken.IsCancellationRequested)
            {
                sceneCommit.CancelWaiter();
                if (sceneCommit.TryReleaseOwnership())
                    ReleaseLoaded(id, record.AssetBundleKey);
                throw new OperationCanceledException(cancellationToken);
            }
            return new ESRuntimeSceneHandle(this, id, record.AssetBundleKey, loadedScene);
        }

        private async UniTask LoadDirectSceneCoreAsync(
            ESAssetIdentity id,
            LoadSceneMode mode,
            CancellationToken requestCancellation,
            IESRuntimeDirectAssetProvider provider,
            UniTaskCompletionSource<Scene> completion,
            SceneLoadCommit commit)
        {
            try
            {
                Scene scene = await provider.LoadSceneAsync(id, mode, CancellationToken.None);
                if (requestCancellation.IsCancellationRequested || commit.WaiterCanceled)
                {
                    if (mode == LoadSceneMode.Additive && scene.isLoaded)
                    {
                        AsyncOperation unload = SceneManager.UnloadSceneAsync(scene);
                        if (unload == null)
                            throw new InvalidOperationException("[ESRes][Scene] 取消后的 Additive 场景补偿卸载无法启动，场景仍保持加载：GUID=" + id.Guid);
                        await unload.ToUniTask();
                    }
                    else if (mode == LoadSceneMode.Single)
                    {
                        Debug.LogWarning("[ESRes][Scene] 取消请求发生在 Single 场景已开始加载之后；Unity 无法回滚已进入的场景。" +
                            "调用方等待已取消，但当前场景仍由场景生命周期管理。GUID=" + id.Guid);
                    }
                    completion.TrySetException(new OperationCanceledException(requestCancellation));
                    return;
                }
                RetainObject(id);
                if (!commit.TryCommitOwnership())
                {
                    ReleaseLoaded(id, string.Empty);
                    completion.TrySetException(new OperationCanceledException(requestCancellation));
                    return;
                }
                completion.TrySetResult(scene);
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }
            finally
            {
                pendingSceneOperations--;
                TryFinalizeDeferredDispose();
            }
        }

        private async UniTask LoadBundleSceneCoreAsync(
            ESAssetIdentity id,
            ESRuntimeAssetRecord record,
            LoadSceneMode mode,
            CancellationToken requestCancellation,
            UniTaskCompletionSource<Scene> completion,
            SceneLoadCommit commit)
        {
            bool bundleTreeAcquired = false;
            try
            {
                await AcquireAssetBundleTreeAsync(record.AssetBundleKey, id, CancellationToken.None);
                bundleTreeAcquired = true;
                if (requestCancellation.IsCancellationRequested || commit.WaiterCanceled)
                {
                    ReleaseAssetBundleTree(record.AssetBundleKey);
                    completion.TrySetException(new OperationCanceledException(requestCancellation));
                    return;
                }

                if (!assetBundles.TryGetValue(record.AssetBundleKey, out AssetBundleLease lease) || lease.Bundle == null)
                    throw new InvalidOperationException($"[ESRes][Load] Scene 所属 AssetBundle 未加载：BundleKey={record.AssetBundleKey}, GUID={id.Guid}");
                Report(id, ESRuntimeAssetLoadState.LoadingAsset, .9f, 0, record.InternalName);
                // AssetBundle 只负责将场景数据载入内存；真正触发场景切换的是 SceneManager。
                // The Unity operation itself is intentionally awaited without the caller token.
                // Cancellation can stop the wait, never the Unity operation.
                AsyncOperation operation = SceneManager.LoadSceneAsync(record.InternalName, mode);
                if (operation == null) throw new InvalidOperationException($"[ESRes][Load] AssetBundle 中未找到 Scene：GUID={id.Guid}, BundleKey={record.AssetBundleKey}, InternalName={record.InternalName}");
                await operation.ToUniTask();
                Scene scene = SceneManager.GetSceneByPath(record.InternalName);
                if (!scene.IsValid()) throw new InvalidOperationException($"[ESRes][Load] 已加载 Scene 无效：GUID={id.Guid}, BundleKey={record.AssetBundleKey}, InternalName={record.InternalName}");

                if (requestCancellation.IsCancellationRequested || commit.WaiterCanceled)
                {
                    // Additive scenes can be compensated after Unity finishes. A Single load
                    // has already replaced the active scene and cannot be rolled back safely;
                    // report that fact explicitly instead of pretending the load was canceled.
                    if (mode == LoadSceneMode.Additive && scene.isLoaded)
                    {
                        AsyncOperation unload = SceneManager.UnloadSceneAsync(scene);
                        if (unload == null)
                            throw new InvalidOperationException("[ESRes][Scene] 取消后的 Additive 场景补偿卸载无法启动，场景仍保持加载：GUID=" + id.Guid);
                        await unload.ToUniTask();
                    }
                    else if (mode == LoadSceneMode.Single)
                    {
                        Debug.LogWarning("[ESRes][Scene] 取消请求发生在 Single 场景已开始加载之后；Unity 无法回滚已进入的场景。" +
                            "调用方等待已取消，但当前场景仍由场景生命周期管理。GUID=" + id.Guid);
                    }
                    ReleaseAssetBundleTree(record.AssetBundleKey);
                    bundleTreeAcquired = false;
                    completion.TrySetException(new OperationCanceledException(requestCancellation));
                    return;
                }

                Report(id, ESRuntimeAssetLoadState.Ready, 1f, 0, null);
                RetainObject(id);
                if (!commit.TryCommitOwnership())
                {
                    ReleaseLoaded(id, record.AssetBundleKey);
                    bundleTreeAcquired = false;
                    completion.TrySetException(new OperationCanceledException(requestCancellation));
                    return;
                }
                completion.TrySetResult(scene);
                bundleTreeAcquired = false; // ownership transfers to ESRuntimeSceneHandle
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }
            finally
            {
                if (bundleTreeAcquired)
                    ReleaseAssetBundleTree(record.AssetBundleKey);
                pendingSceneOperations--;
                TryFinalizeDeferredDispose();
            }
        }

        public void Release(ESAssetIdentity id)
        {
            if (directAssetProvider != null) { ReleaseLoaded(id, string.Empty); return; }
            if (id.IsSubAsset)
            {
                if (runtimeMap.TryGetSubAsset(new ESSubAssetId(id.Guid, id.LocalFileId), out var sub))
                    ReleaseLoaded(id, sub.AssetBundleKey);
                return;
            }
            if (runtimeMap.TryGetMainAsset(id.Guid, out var main)) ReleaseLoaded(id, main.AssetBundleKey);
        }

        /// <summary>
        /// 唯一允许卸载驻留 Bundle 的入口。只能在切场景、退回登录或明确的资源安全点调用。
        /// 运行中普通 Release 不应触发此操作。
        /// </summary>
        public void UnloadAllAtSafePoint()
        {
            ThrowIfDisposed();
            if (loadingObjects.Count > 0) throw new InvalidOperationException("[ESRes][Load] 仍有资产请求进行中，不能执行资源安全点卸载。");
            foreach (AssetBundleLease lease in assetBundles.Values)
            {
                if (lease.InFlight != null) throw new InvalidOperationException("[ESRes][Load] 仍有 AssetBundle 请求进行中，不能执行资源安全点卸载。");
            }

            var failures = new List<Exception>();
            var unloadedKeys = new List<string>();
            foreach (var pair in assetBundles)
                if (TryUnloadAssetBundle(pair.Key, pair.Value.Bundle, failures))
                    unloadedKeys.Add(pair.Key);

            for (int i = 0; i < unloadedKeys.Count; i++)
                assetBundles.Remove(unloadedKeys[i]);
            if (failures.Count > 0)
            {
                // A partial safe-point unload is not a usable Provider state. Keep the
                // failed leases for diagnosis, but block future loads instead of silently
                // exposing a cache whose Bundle ownership is no longer trustworthy.
                disposed = true;
                cachedObjects.Clear();
                objectRefCounts.Clear();
                loadingObjects.Clear();
                ThrowUnloadFailures("全量资源安全点卸载", failures);
            }

            assetBundles.Clear();
            cachedObjects.Clear();
            objectRefCounts.Clear();
            statusById.Clear();
        }

        internal void ReleaseLoaded(ESAssetIdentity id, string assetBundleKey)
        {
            if (!objectRefCounts.TryGetValue(id, out var count)) return;
            if (count <= 0) return;
            count--;
            objectRefCounts[id] = count;

            // Every successful handle acquisition retains the owning bundle tree too.
            // Keep that lease accounting symmetrical even though ordinary releases retain
            // the loaded bundle until the explicit resource safe point.
            if (!string.IsNullOrEmpty(assetBundleKey)) ReleaseAssetBundleTree(assetBundleKey);

            if (count > 0) return;
            // 运行中保持 AB 驻留；零引用对象先留在缓存中，交由显式异步清扫或资源安全点处理。
            Report(id, ESRuntimeAssetLoadState.Released, 0f, 0, null);
        }

        /// <summary>
        /// 清除逻辑引用数为 0 的 C# 对象缓存，并异步请求 Unity 回收真正无人引用的原生对象。
        /// 不会卸载任何 AssetBundle；Unity 若发现仍有原生引用，可保留对应对象，这是正确行为。
        /// </summary>
        public async UniTask<ESRuntimeUnusedAssetUnloadResult> UnloadZeroReferenceAssetsAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (loadingObjects.Count > 0) throw new InvalidOperationException("[ESRes][Load] 仍有资产请求进行中，不能清扫零引用资产。");

            List<ESAssetIdentity> unusedIds = null;
            foreach (var pair in objectRefCounts)
            {
                if (pair.Value > 0) continue;
                if (unusedIds == null) unusedIds = new List<ESAssetIdentity>();
                unusedIds.Add(pair.Key);
            }

            int evicted = unusedIds?.Count ?? 0;
            if (unusedIds != null)
            {
                for (int i = 0; i < unusedIds.Count; i++)
                {
                    ESAssetIdentity id = unusedIds[i];
                    objectRefCounts.Remove(id);
                    cachedObjects.Remove(id);
                    Report(id, ESRuntimeAssetLoadState.Released, 0f, 0, "Zero-reference cache evicted");
                }
            }

            if (evicted == 0) return new ESRuntimeUnusedAssetUnloadResult(0, false);
            AsyncOperation operation = Resources.UnloadUnusedAssets();
            await operation.ToUniTask(cancellationToken: cancellationToken);
            return new ESRuntimeUnusedAssetUnloadResult(evicted, true);
        }

        /// <summary>
        /// 关卡切换后的增量安全点：只处理没有活动 Handle/Scope 租约的 AB。
        /// 调用者必须先销毁旧关卡实例并 Release 旧 ResourcePlan；绝不在普通游戏帧调用。
        /// </summary>
        public async UniTask<ESRuntimeUnusedAssetBundleUnloadResult> UnloadZeroReferenceAssetBundlesAtSafePointAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (loadingObjects.Count > 0) throw new InvalidOperationException("[ESRes][Load] 仍有资产请求进行中，不能执行增量资源安全点卸载。");
            foreach (AssetBundleLease lease in assetBundles.Values)
                if (lease.InFlight != null) throw new InvalidOperationException("[ESRes][Load] 仍有 AssetBundle 请求进行中，不能执行增量资源安全点卸载。");

            int evicted = 0;
            List<ESAssetIdentity> unusedAssets = null;
            foreach (var pair in objectRefCounts)
            {
                if (pair.Value > 0) continue;
                if (unusedAssets == null) unusedAssets = new List<ESAssetIdentity>();
                unusedAssets.Add(pair.Key);
            }
            if (unusedAssets != null)
            {
                evicted = unusedAssets.Count;
                for (int i = 0; i < unusedAssets.Count; i++)
                {
                    ESAssetIdentity id = unusedAssets[i];
                    objectRefCounts.Remove(id);
                    cachedObjects.Remove(id);
                    Report(id, ESRuntimeAssetLoadState.Released, 0f, 0, "Zero-reference level cache evicted");
                }
            }

            int unloaded = 0;
            var failures = new List<Exception>();
            List<string> unusedBundles = null;
            foreach (var pair in assetBundles)
            {
                if (pair.Value.RefCount != 0) continue;
                if (unusedBundles == null) unusedBundles = new List<string>();
                unusedBundles.Add(pair.Key);
            }
            if (unusedBundles != null)
                for (int i = 0; i < unusedBundles.Count; i++)
                {
                    AssetBundleLease lease = assetBundles[unusedBundles[i]];
                    if (!TryUnloadAssetBundle(unusedBundles[i], lease.Bundle, failures))
                        continue;
                    assetBundles.Remove(unusedBundles[i]);
                    unloaded++;
                }

            if (evicted > 0 || unloaded > 0)
                await Resources.UnloadUnusedAssets().ToUniTask(cancellationToken: cancellationToken);
            if (failures.Count > 0)
            {
                disposed = true;
                cachedObjects.Clear();
                objectRefCounts.Clear();
                ThrowUnloadFailures("增量资源安全点卸载", failures);
            }
            return new ESRuntimeUnusedAssetBundleUnloadResult(unloaded, evicted);
        }

        private async UniTask<T> GetOrLoadAssetAsync<T>(ESAssetIdentity id, string assetBundleKey, string internalName, CancellationToken token) where T : UnityEngine.Object
        {
            if (cachedObjects.TryGetValue(id, out var cached))
            {
                if (!(cached is T typed)) throw new InvalidCastException($"[ESRes][Load] 缓存资产类型不匹配：AssetId={id}, ActualType={cached.GetType().FullName}, RequestedType={typeof(T).FullName}");
                RetainObject(id); return typed;
            }
            if (loadingObjects.TryGetValue(id, out var waiting))
            {
                var loaded = await waiting.Task.AttachExternalCancellation(token);
                if (!(loaded is T typed)) throw new InvalidCastException($"[ESRes][Load] 并发加载资产类型不匹配：AssetId={id}, ActualType={loaded.GetType().FullName}, RequestedType={typeof(T).FullName}");
                RetainObject(id); return typed;
            }
            if (!assetBundles.TryGetValue(assetBundleKey, out var lease) || lease.Bundle == null) throw new InvalidOperationException($"[ESRes][Load] AssetBundle 未加载：BundleKey={assetBundleKey}, AssetId={id}");

            var completion = new UniTaskCompletionSource<UnityEngine.Object>();
            loadingObjects.Add(id, completion);
            try
            {
                Report(id, ESRuntimeAssetLoadState.LoadingAsset, .9f, 0, internalName);
                var request = lease.Bundle.LoadAssetAsync<T>(internalName);
                await request.ToUniTask();
                if (!(request.asset is T asset)) throw new InvalidOperationException($"[ESRes][Load] 资产不存在或类型不匹配：AssetId={id}, BundleKey={assetBundleKey}, InternalName={internalName}, RequestedType={typeof(T).FullName}");
                cachedObjects.Add(id, asset); objectRefCounts.Add(id, 1); completion.TrySetResult(asset);
                Report(id, ESRuntimeAssetLoadState.Ready, 1f, 0, null);
                return asset;
            }
            catch (Exception e) { completion.TrySetException(e); Report(id, ESRuntimeAssetLoadState.Failed, 0f, 0, e.Message); throw; }
            finally
            {
                loadingObjects.Remove(id);
                TryFinalizeDeferredDispose();
            }
        }

        private async UniTask<T> GetOrLoadSubAssetAsync<T>(ESAssetIdentity id, string assetBundleKey, string internalName, string selector, string expectedTypeName, CancellationToken token) where T : UnityEngine.Object
        {
            if (cachedObjects.TryGetValue(id, out var cached))
            {
                if (!(cached is T typed)) throw new InvalidCastException($"[ESRes][SubAsset] 缓存子资产类型不匹配：GUID={id.Guid}, LocalFileId={id.LocalFileId}, ActualType={cached.GetType().FullName}, RequestedType={typeof(T).FullName}");
                RetainObject(id);
                return typed;
            }
            if (loadingObjects.TryGetValue(id, out var waiting))
            {
                var loaded = await waiting.Task.AttachExternalCancellation(token);
                if (!(loaded is T typed)) throw new InvalidCastException($"[ESRes][SubAsset] 并发加载子资产类型不匹配：GUID={id.Guid}, LocalFileId={id.LocalFileId}, ActualType={loaded.GetType().FullName}, RequestedType={typeof(T).FullName}");
                RetainObject(id);
                return typed;
            }
            if (string.IsNullOrEmpty(selector) || string.IsNullOrEmpty(expectedTypeName))
                throw new InvalidOperationException($"[ESRes][SubAsset] 子资产选择信息缺失：GUID={id.Guid}, LocalFileId={id.LocalFileId}, BundleKey={assetBundleKey}, InternalName={internalName}, Selector={selector}, Type={expectedTypeName}");
            if (!assetBundles.TryGetValue(assetBundleKey, out var lease) || lease.Bundle == null)
                throw new InvalidOperationException($"[ESRes][SubAsset] 子资产所属 AssetBundle 未加载：GUID={id.Guid}, LocalFileId={id.LocalFileId}, BundleKey={assetBundleKey}");

            var completion = new UniTaskCompletionSource<UnityEngine.Object>();
            loadingObjects.Add(id, completion);
            try
            {
                Report(id, ESRuntimeAssetLoadState.LoadingAsset, .9f, 0, internalName + "/" + selector);
                var request = lease.Bundle.LoadAssetWithSubAssetsAsync<UnityEngine.Object>(internalName);
                await request.ToUniTask(cancellationToken: token);
                T selected = null;
                var matches = 0;
                foreach (var candidate in request.allAssets)
                {
                    if (!(candidate is T typed) || !string.Equals(typed.name, selector, StringComparison.Ordinal)
                        || !string.Equals(typed.GetType().FullName, expectedTypeName, StringComparison.Ordinal)) continue;
                    selected = typed;
                    matches++;
                }
                if (matches != 1)
                    throw new InvalidOperationException($"[ESRes][SubAsset] 子资产选择必须唯一命中：GUID={id.Guid}, LocalFileId={id.LocalFileId}, BundleKey={assetBundleKey}, InternalName={internalName}, Selector={selector}, ManifestType={expectedTypeName}, RequestedType={typeof(T).FullName}, MatchCount={matches}");
                cachedObjects.Add(id, selected);
                objectRefCounts.Add(id, 1);
                completion.TrySetResult(selected);
                Report(id, ESRuntimeAssetLoadState.Ready, 1f, 0, null);
                return selected;
            }
            catch (Exception e) { completion.TrySetException(e); Report(id, ESRuntimeAssetLoadState.Failed, 0f, 0, e.Message); throw; }
            finally
            {
                loadingObjects.Remove(id);
                TryFinalizeDeferredDispose();
            }
        }

        private void RetainObject(ESAssetIdentity id) { objectRefCounts.TryGetValue(id, out var count); objectRefCounts[id] = count + 1; }

        private async UniTask<ESRuntimeAssetHandle<T>> LoadDirectAsync<T>(ESAssetIdentity id, CancellationToken token) where T : UnityEngine.Object
        {
            if (cachedObjects.TryGetValue(id, out var cached))
            {
                if (!(cached is T typed)) throw new InvalidCastException($"[ESRes][Load] Direct 缓存资产类型不匹配：AssetId={id}, ActualType={cached.GetType().FullName}, RequestedType={typeof(T).FullName}");
                RetainObject(id);
                return new ESRuntimeAssetHandle<T>(this, id, string.Empty, typed);
            }
            if (!loadingObjects.TryGetValue(id, out UniTaskCompletionSource<UnityEngine.Object> completion))
            {
                completion = new UniTaskCompletionSource<UnityEngine.Object>();
                loadingObjects.Add(id, completion);
                LoadDirectCoreAsync(id, completion).Forget();
            }
            UnityEngine.Object loaded = await completion.Task.AttachExternalCancellation(token);
            if (!(loaded is T asset)) throw new InvalidOperationException($"[ESRes][Load] EditorDirect 资产不存在或类型不匹配：AssetId={id}, RequestedType={typeof(T).FullName}");
            RetainObject(id);
            return new ESRuntimeAssetHandle<T>(this, id, string.Empty, asset);
        }

        private async UniTask LoadDirectCoreAsync(ESAssetIdentity id, UniTaskCompletionSource<UnityEngine.Object> completion)
        {
            try
            {
                Report(id, ESRuntimeAssetLoadState.LoadingAsset, .5f, 0, id.ToString());
                UnityEngine.Object loaded = await directAssetProvider.LoadAsync(id, CancellationToken.None);
                if (loaded == null)
                    throw new InvalidOperationException("[ESRes][Load] EditorDirect returned null: AssetId=" + id);
                cachedObjects.Add(id, loaded);
                objectRefCounts.Add(id, 0);
                completion.TrySetResult(loaded);
                Report(id, ESRuntimeAssetLoadState.Ready, 1f, 0, null);
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
                Report(id, ESRuntimeAssetLoadState.Failed, 0f, 0, exception.Message);
            }
            finally
            {
                loadingObjects.Remove(id);
                TryFinalizeDeferredDispose();
            }
        }

        private async UniTask AcquireAssetBundleTreeAsync(string assetBundleKey, ESAssetIdentity requestId, CancellationToken token)
        {
            var acquired = new List<string>();
            try
            {
                await AcquireAssetBundleTreeCoreAsync(assetBundleKey, requestId, null, acquired, token);
            }
            catch
            {
                for (int i = acquired.Count - 1; i >= 0; i--)
                    ReleaseSingleAssetBundle(acquired[i]);
                throw;
            }
        }

        private async UniTask AcquireAssetBundleTreeCoreAsync(string assetBundleKey, ESAssetIdentity requestId, HashSet<string> guard, List<string> acquired, CancellationToken token)
        {
            if (guard != null && !guard.Add(assetBundleKey)) throw new InvalidOperationException($"[ESRes][Load] AssetBundle 依赖循环：BundleKey={assetBundleKey}, RequestAssetId={requestId}");
            try
            {
                if (!runtimeMap.TryGetAssetBundle(assetBundleKey, out var record)) throw new KeyNotFoundException($"[ESRes][RuntimeMap] RuntimeMap 缺少 AssetBundle：BundleKey={assetBundleKey}, RequestAssetId={requestId}");
                Report(requestId, ESRuntimeAssetLoadState.LoadingDependencies, .1f, 0, assetBundleKey);
                var deps = record.Dependencies;
                if (deps != null && deps.Count > 0 && guard == null)
                    guard = new HashSet<string>(StringComparer.Ordinal) { assetBundleKey };
                for (var i = 0; deps != null && i < deps.Count; i++) await AcquireAssetBundleTreeCoreAsync(deps[i], requestId, guard, acquired, token);
                await AcquireAssetBundleAsync(record, requestId, token);
                acquired.Add(assetBundleKey);
            }
            finally { guard?.Remove(assetBundleKey); }
        }

        private async UniTask AcquireAssetBundleAsync(ESRuntimeAssetBundleRecord record, ESAssetIdentity requestId, CancellationToken token)
        {
            if (!assetBundles.TryGetValue(record.AssetBundleKey, out var lease)) { lease = new AssetBundleLease(); assetBundles.Add(record.AssetBundleKey, lease); }
            lease.RefCount++;
            if (lease.Bundle != null) return;
            if (lease.InFlight == null)
            {
                lease.InFlight = new UniTaskCompletionSource<AssetBundle>();
                LoadAssetBundleCoreAsync(record, requestId, lease).Forget();
            }
            try
            {
                await lease.InFlight.Task.AttachExternalCancellation(token);
            }
            catch
            {
                if (lease.RefCount > 0) lease.RefCount--;
                if (lease.RefCount == 0 && lease.Bundle == null && lease.InFlight == null)
                    assetBundles.Remove(record.AssetBundleKey);
                throw;
            }
        }

        private async UniTask LoadAssetBundleCoreAsync(ESRuntimeAssetBundleRecord record, ESAssetIdentity requestId, AssetBundleLease lease)
        {
            UniTaskCompletionSource<AssetBundle> completion = lease.InFlight;
            try
            {
                AssetBundle bundle = await LoadAssetBundleWithRetryAsync(record, requestId, CancellationToken.None);
                lease.Bundle = bundle;
                completion.TrySetResult(bundle);
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }
            finally
            {
                lease.InFlight = null;
                if (lease.RefCount == 0)
                {
                    var failures = new List<Exception>();
                    if (TryUnloadAssetBundle(record.AssetBundleKey, lease.Bundle, failures))
                        assetBundles.Remove(record.AssetBundleKey);
                    else
                    {
                        disposed = true;
                        Debug.LogError(new AggregateException("[ESRes][Unload] 取消后的 AssetBundle 收尾失败；Provider 已阻断后续加载。", failures));
                    }
                }
                TryFinalizeDeferredDispose();
            }
        }

        private async UniTask<AssetBundle> LoadAssetBundleWithRetryAsync(ESRuntimeAssetBundleRecord record, ESAssetIdentity requestId, CancellationToken token)
        {
            Exception lastError = null;
            var delay = retry.InitialDelaySeconds;
            for (var attempt = 1; attempt <= retry.MaxAttempts; attempt++)
            {
                try
                {
                    Report(requestId, ESRuntimeAssetLoadState.LoadingAssetBundle, .2f, attempt, record.AssetBundleKey);
                    var bundle = await assetBundleProvider.LoadAssetBundleAsync(record, null, token);
                    if (bundle != null) return bundle;
                    lastError = new InvalidOperationException($"Provider returned null AssetBundle: {record.AssetBundleKey}");
                }
                catch (Exception e) when (!(e is OperationCanceledException)) { lastError = e; }
                if (attempt < retry.MaxAttempts && delay > 0f)
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(delay), DelayType.Realtime, PlayerLoopTiming.Update, token);
                    delay *= retry.BackoffMultiplier;
                }
            }
            Report(requestId, ESRuntimeAssetLoadState.Failed, 0f, retry.MaxAttempts, lastError?.Message);
            throw new InvalidOperationException($"[ESRes][Load] AssetBundle 重试 {retry.MaxAttempts} 次后仍加载失败：BundleKey={record.AssetBundleKey}, RequestAssetId={requestId}", lastError);
        }

        private void ReleaseAssetBundleTree(string assetBundleKey)
        {
            if (!runtimeMap.TryGetAssetBundle(assetBundleKey, out var record)) return;
            var deps = record.Dependencies;
            for (var i = deps.Count - 1; i >= 0; i--) ReleaseAssetBundleTree(deps[i]);
            ReleaseSingleAssetBundle(assetBundleKey);
        }

        private void ReleaseSingleAssetBundle(string assetBundleKey)
        {
            if (!assetBundles.TryGetValue(assetBundleKey, out var lease) || lease.RefCount <= 0) return;
            lease.RefCount--;
            // RefCount describes active handles, not an automatic unload policy. Bundles
            // remain resident until UnloadAllAtSafePoint so gameplay cannot evict them.
        }

        private void Report(ESAssetIdentity id, ESRuntimeAssetLoadState state, float progress, int attempt, string message)
        {
            var status = new ESRuntimeAssetLoadStatus(id, state, progress, attempt, message);
            statusById[id] = status; StatusChanged?.Invoke(status);
        }

        private bool TryUnloadAssetBundle(string assetBundleKey, AssetBundle bundle, List<Exception> failures)
        {
            if (bundle == null) return true;
            try
            {
                bundle.Unload(false);
                return true;
            }
            catch (Exception exception)
            {
                var failure = new InvalidOperationException(
                    "[ESRes][Unload] AssetBundle 卸载失败：BundleKey=" + assetBundleKey,
                    exception);
                unloadFailureCount++;
                lastUnloadError = failure.ToString();
                failures?.Add(failure);
                return false;
            }
        }

        private void ThrowUnloadFailures(string operation, List<Exception> failures)
        {
            if (failures == null || failures.Count == 0) return;
            var aggregate = new AggregateException(
                "[ESRes][Unload] " + operation + "未能完整完成；Provider 已阻断后续加载。",
                failures);
            Debug.LogError(aggregate);
            throw aggregate;
        }

        private void ThrowIfDisposed() { if (disposed) throw new ObjectDisposedException(nameof(ESRuntimeAssetLoader)); }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (HasPendingOperations)
            {
                // A synchronous Provider disposal may be requested while Unity still owns an
                // AsyncOperation. Mark the loader unavailable to new callers, but defer bundle
                // unload and dictionary clearing until every old request has reached its own
                // completion path. This keeps the old generation self-contained during a
                // Provider transition.
                disposeWhenIdle = true;
                return;
            }
            FinalizeDispose();
        }

        private void TryFinalizeDeferredDispose()
        {
            if (disposeWhenIdle && !HasPendingOperations)
            {
                disposeWhenIdle = false;
                FinalizeDispose();
            }
        }

        private void FinalizeDispose()
        {
            var failures = new List<Exception>();
            var unloadedKeys = new List<string>();
            foreach (var pair in assetBundles)
                if (TryUnloadAssetBundle(pair.Key, pair.Value.Bundle, failures))
                    unloadedKeys.Add(pair.Key);

            for (int i = 0; i < unloadedKeys.Count; i++)
                assetBundles.Remove(unloadedKeys[i]);
            cachedObjects.Clear();
            objectRefCounts.Clear();
            loadingObjects.Clear();
            statusById.Clear();
            if (failures.Count > 0)
                Debug.LogError(new AggregateException("[ESRes][Unload] Provider Dispose 未能完整卸载全部 AssetBundle。", failures));
        }
    }

    /// <summary>
    /// 可复制的轻量 Handle。所有副本共享同一个释放状态，因此无论 Dispose 多少次，
    /// 底层引用计数都只会释放一次。
    /// </summary>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public readonly struct ESRuntimeAssetHandle<T> : IDisposable where T : UnityEngine.Object
    {
        private sealed class SharedState : IESRuntimeAssetLease
        {
            private ESRuntimeAssetLoader loader;
            private readonly ESAssetIdentity id;
            private readonly string assetBundleKey;
            public T Asset { get; private set; }

            public SharedState(ESRuntimeAssetLoader owner, ESAssetIdentity assetId, string bundleKey, T asset)
            {
                loader = owner;
                id = assetId;
                assetBundleKey = bundleKey;
                Asset = asset;
            }

            public void Dispose()
            {
                ESRuntimeAssetLoader owner = Interlocked.Exchange(ref loader, null);
                if (owner == null) return;
                owner.ReleaseLoaded(id, assetBundleKey);
                Asset = null;
            }
        }

        private readonly SharedState state;
        public T Asset => state?.Asset;
        internal IESRuntimeAssetLease Lease => state;

        internal ESRuntimeAssetHandle(ESRuntimeAssetLoader owner, ESAssetIdentity assetId, string assetBundle, T asset)
        {
            state = new SharedState(owner, assetId, assetBundle, asset);
        }

        public void Dispose() => state?.Dispose();
    }

    /// <summary>与资产 Handle 相同，复制后仍保证底层场景租约只释放一次。</summary>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public readonly struct ESRuntimeSceneHandle : IDisposable
    {
        private sealed class SharedState
        {
            private ESRuntimeAssetLoader loader;
            private readonly ESAssetIdentity id;
            private readonly string assetBundleKey;
            public readonly Scene Scene;

            public SharedState(ESRuntimeAssetLoader owner, ESAssetIdentity sceneId, string bundleKey, Scene scene)
            {
                loader = owner;
                id = sceneId;
                assetBundleKey = bundleKey;
                Scene = scene;
            }

            public void Dispose()
            {
                ESRuntimeAssetLoader owner = Interlocked.Exchange(ref loader, null);
                if (owner != null) owner.ReleaseLoaded(id, assetBundleKey);
            }
        }

        private readonly SharedState state;
        public Scene Scene => state != null ? state.Scene : default;

        internal ESRuntimeSceneHandle(ESRuntimeAssetLoader owner, ESAssetIdentity sceneId, string bundleKey, Scene scene)
        {
            state = new SharedState(owner, sceneId, bundleKey, scene);
        }

        public void Dispose() => state?.Dispose();
    }
}
