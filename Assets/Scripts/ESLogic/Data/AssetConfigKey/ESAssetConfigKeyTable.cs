using System;
using System.Collections.Generic;
using System.Threading;
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
    public class ESAssetConfigKeyTable<TConfigData, TAsset> : ESRetainedConfigKeyTable<TConfigData>
        where TConfigData : ESAssetReferConfigDataBase<TAsset>
        where TAsset : UnityEngine.Object
    {
        private const string ProviderSwitchCancellationError = "Provider 已切换，资产加载已取消";

        private sealed class PendingLoad
        {
            public readonly List<Action<TAsset, string>> callbacks = new List<Action<TAsset, string>>(2);
            public bool releaseRequested;
        }

        private readonly Dictionary<int, PendingLoad> pendingLoads;
        private readonly Dictionary<int, int> payloadRetainCounts;
        private readonly string diagnosticScope;
        private readonly bool requiresCurrentCatalogBinding;
        private IESAssetConfigTableLoader<TConfigData, TAsset> loader;

        public ESAssetConfigKeyTable(
            int capacity = 64,
            string keyScope = null,
            bool requiresCurrentCatalogBinding = false) : base(capacity, keyScope)
        {
            pendingLoads = new Dictionary<int, PendingLoad>(capacity);
            payloadRetainCounts = new Dictionary<int, int>(capacity);
            diagnosticScope = string.IsNullOrWhiteSpace(keyScope) ? typeof(TConfigData).Name : keyScope;
            this.requiresCurrentCatalogBinding = requiresCurrentCatalogBinding;
        }

        public bool HasLoader => loader != null;
        public bool HasPendingLoads => pendingLoads.Count != 0;

        public override bool TryGet(int runtimeKey, out TConfigData data)
        {
            if (requiresCurrentCatalogBinding && !ESRuntimeDataAsset.AssetConfigTablesAvailable)
            {
                data = null;
                return false;
            }
            return base.TryGet(runtimeKey, out data);
        }

        public override bool TryGet<TEnumKey>(ESGameCoreConfigKey<TEnumKey> key, out TConfigData data)
            where TEnumKey : struct
        {
            if (requiresCurrentCatalogBinding && !ESRuntimeDataAsset.AssetConfigTablesAvailable)
            {
                data = null;
                return false;
            }
            return base.TryGet(key, out data);
        }

        public override bool TryGet<TEnumKey>(ESAssetConfigKey<TEnumKey> key, out TConfigData data)
            where TEnumKey : struct
        {
            if (requiresCurrentCatalogBinding && !ESRuntimeDataAsset.AssetConfigTablesAvailable)
            {
                data = null;
                return false;
            }
            return base.TryGet(key, out data);
        }

        public override TConfigData Get(int runtimeKey)
        {
            return requiresCurrentCatalogBinding && !ESRuntimeDataAsset.AssetConfigTablesAvailable
                ? null
                : base.Get(runtimeKey);
        }

        public override bool TryGetRuntimeKey(string stringKey, out int runtimeKey)
        {
            if (requiresCurrentCatalogBinding && !ESRuntimeDataAsset.AssetConfigTablesAvailable)
            {
                runtimeKey = 0;
                return false;
            }
            return base.TryGetRuntimeKey(stringKey, out runtimeKey);
        }

        public override bool TryGetRuntimeKey(int enumKey, string stringKey, out int runtimeKey)
        {
            if (requiresCurrentCatalogBinding && !ESRuntimeDataAsset.AssetConfigTablesAvailable)
            {
                runtimeKey = 0;
                return false;
            }
            return base.TryGetRuntimeKey(enumKey, stringKey, out runtimeKey);
        }

        public override bool TryGetRuntimeKey(IESConfigKey key, out int runtimeKey)
        {
            if (requiresCurrentCatalogBinding && !ESRuntimeDataAsset.AssetConfigTablesAvailable)
            {
                runtimeKey = 0;
                return false;
            }
            return base.TryGetRuntimeKey(key, out runtimeKey);
        }

        public override int GetRuntimeKey(IESConfigKey key)
        {
            if (requiresCurrentCatalogBinding && !ESRuntimeDataAsset.AssetConfigTablesAvailable)
                throw new InvalidOperationException(
                    "AssetTable 当前 Catalog 尚未提交；上一代 RuntimeKey 不属于当前 Provider。");
            return base.GetRuntimeKey(key);
        }

        public override bool TryGetByStringKey(string stringKey, out TConfigData data)
        {
            if (requiresCurrentCatalogBinding && !ESRuntimeDataAsset.AssetConfigTablesAvailable)
            {
                data = null;
                return false;
            }
            return base.TryGetByStringKey(stringKey, out data);
        }

        public override bool TryGetSlot(int runtimeKey, out int slot)
        {
            if (requiresCurrentCatalogBinding && !ESRuntimeDataAsset.AssetConfigTablesAvailable)
            {
                slot = -1;
                return false;
            }
            return base.TryGetSlot(runtimeKey, out slot);
        }

        public override bool TryGetSlotByEnumKey(int enumKey, out int slot)
        {
            if (requiresCurrentCatalogBinding && !ESRuntimeDataAsset.AssetConfigTablesAvailable)
            {
                slot = -1;
                return false;
            }
            return base.TryGetSlotByEnumKey(enumKey, out slot);
        }

        public override bool TryGetSlotByStringKey(string stringKey, out int slot)
        {
            if (requiresCurrentCatalogBinding && !ESRuntimeDataAsset.AssetConfigTablesAvailable)
            {
                slot = -1;
                return false;
            }
            return base.TryGetSlotByStringKey(stringKey, out slot);
        }

        public override bool TryGetBySlot(int slot, out TConfigData data)
        {
            if (requiresCurrentCatalogBinding && !ESRuntimeDataAsset.AssetConfigTablesAvailable)
            {
                data = null;
                return false;
            }
            return base.TryGetBySlot(slot, out data);
        }

        public override bool TryGetRuntimeKeyBySlot(int slot, out int runtimeKey)
        {
            if (requiresCurrentCatalogBinding && !ESRuntimeDataAsset.AssetConfigTablesAvailable)
            {
                runtimeKey = 0;
                return false;
            }
            return base.TryGetRuntimeKeyBySlot(slot, out runtimeKey);
        }

        /// <summary>
        /// Catalog/Page 冷路径专用。当前活动表中任一业务别名已存在时保留首条记录，
        /// 记录冲突并拒绝后续项，避免重复注入先修改稳定外壳再被判定冲突。
        /// </summary>
        internal bool TryAcquireBuildRecord(
            IESConfigKey key,
            Func<TConfigData> factory,
            string incomingIdentity,
            out TConfigData data)
        {
            data = null;
            if (!IsBuilding)
                throw new InvalidOperationException("AssetTable 构建记录只能在 BeginBuild/EndBuild 事务内注入。");
            if (key == null || !ESConfigKeyMatch.IsConfigured(key.EnumKeyInt, key.StringKey))
            {
                RecordConflict(0, key?.StringKey, "Asset Catalog record has no EnumKey/StringKey. Skipped.");
                return false;
            }

            int existingRuntimeKey = 0;
            bool enumExists = key.EnumKeyInt != 0
                && base.TryGetSlotByEnumKey(key.EnumKeyInt, out int enumSlot)
                && base.TryGetRuntimeKeyBySlot(enumSlot, out existingRuntimeKey);
            int stringRuntimeKey = 0;
            bool stringExists = !string.IsNullOrEmpty(key.StringKey)
                && base.TryGetRuntimeKey(key.StringKey, out stringRuntimeKey);
            if (existingRuntimeKey == 0 && stringExists)
                existingRuntimeKey = stringRuntimeKey;

            if (enumExists || stringExists)
            {
                RecordConflict(
                    existingRuntimeKey,
                    key.StringKey,
                    "Duplicate Asset Catalog business key. First registration retained; incoming identity="
                    + (incomingIdentity ?? string.Empty) + ".");
                return false;
            }

            if (TryAcquireRetained(key, factory, out data))
                return true;

            RecordConflict(
                0,
                key.StringKey,
                "Asset Catalog EnumKey/StringKey aliases are already bound to different retained shells. Incoming identity="
                + (incomingIdentity ?? string.Empty) + ".");
            return false;
        }

        /// <summary>
        /// Catalog/Page 预检保护重建专用。使用与正式资产注册完全一致的 RuntimeKey 和别名语义，
        /// 但只作用于调用方创建的隔离表，不得用于运行时业务注入。
        /// </summary>
        internal int RegisterPreparedBuildRecord(IESConfigKey key, TConfigData data, string effectiveStringKey)
        {
            return RegisterConfiguredAndGetRuntimeKey(key, data, effectiveStringKey, effectiveStringKey);
        }

        /// <summary>
        /// Asset Catalog 全量重建入口。clear=true 时先释放本表实际资产和 Loader Handle，
        /// 再清活动槽位；业务键对应的轻量配置外壳继续驻留并在重新注册时复用。
        /// </summary>
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public new void BeginBuild(bool clear = false)
        {
            if (IsBuilding)
                throw new InvalidOperationException("ESAssetConfigKeyTable is already building.");

            if (clear)
            {
                if (pendingLoads.Count != 0)
                    throw new InvalidOperationException("AssetTable 仍有加载请求，不能全量重建 Catalog。");

                for (int slot = 0; slot < Count; slot++)
                {
                    if (!base.TryGetRuntimeKeyBySlot(slot, out int runtimeKey))
                        continue;
                    Release(runtimeKey);
                }
            }

            base.BeginBuild(clear);
        }

        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public void SetLoader(IESAssetConfigTableLoader<TConfigData, TAsset> assetLoader)
        {
            if (ReferenceEquals(loader, assetLoader))
                return;

            // Loader 持有底层 Runtime Handle；换 Provider/重绑表前必须先完整释放，
            // 不能只清 ConfigData 上的 LoadedAsset 标记。
            ResetLoader();
            loader = assetLoader;
        }

        /// <summary>
        /// 全量资源安全点或 Provider 重建时使用。释放本表所有底层 Handle、清理
        /// 合并请求和 LoadedAsset 状态，使同一 RuntimeKey 可以在新 Loader 上再次加载。
        /// </summary>
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public int ResetLoader()
        {
            PendingLoad[] canceledLoads = null;
            if (pendingLoads.Count != 0)
            {
                canceledLoads = new PendingLoad[pendingLoads.Count];
                pendingLoads.Values.CopyTo(canceledLoads, 0);
                for (int i = 0; i < canceledLoads.Length; i++)
                    canceledLoads[i].releaseRequested = true;
                pendingLoads.Clear();
            }

            IESAssetConfigTableLoader<TConfigData, TAsset> previousLoader = loader;
            loader = null;
            payloadRetainCounts.Clear();

            int cleared = 0;
            try
            {
                if (previousLoader is IDisposable disposable)
                    disposable.Dispose();
            }
            finally
            {
                for (int i = 0; i < Count; i++)
                {
                    if (!base.TryGetBySlot(i, out TConfigData configData) || !configData.HasLoadedAsset)
                        continue;
                    configData.ClearLoadedAsset();
                    cleared++;
                }

                if (canceledLoads != null)
                {
                    for (int i = 0; i < canceledLoads.Length; i++)
                        InvokeCallbacks(canceledLoads[i], null, ProviderSwitchCancellationError);
                }
            }
            return cleared;
        }

        internal int ClearLoadedAssetPayloads()
        {
            int cleared = 0;
            for (int slot = 0; slot < Count; slot++)
            {
                if (!base.TryGetBySlot(slot, out TConfigData configData) || !configData.HasLoadedAsset)
                    continue;

                configData.ClearLoadedAsset();
                cleared++;
            }
            return cleared;
        }

        /// <summary>热路径查询：只读取 Ready 缓存，绝不隐式触发加载。</summary>
        public bool TryGetReady(IESConfigKey key, out TAsset asset)
        {
            if (requiresCurrentCatalogBinding && !ESRuntimeDataAsset.AssetConfigTablesAvailable)
            {
                asset = null;
                return false;
            }
            if (TryGetRuntimeKey(key, out int runtimeKey))
                return TryGetReady(runtimeKey, out asset);

            asset = null;
            return false;
        }

        /// <summary>RuntimeKey 热路径重载；只读取 Ready 缓存，绝不隐式触发加载。</summary>
        public bool TryGetReady(int runtimeKey, out TAsset asset)
        {
            if (requiresCurrentCatalogBinding && !ESRuntimeDataAsset.AssetConfigTablesAvailable)
            {
                asset = null;
                return false;
            }
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

        internal bool TryRetainLoadedPayload(int runtimeKey)
        {
            if (!base.TryGet(runtimeKey, out TConfigData configData)
                || configData == null
                || !configData.HasLoadedAsset)
                return false;

            payloadRetainCounts.TryGetValue(runtimeKey, out int count);
            if (count == int.MaxValue)
                throw new InvalidOperationException("AssetTable Payload Lease 计数已溢出。");
            payloadRetainCounts[runtimeKey] = count + 1;
            return true;
        }

        internal void ReleaseLoadedPayload(int runtimeKey)
        {
            if (!payloadRetainCounts.TryGetValue(runtimeKey, out int count))
                return;

            if (count > 1)
            {
                payloadRetainCounts[runtimeKey] = count - 1;
                return;
            }

            payloadRetainCounts.Remove(runtimeKey);
            Release(runtimeKey);
        }

        /// <summary>
        /// Ready 时立即回调缓存对象；未 Ready 时对同一个 RuntimeKey 合并请求，并交给当前 Loader。
        /// 回调仅发生于调用线程/Loader 完成线程；Unity Loader 必须保证在主线程回调。
        /// </summary>
        public void GetOrLoadAsync(IESConfigKey key, Action<TAsset, string> completed)
        {
            if (!EnsureCurrentCatalogAvailable(completed))
                return;
            if (!TryGetRuntimeKey(key, out int runtimeKey))
            {
                string description = key == null
                    ? "<null>"
                    : ESConfigKeyMatch.Describe(key.EnumKeyInt, key.StringKey);
                completed?.Invoke(null, "AssetTable 未登记业务 Key: " + description);
                return;
            }

            GetOrLoadAsync(runtimeKey, completed);
        }

        /// <summary>RuntimeKey 热路径重载；未 Ready 时合并请求并交给当前 Loader。</summary>
        public void GetOrLoadAsync(int runtimeKey, Action<TAsset, string> completed)
        {
            if (!EnsureCurrentCatalogAvailable(completed))
                return;
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
                CompleteLoad(runtimeKey, configData, pending, null, null, "AssetTable 尚未配置 Loader");
                return;
            }

            IESAssetConfigTableLoader<TConfigData, TAsset> requestLoader = loader;
            try
            {
                requestLoader.LoadAsync(runtimeKey, configData,
                    (asset, error) => CompleteLoad(
                        runtimeKey,
                        configData,
                        pending,
                        requestLoader,
                        asset,
                        error));
            }
            catch (Exception exception)
            {
                // Loader 接口允许第三方 RunMode 后端同步失败。失败必须走同一完成事务，
                // 否则该 RuntimeKey 会永久滞留在 pendingLoads 中并吞掉后续重试。
                CompleteLoad(runtimeKey, configData, pending, requestLoader, null, exception.Message);
            }
        }

        private bool EnsureCurrentCatalogAvailable(Action<TAsset, string> completed)
        {
            if (!requiresCurrentCatalogBinding || ESRuntimeDataAsset.AssetConfigTablesAvailable)
                return true;

            const string error = "AssetTable 当前 Catalog 尚未提交；上一代配置表不会用于当前 Provider";
            ESConfigKeyDiagnostics.ReportMissing(diagnosticScope, error);
            completed?.Invoke(null, error);
            return false;
        }

        /// <summary>
        /// 按业务 Key 驱逐本表共享缓存；稳定配置外壳和业务 Key 映射继续保留。
        /// 仅供 ResourcePlan、Scope 或统一内存管理服务调用，普通业务不得把它当作调用者私有引用释放。
        /// </summary>
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public bool Release(IESConfigKey key)
        {
            return base.TryGetRuntimeKey(key, out int runtimeKey) && Release(runtimeKey);
        }

        /// <summary>按 RuntimeKey 驱逐共享缓存。生命周期和依赖释放由当前 Loader 执行。</summary>
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public bool Release(int runtimeKey)
        {
            if (!base.TryGet(runtimeKey, out TConfigData configData))
                return false;

            if (payloadRetainCounts.TryGetValue(runtimeKey, out int retained) && retained > 0)
                return false;

            if (pendingLoads.TryGetValue(runtimeKey, out PendingLoad pending))
            {
                pending.releaseRequested = true;
                return true;
            }

            if (!configData.HasLoadedAsset)
                return false;

            TAsset asset = configData.LoadedAsset;
            configData.ClearLoadedAsset();
            loader?.Release(runtimeKey, configData, asset);
            return true;
        }

        internal void ClearPendingLoads()
        {
            foreach (PendingLoad pending in pendingLoads.Values)
                pending.releaseRequested = true;
        }

        private void CompleteLoad(
            int runtimeKey,
            TConfigData configData,
            PendingLoad expectedPending,
            IESAssetConfigTableLoader<TConfigData, TAsset> requestLoader,
            TAsset asset,
            string error)
        {
            if (!pendingLoads.TryGetValue(runtimeKey, out PendingLoad pending)
                || !ReferenceEquals(pending, expectedPending))
            {
                // Provider 切换后同 Key 可能已经产生新请求。旧请求的迟到结果只能
                // 交还给发起它的旧 Loader，绝不能完成新 Pending 或恢复 Ready。
                if (asset != null)
                    requestLoader?.Release(runtimeKey, configData, asset);
                return;
            }

            pendingLoads.Remove(runtimeKey);
            bool succeeded = asset != null && string.IsNullOrEmpty(error);
            if (succeeded && !pending.releaseRequested)
                configData.SetLoadedAsset(asset);
            else
                configData.ClearLoadedAsset();

            if (asset != null && (!succeeded || pending.releaseRequested))
                requestLoader?.Release(runtimeKey, configData, asset);

            string finalError = pending.releaseRequested
                ? "资产在加载完成前已释放"
                : succeeded ? null : (string.IsNullOrEmpty(error) ? "Loader 返回空资产" : error);
            InvokeCallbacks(pending, pending.releaseRequested ? null : asset, finalError);
        }

        private static void InvokeCallbacks(PendingLoad pending, TAsset asset, string error)
        {
            for (int i = 0; i < pending.callbacks.Count; i++)
            {
                try
                {
                    pending.callbacks[i]?.Invoke(asset, error);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
            pending.callbacks.Clear();
        }

        protected override void OnRetainedReleased(TConfigData data)
        {
            data?.ClearLoadedAsset();
        }
    }

    internal delegate ESAssetConfigKeyTable<TConfigData, TAsset> ESAssetConfigTableSelector<TConfigData, TAsset>(
        ESAssetConfigTableGenerationState state)
        where TConfigData : ESAssetReferConfigDataBase<TAsset>
        where TAsset : UnityEngine.Object;

    /// <summary>
    /// Pins one ConfigTable generation while a loaded asset is in use. Disposing the lease releases
    /// the per-key payload ownership first, then allows a retired generation to reclaim its Loaders.
    /// </summary>
    public sealed class ESAssetConfigPayloadLease<TAsset> : IDisposable where TAsset : UnityEngine.Object
    {
        private Action release;
        private TAsset asset;

        internal ESAssetConfigPayloadLease(TAsset asset, long generation, Action release)
        {
            this.asset = asset;
            Generation = generation;
            this.release = release ?? throw new ArgumentNullException(nameof(release));
        }

        public TAsset Asset => asset;
        public long Generation { get; }
        public bool IsDisposed => Volatile.Read(ref release) == null;

        public void Dispose()
        {
            Action ownedRelease = Interlocked.Exchange(ref release, null);
            if (ownedRelease == null)
                return;

            asset = null;
            try
            {
                ownedRelease();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }

        internal sealed class ESAssetConfigDataReadLease<TConfigData, TAsset> : IDisposable
        where TConfigData : ESAssetReferConfigDataBase<TAsset>
        where TAsset : UnityEngine.Object
        {
            private ESAssetConfigTableGenerationLease generationLease;
            private TConfigData data;

        internal ESAssetConfigDataReadLease(
            ESAssetConfigTableGenerationLease generationLease,
            int runtimeKey,
            TConfigData data)
        {
            this.generationLease = generationLease;
            RuntimeKey = runtimeKey;
            this.data = data;
        }

        public long Generation => generationLease?.Generation ?? 0;
        public int RuntimeKey { get; }
        public TConfigData Data => generationLease == null
            ? throw new ObjectDisposedException(nameof(ESAssetConfigDataReadLease<TConfigData, TAsset>))
            : data;

        public void Dispose()
        {
            ESAssetConfigTableGenerationLease ownedLease = Interlocked.Exchange(ref generationLease, null);
            data = null;
            ownedLease?.Dispose();
        }
    }

    /// <summary>
    /// Public Asset ConfigTable facade. It never exposes the mutable table or its dictionaries;
    /// every operation first pins one immutable generation snapshot.
    /// </summary>
    public sealed class ESAssetConfigTableReader<TConfigData, TAsset>
        where TConfigData : ESAssetReferConfigDataBase<TAsset>
        where TAsset : UnityEngine.Object
    {
        private const string UnavailableError = "Asset ConfigTable 当前没有与 Provider 同代提交的可用状态";
        private readonly ESAssetConfigTableSelector<TConfigData, TAsset> selectTable;

        internal ESAssetConfigTableReader(ESAssetConfigTableSelector<TConfigData, TAsset> selectTable)
        {
            this.selectTable = selectTable ?? throw new ArgumentNullException(nameof(selectTable));
        }

        public long Generation => ESRuntimeDataAsset.AssetConfigTableGeneration;

        public int Count
        {
            get
            {
                if (!ESRuntimeDataAsset.TryAcquireAssetConfigGeneration(false, out ESAssetConfigTableGenerationLease lease))
                    return 0;
                try { return selectTable(lease.State).Count; }
                finally { lease.Dispose(); }
            }
        }

        public bool TryResolveAssetIdentity(
            int enumKey,
            string stringKey,
            out ESAssetIdentity identity)
        {
            identity = default;
            if (!ESRuntimeDataAsset.TryAcquireAssetConfigGeneration(false, out ESAssetConfigTableGenerationLease lease))
                return false;

            try
            {
                ESAssetConfigKeyTable<TConfigData, TAsset> table = selectTable(lease.State);
                if (!table.TryGetRuntimeKey(enumKey, stringKey, out int runtimeKey)
                    || !table.TryGet(runtimeKey, out TConfigData data)
                    || data == null)
                    return false;

                identity = new ESAssetIdentity(data.AssetGuid, data.AssetLocalFileId);
                return identity.IsValid;
            }
            finally
            {
                lease.Dispose();
            }
        }

        public bool TryAcquireReady(
            IESConfigKey key,
            out ESAssetConfigPayloadLease<TAsset> payloadLease)
        {
            payloadLease = null;
            if (!ESRuntimeDataAsset.TryAcquireAssetConfigGeneration(true, out ESAssetConfigTableGenerationLease generationLease))
                return false;

            ESAssetConfigKeyTable<TConfigData, TAsset> table = selectTable(generationLease.State);
            try
            {
                if (!table.TryGetRuntimeKey(key, out int runtimeKey)
                    || !table.TryGetReady(runtimeKey, out TAsset asset)
                    || !table.TryRetainLoadedPayload(runtimeKey))
                {
                    generationLease.Dispose();
                    return false;
                }

                payloadLease = CreatePayloadLease(table, runtimeKey, asset, generationLease);
                return true;
            }
            catch
            {
                generationLease.Dispose();
                throw;
            }
        }

        public void GetOrLoadAsync(
            IESConfigKey key,
            Action<ESAssetConfigPayloadLease<TAsset>, string> completed)
        {
            if (!ESRuntimeDataAsset.TryAcquireAssetConfigGeneration(true, out ESAssetConfigTableGenerationLease generationLease))
            {
                completed?.Invoke(null, UnavailableError);
                return;
            }

            ESAssetConfigKeyTable<TConfigData, TAsset> table = selectTable(generationLease.State);
            if (!table.TryGetRuntimeKey(key, out int runtimeKey))
            {
                generationLease.Dispose();
                completed?.Invoke(null, "AssetTable 未登记业务 Key");
                return;
            }

            try
            {
                table.GetOrLoadAsync(runtimeKey, (asset, error) =>
                {
                    if (asset == null || !string.IsNullOrEmpty(error)
                        || !table.TryRetainLoadedPayload(runtimeKey))
                    {
                        generationLease.Dispose();
                        completed?.Invoke(null, string.IsNullOrEmpty(error) ? "AssetTable 未能取得 Payload Lease" : error);
                        return;
                    }

                    ESAssetConfigPayloadLease<TAsset> payload = CreatePayloadLease(
                        table,
                        runtimeKey,
                        asset,
                        generationLease);
                    if (completed == null)
                    {
                        payload.Dispose();
                        return;
                    }

                    try
                    {
                        completed(payload, null);
                    }
                    catch
                    {
                        payload.Dispose();
                        throw;
                    }
                });
            }
            catch
            {
                generationLease.Dispose();
                throw;
            }
        }

        internal bool TryAcquireConfigData(
            string stringKey,
            out ESAssetConfigDataReadLease<TConfigData, TAsset> dataLease)
        {
            dataLease = null;
            if (!ESRuntimeDataAsset.TryAcquireAssetConfigGeneration(false, out ESAssetConfigTableGenerationLease generationLease))
                return false;

            ESAssetConfigKeyTable<TConfigData, TAsset> table = selectTable(generationLease.State);
            try
            {
                if (!table.TryGetRuntimeKey(stringKey, out int runtimeKey)
                    || !table.TryGet(runtimeKey, out TConfigData data)
                    || data == null)
                {
                    generationLease.Dispose();
                    return false;
                }

                dataLease = new ESAssetConfigDataReadLease<TConfigData, TAsset>(generationLease, runtimeKey, data);
                return true;
            }
            catch
            {
                generationLease.Dispose();
                throw;
            }
        }

        private static ESAssetConfigPayloadLease<TAsset> CreatePayloadLease(
            ESAssetConfigKeyTable<TConfigData, TAsset> table,
            int runtimeKey,
            TAsset asset,
            ESAssetConfigTableGenerationLease generationLease)
        {
            return new ESAssetConfigPayloadLease<TAsset>(asset, generationLease.Generation, () =>
            {
                try { table.ReleaseLoadedPayload(runtimeKey); }
                finally { generationLease.Dispose(); }
            });
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

                if (handle.Asset == null)
                {
                    handle.Dispose();
                    completed?.Invoke(null, "Runtime Provider 返回空资产");
                    return;
                }

                try
                {
                    handles.Add(runtimeKey, handle);
                }
                catch
                {
                    handle.Dispose();
                    throw;
                }
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
