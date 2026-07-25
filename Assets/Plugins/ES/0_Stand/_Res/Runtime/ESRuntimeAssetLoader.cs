using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ES
{
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
    public interface IESAssetRuntimeProvider : IDisposable
    {
        UniTask<ESRuntimeAssetHandle<T>> LoadMainAssetAsync<T>(ESAssetIdentity id, CancellationToken cancellationToken = default) where T : UnityEngine.Object;
        UniTask<ESRuntimeAssetHandle<T>> LoadSubAssetAsync<T>(ESAssetIdentity id, CancellationToken cancellationToken = default) where T : UnityEngine.Object;
        UniTask<ESRuntimeSceneHandle> LoadSceneAsync(ESAssetIdentity id, LoadSceneMode mode = LoadSceneMode.Single, CancellationToken cancellationToken = default);
        bool TryGetLoaded<T>(ESAssetIdentity id, out T asset) where T : UnityEngine.Object;
        bool TryGetStatus(ESAssetIdentity id, out ESRuntimeAssetLoadStatus status);
        void Release(ESAssetIdentity id);
        UniTask<ESRuntimeUnusedAssetUnloadResult> UnloadZeroReferenceAssetsAsync(CancellationToken cancellationToken = default);
        void UnloadAllAtSafePoint();
    }

    public interface IESRuntimeDirectAssetProvider
    {
        UniTask<UnityEngine.Object> LoadAsync(ESAssetIdentity id, CancellationToken cancellationToken);
        UniTask<Scene> LoadSceneAsync(ESAssetIdentity id, LoadSceneMode mode, CancellationToken cancellationToken);
    }

    /// <summary>GUID/GUID+LocalFileId 到物理加载位置的运行时加载器。RuntimeKey 不参与全局寻址。</summary>
    public sealed class ESRuntimeAssetLoader : IESAssetRuntimeProvider
    {
        private sealed class AssetBundleLease
        {
            public AssetBundle Bundle;
            public int RefCount;
            public UniTaskCompletionSource<AssetBundle> InFlight;
        }

        private readonly ESGlobalAssetRuntimeMap runtimeMap;
        private readonly IESRuntimeAssetBundleProvider assetBundleProvider;
        private readonly IESRuntimeDirectAssetProvider directAssetProvider;
        private readonly ESRuntimeRetryPolicy retry;
        private readonly Dictionary<string, AssetBundleLease> assetBundles = new Dictionary<string, AssetBundleLease>(StringComparer.Ordinal);
        private readonly Dictionary<ESAssetIdentity, UnityEngine.Object> cachedObjects = new Dictionary<ESAssetIdentity, UnityEngine.Object>();
        private readonly Dictionary<ESAssetIdentity, int> objectRefCounts = new Dictionary<ESAssetIdentity, int>();
        private readonly Dictionary<ESAssetIdentity, UniTaskCompletionSource<UnityEngine.Object>> loadingObjects = new Dictionary<ESAssetIdentity, UniTaskCompletionSource<UnityEngine.Object>>();
        private readonly Dictionary<ESAssetIdentity, ESRuntimeAssetLoadStatus> statusById = new Dictionary<ESAssetIdentity, ESRuntimeAssetLoadStatus>();
        private bool disposed;

        public event Action<ESRuntimeAssetLoadStatus> StatusChanged;

        public ESRuntimeAssetLoader(ESGlobalAssetRuntimeMap globalRuntimeMap, IESRuntimeAssetBundleProvider provider, ESRuntimeRetryPolicy retryPolicy, IESRuntimeDirectAssetProvider directProvider = null)
        {
            runtimeMap = globalRuntimeMap ? globalRuntimeMap : throw new ArgumentNullException(nameof(globalRuntimeMap), "[ESRes][RuntimeMap] Global Runtime Map 不能为空。");
            if (provider == null && directProvider == null) throw new ArgumentNullException(nameof(provider), "[ESRes][Load] AssetBundle Provider 与 Direct Provider 不能同时为空。");
            assetBundleProvider = provider;
            directAssetProvider = directProvider;
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
            if (directAssetProvider != null) return await LoadDirectAsync<T>(id, cancellationToken);
            if (id.IsSubAsset) throw new ArgumentException("[ESRes][Load] 子资产身份必须使用 LoadSubAssetAsync。", nameof(id));
            if (!runtimeMap.TryGetMainAsset(id.Guid, out var record)) throw new KeyNotFoundException($"[ESRes][Load] 主资产 GUID 未登记：GUID={id.Guid}");
            await AcquireAssetBundleTreeAsync(record.AssetBundleKey, id, new HashSet<string>(StringComparer.Ordinal), cancellationToken);
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
            if (directAssetProvider != null) return await LoadDirectAsync<T>(id, cancellationToken);
            var subAssetId = new ESSubAssetId(id.Guid, id.LocalFileId);
            if (!runtimeMap.TryGetSubAsset(subAssetId, out var sub) || string.IsNullOrEmpty(sub.AssetBundleKey))
                throw new KeyNotFoundException($"[ESRes][SubAsset] 子资产身份或 BundleKey 未登记：GUID={subAssetId.Guid}, LocalFileId={subAssetId.LocalFileId}");
            await AcquireAssetBundleTreeAsync(sub.AssetBundleKey, id, new HashSet<string>(StringComparer.Ordinal), cancellationToken);
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
                Scene directScene = await directAssetProvider.LoadSceneAsync(id, mode, cancellationToken);
                return new ESRuntimeSceneHandle(this, id, string.Empty, directScene);
            }
            if (!runtimeMap.TryGetMainAsset(id.Guid, out ESRuntimeAssetRecord record)) throw new KeyNotFoundException($"[ESRes][Load] Scene GUID 未登记：GUID={id.Guid}");
            await AcquireAssetBundleTreeAsync(record.AssetBundleKey, id, new HashSet<string>(StringComparer.Ordinal), cancellationToken);
            try
            {
                if (!assetBundles.TryGetValue(record.AssetBundleKey, out AssetBundleLease lease) || lease.Bundle == null)
                    throw new InvalidOperationException($"[ESRes][Load] Scene 所属 AssetBundle 未加载：BundleKey={record.AssetBundleKey}, GUID={id.Guid}");
                Report(id, ESRuntimeAssetLoadState.LoadingAsset, .9f, 0, record.InternalName);
                // AssetBundle 只负责将场景数据载入内存；真正触发场景切换的是 SceneManager。
                AsyncOperation operation = SceneManager.LoadSceneAsync(record.InternalName, mode);
                if (operation == null) throw new InvalidOperationException($"[ESRes][Load] AssetBundle 中未找到 Scene：GUID={id.Guid}, BundleKey={record.AssetBundleKey}, InternalName={record.InternalName}");
                await operation.ToUniTask(cancellationToken: cancellationToken);
                Scene scene = SceneManager.GetSceneByPath(record.InternalName);
                if (!scene.IsValid()) throw new InvalidOperationException($"[ESRes][Load] 已加载 Scene 无效：GUID={id.Guid}, BundleKey={record.AssetBundleKey}, InternalName={record.InternalName}");
                Report(id, ESRuntimeAssetLoadState.Ready, 1f, 0, null);
                return new ESRuntimeSceneHandle(this, id, record.AssetBundleKey, scene);
            }
            catch { ReleaseAssetBundleTree(record.AssetBundleKey); throw; }
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
                if (lease.Bundle != null) lease.Bundle.Unload(false);
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
            finally { loadingObjects.Remove(id); }
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
            finally { loadingObjects.Remove(id); }
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
            }
        }

        private async UniTask AcquireAssetBundleTreeAsync(string assetBundleKey, ESAssetIdentity requestId, HashSet<string> guard, CancellationToken token)
        {
            var acquired = new List<string>();
            try
            {
                await AcquireAssetBundleTreeCoreAsync(assetBundleKey, requestId, guard, acquired, token);
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
            if (!guard.Add(assetBundleKey)) throw new InvalidOperationException($"[ESRes][Load] AssetBundle 依赖循环：BundleKey={assetBundleKey}, RequestAssetId={requestId}");
            try
            {
                if (!runtimeMap.TryGetAssetBundle(assetBundleKey, out var record)) throw new KeyNotFoundException($"[ESRes][RuntimeMap] RuntimeMap 缺少 AssetBundle：BundleKey={assetBundleKey}, RequestAssetId={requestId}");
                Report(requestId, ESRuntimeAssetLoadState.LoadingDependencies, .1f, 0, assetBundleKey);
                var deps = record.Dependencies;
                for (var i = 0; i < deps.Count; i++) await AcquireAssetBundleTreeCoreAsync(deps[i], requestId, guard, acquired, token);
                await AcquireAssetBundleAsync(record, requestId, token);
                acquired.Add(assetBundleKey);
            }
            finally { guard.Remove(assetBundleKey); }
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
                    if (lease.Bundle != null) lease.Bundle.Unload(false);
                    assetBundles.Remove(record.AssetBundleKey);
                }
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

        private void ThrowIfDisposed() { if (disposed) throw new ObjectDisposedException(nameof(ESRuntimeAssetLoader)); }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            foreach (var pair in assetBundles) if (pair.Value.Bundle != null) pair.Value.Bundle.Unload(false);
            assetBundles.Clear(); cachedObjects.Clear(); objectRefCounts.Clear(); loadingObjects.Clear(); statusById.Clear();
        }
    }

    public struct ESRuntimeAssetHandle<T> : IDisposable where T : UnityEngine.Object
    {
        private ESRuntimeAssetLoader loader;
        private readonly ESAssetIdentity id;
        private readonly string assetBundleKey;
        public T Asset { get; private set; }
        internal ESRuntimeAssetHandle(ESRuntimeAssetLoader owner, ESAssetIdentity assetId, string assetBundle, T asset)
        { loader = owner; id = assetId; assetBundleKey = assetBundle; Asset = asset; }
        public void Dispose() { if (loader == null) return; loader.ReleaseLoaded(id, assetBundleKey); loader = null; Asset = null; }
    }

    public struct ESRuntimeSceneHandle : IDisposable
    {
        private ESRuntimeAssetLoader loader;
        private readonly ESAssetIdentity id;
        private readonly string assetBundleKey;
        public Scene Scene { get; }
        internal ESRuntimeSceneHandle(ESRuntimeAssetLoader owner, ESAssetIdentity sceneId, string bundleKey, Scene scene)
        { loader = owner; id = sceneId; assetBundleKey = bundleKey; Scene = scene; }
        public void Dispose() { if (loader == null) return; loader.ReleaseLoaded(id, assetBundleKey); loader = null; }
    }
}
