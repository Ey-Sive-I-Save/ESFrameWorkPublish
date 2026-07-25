using System;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// AssetTable 内部使用的加载后端。四种 ESAssetRunMode 各自提供实现，业务代码不直接接触它。
    /// </summary>
    public interface IESAssetConfigTableLoader<TConfigData, TAsset>
        where TConfigData : ESAssetReferConfigDataBase<TAsset>
        where TAsset : UnityEngine.Object
    {
        void LoadAsync(int runtimeKey, TConfigData configData, Action<TAsset, string> completed);
        void Release(int runtimeKey, TConfigData configData, TAsset asset);
    }

    /// <summary>
    /// 资产专用的权威配置表。
    /// RuntimeKey 仍由 ESConfigKeyTable 负责查找；本类只补充 Ready 直取、同 Key 请求合并和 Loader 回填。
    /// </summary>
    public class ESAssetConfigKeyTable<TConfigData, TAsset> : ESConfigKeyTable<TConfigData>
        where TConfigData : ESAssetReferConfigDataBase<TAsset>
        where TAsset : UnityEngine.Object
    {
        private sealed class PendingLoad
        {
            public readonly List<Action<TAsset, string>> callbacks = new List<Action<TAsset, string>>(2);
        }

        private readonly Dictionary<int, PendingLoad> pendingLoads;
        private IESAssetConfigTableLoader<TConfigData, TAsset> loader;

        public ESAssetConfigKeyTable(int capacity = 64) : base(capacity)
        {
            pendingLoads = new Dictionary<int, PendingLoad>(capacity);
        }

        public bool HasLoader => loader != null;

        public void SetLoader(IESAssetConfigTableLoader<TConfigData, TAsset> assetLoader)
        {
            loader = assetLoader;
        }

        /// <summary>热路径查询：只读取 Ready 缓存，绝不隐式触发加载。</summary>
        public bool TryGetReady(int runtimeKey, out TAsset asset)
        {
            if (TryGet(runtimeKey, out TConfigData configData) && configData.HasLoadedAsset)
            {
                asset = configData.LoadedAsset;
                return asset != null;
            }

            asset = null;
            return false;
        }

        public bool IsLoading(int runtimeKey)
        {
            return pendingLoads.ContainsKey(runtimeKey);
        }

        /// <summary>
        /// Ready 时立即回调缓存对象；未 Ready 时对同一个 RuntimeKey 合并请求，并交给当前 Loader。
        /// 回调仅发生于调用线程/Loader 完成线程；Unity Loader 必须保证在主线程回调。
        /// </summary>
        public void GetOrLoadAsync(int runtimeKey, Action<TAsset, string> completed)
        {
            if (!TryGet(runtimeKey, out TConfigData configData))
            {
                completed?.Invoke(null, $"AssetTable 未登记 RuntimeKey: {runtimeKey}");
                return;
            }

            if (configData.HasLoadedAsset)
            {
                completed?.Invoke(configData.LoadedAsset, null);
                return;
            }

            if (pendingLoads.TryGetValue(runtimeKey, out PendingLoad pending))
            {
                if (completed != null)
                    pending.callbacks.Add(completed);
                return;
            }

            pending = new PendingLoad();
            if (completed != null)
                pending.callbacks.Add(completed);
            pendingLoads.Add(runtimeKey, pending);

            if (loader == null)
            {
                CompleteLoad(runtimeKey, configData, null, "AssetTable 尚未配置 Loader");
                return;
            }

            loader.LoadAsync(runtimeKey, configData, (asset, error) => CompleteLoad(runtimeKey, configData, asset, error));
        }

        /// <summary>释放本表缓存的单个资产。生命周期和依赖释放由当前 Loader 执行。</summary>
        public bool Release(int runtimeKey)
        {
            if (!TryGet(runtimeKey, out TConfigData configData) || !configData.HasLoadedAsset)
                return false;

            TAsset asset = configData.LoadedAsset;
            configData.ClearLoadedAsset();
            loader?.Release(runtimeKey, configData, asset);
            return true;
        }

        public void ClearPendingLoads()
        {
            pendingLoads.Clear();
        }

        private void CompleteLoad(int runtimeKey, TConfigData configData, TAsset asset, string error)
        {
            if (!pendingLoads.TryGetValue(runtimeKey, out PendingLoad pending))
                return;

            pendingLoads.Remove(runtimeKey);
            if (asset != null && string.IsNullOrEmpty(error))
                configData.SetLoadedAsset(asset);
            else
                configData.ClearLoadedAsset();

            string finalError = asset != null && string.IsNullOrEmpty(error) ? null : (string.IsNullOrEmpty(error) ? "Loader 返回空资产" : error);
            for (int i = 0; i < pending.callbacks.Count; i++)
                pending.callbacks[i]?.Invoke(asset, finalError);
        }
    }

    /// <summary>
    /// IESAssetRuntimeProvider 到分类 AssetTable 的适配器。
    /// 每张 Table 只保留每个 RuntimeKey 的一个底层 Handle；Table 的请求合并保证不会重复创建 Handle。
    /// </summary>
    public sealed class ESRuntimeAssetTableLoader<TConfigData, TAsset> : IESAssetConfigTableLoader<TConfigData, TAsset>, IDisposable
        where TConfigData : ESAssetReferConfigDataBase<TAsset>
        where TAsset : UnityEngine.Object
    {
        private readonly IESAssetRuntimeProvider runtimeProvider;
        private readonly Dictionary<int, ESRuntimeAssetHandle<TAsset>> handles = new Dictionary<int, ESRuntimeAssetHandle<TAsset>>();
        private bool disposed;

        public ESRuntimeAssetTableLoader(IESAssetRuntimeProvider provider)
        {
            runtimeProvider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        public void LoadAsync(int runtimeKey, TConfigData configData, Action<TAsset, string> completed)
        {
            LoadInternalAsync(runtimeKey, configData, completed);
        }

        private async void LoadInternalAsync(int runtimeKey, TConfigData configData, Action<TAsset, string> completed)
        {
            try
            {
                if (disposed)
                {
                    completed?.Invoke(null, "Runtime AssetTable Loader 已释放");
                    return;
                }

                ESRuntimeAssetHandle<TAsset> handle;
                if (configData.IsSubAsset)
                    handle = await runtimeProvider.LoadSubAssetAsync<TAsset>(new ESAssetIdentity(configData.AssetGuid, configData.AssetLocalFileId));
                else
                    handle = await runtimeProvider.LoadMainAssetAsync<TAsset>(new ESAssetIdentity(configData.AssetGuid));
                if (disposed)
                {
                    handle.Dispose();
                    completed?.Invoke(null, "Runtime AssetTable Loader 已释放");
                    return;
                }

                handles.Add(runtimeKey, handle);
                completed?.Invoke(handle.Asset, null);
            }
            catch (Exception exception)
            {
                completed?.Invoke(null, exception.Message);
            }
        }

        public void Release(int runtimeKey, TConfigData configData, TAsset asset)
        {
            if (!handles.TryGetValue(runtimeKey, out ESRuntimeAssetHandle<TAsset> handle))
                return;

            handles.Remove(runtimeKey);
            handle.Dispose();
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            foreach (ESRuntimeAssetHandle<TAsset> handle in handles.Values)
                handle.Dispose();
            handles.Clear();
        }
    }
}
