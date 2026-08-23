using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ES
{
    [Serializable]
    public sealed class ESGameObjectPoolConfig
    {
        [LabelText("预热数量")]
        public int prewarmCount;

        [LabelText("空闲保留上限")]
        public int maxInactiveCount = 64;

        [LabelText("总量上限")]
        public int maxTotalCount = 256;

        [LabelText("允许扩容")]
        public bool allowExpand = true;

        [LabelText("溢出销毁")]
        public bool destroyOverflow = true;

        [LabelText("自动修补")]
        public bool autoRepair = true;

        [LabelText("自动修补目标空闲数")]
        public int repairInactiveTarget;

        [LabelText("归还时清父级")]
        public bool clearParentOnReturn = true;

        [LabelText("归还时停粒子")]
        public bool stopParticlesOnReturn = true;

        [LabelText("归还时清Trail")]
        public bool clearTrailsOnReturn = true;

        [LabelText("默认自动归还")]
        public bool defaultAutoReturn;

        [ShowIf(nameof(defaultAutoReturn))]
        [LabelText("默认自动归还时间")]
        public float defaultAutoReturnDelay = 2f;
    }

    public sealed class ESPooledGameObject : MonoBehaviour
    {
        [ShowInInspector, ReadOnly, LabelText("池Key")]
        public string PoolKey { get; private set; }

        [ShowInInspector, ReadOnly, LabelText("源Prefab")]
        public GameObject SourcePrefab { get; private set; }

        [ShowInInspector, ReadOnly, LabelText("已借出")]
        public bool IsSpawned { get; private set; }

        [ShowInInspector, ReadOnly, LabelText("版本")]
        public int Version { get; private set; }

        [ShowInInspector, ReadOnly, LabelText("自动归还")]
        public bool AutoReturnEnabled { get; private set; }

        [ShowInInspector, ReadOnly, LabelText("自动归还时间")]
        public float AutoReturnDelay { get; private set; }

        private ESGameObjectPoolModule owner;
        private ESGenericLife genericLife;
        private float returnAtTime;
        private bool dispatchingPoolSpawn;
        private bool returnRequestedDuringPoolSpawn;

        public void Bind(ESGameObjectPoolModule ownerModule, string key, GameObject prefab)
        {
            owner = ownerModule;
            PoolKey = key;
            SourcePrefab = prefab;
            genericLife = ESGenericLife.EnsureForPooledRoot(gameObject);
        }

        internal bool IsBoundTo(ESGameObjectPoolModule ownerModule)
        {
            return ReferenceEquals(owner, ownerModule);
        }

        internal bool HasPoolOwner => owner != null;

        internal MonoBehaviour PoolRootLifecycleComponent =>
            genericLife != null ? genericLife.PoolRootLifecycleComponent : null;

        /// <summary>
        /// Ends this bridge's relationship with a pool after that pool has completed its
        /// Despawn edge and removed the instance from its accounting. A detached object is no
        /// longer allowed to request a return through a stale pool owner.
        /// </summary>
        internal void UnbindFromPool()
        {
            MarkPushToPool();
            owner = null;
            PoolKey = null;
            SourcePrefab = null;
            dispatchingPoolSpawn = false;
        }

        public void MarkGetInPool(bool autoReturn, float delay)
        {
            IsSpawned = true;
            Version++;
            returnRequestedDuringPoolSpawn = false;
            AutoReturnEnabled = autoReturn;
            AutoReturnDelay = Mathf.Max(0f, delay);
            returnAtTime = AutoReturnEnabled ? Time.time + AutoReturnDelay : 0f;
        }

        public void MarkPushToPool()
        {
            IsSpawned = false;
            returnRequestedDuringPoolSpawn = false;
            AutoReturnEnabled = false;
            AutoReturnDelay = 0f;
            returnAtTime = 0f;
            Version++;
        }

        public void RequestPushToPool()
        {
            if (TryDeferReturnDuringPoolSpawn())
                return;

            owner?.PushToPool(gameObject);
        }

        internal bool NotifyPoolSpawned()
        {
            dispatchingPoolSpawn = true;
            try
            {
                return genericLife == null || genericLife.NotifyPoolSpawned();
            }
            finally
            {
                dispatchingPoolSpawn = false;
            }
        }

        internal bool NotifyPoolDespawned()
        {
            return genericLife == null || genericLife.NotifyPoolDespawned();
        }

        internal bool TryDeferReturnDuringPoolSpawn()
        {
            if (!dispatchingPoolSpawn)
                return false;

            returnRequestedDuringPoolSpawn = true;
            return true;
        }

        internal bool ConsumeDeferredReturnAfterPoolSpawn()
        {
            if (!returnRequestedDuringPoolSpawn)
                return false;

            returnRequestedDuringPoolSpawn = false;
            return true;
        }

        private void Update()
        {
            if (!AutoReturnEnabled || !IsSpawned || Time.time < returnAtTime)
                return;

            RequestPushToPool();
        }
    }

    internal sealed class ESGameObjectPoolGroup
    {
        public string key;
        public GameObject prefab;
        public Transform poolRoot;
        public ESGameObjectPoolConfig config;

        public readonly Queue<GameObject> inactive = new Queue<GameObject>(32);
        public readonly HashSet<GameObject> active = new HashSet<GameObject>();

        public int createdCount;
        public int rentCount;
        public int returnCount;
        public int missCount;
        public int repairCount;
        public int overflowDestroyCount;
        public int spawnDispatchDepth;
        public bool isTerminating;
        public bool clearRequested;
        public bool releaseWhenCleared;
        public bool destroyActiveWhenExclusiveRequested;

        public readonly Dictionary<object, int> prewarmSources = new Dictionary<object, int>(4);

        public int ActiveCount => active.Count;
        public int InactiveCount => inactive.Count;
        public int TotalCount => active.Count + inactive.Count;
        public int PrewarmSourceCount => prewarmSources.Count;
        public bool requiresExplicitKey;
    }

    public struct ESGameObjectPoolStats
    {
        public string key;
        public int activeCount;
        public int inactiveCount;
        public int totalCount;
        public int createdCount;
        public int rentCount;
        public int returnCount;
        public int missCount;
        public int repairCount;
        public int overflowDestroyCount;
        public int prewarmSourceCount;
    }

    internal readonly struct ESGameObjectPoolPrewarmScope : IEquatable<ESGameObjectPoolPrewarmScope>
    {
        public readonly string sceneName;
        public readonly string spaceName;

        public ESGameObjectPoolPrewarmScope(string sceneName, string spaceName)
        {
            this.sceneName = sceneName ?? string.Empty;
            this.spaceName = spaceName ?? string.Empty;
        }

        public bool Equals(ESGameObjectPoolPrewarmScope other)
        {
            return string.Equals(sceneName, other.sceneName, StringComparison.Ordinal)
                && string.Equals(spaceName, other.spaceName, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is ESGameObjectPoolPrewarmScope other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((sceneName != null ? sceneName.GetHashCode() : 0) * 397)
                    ^ (spaceName != null ? spaceName.GetHashCode() : 0);
            }
        }
    }

    internal sealed class ESGameObjectPoolAsyncPrewarmContext : IDisposable
    {
        public readonly ESAssetScope assetScope;
        public readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        public readonly Dictionary<PrefabPrewarmEntry, GameObject> loadedPrefabs = new Dictionary<PrefabPrewarmEntry, GameObject>();
        public bool ready;

        public ESGameObjectPoolAsyncPrewarmContext(ESAssetScope scope)
        {
            assetScope = scope;
        }

        public void Dispose()
        {
            cancellation.Cancel();
            cancellation.Dispose();
            assetScope.Dispose();
        }
    }

    [Serializable]
    [TypeRegistryItem("GameObject对象池模块")]
    public sealed class ESGameObjectPoolModule : ESRuntimeModule
    {
        private const int DefaultGroupCapacity = 64;

        [LabelText("默认配置")]
        public ESGameObjectPoolConfig defaultConfig = new ESGameObjectPoolConfig();

        [Title("Prefab预热入口")]
        [LabelText("预热配置列表")]
        public List<PrefabPrewarmDataInfo> prewarmSources = new List<PrefabPrewarmDataInfo>(8);

        [LabelText("Start时自动预热当前场景")]
        public bool loadPrewarmOnStart = true;

        [LabelText("监听场景加载并自动预热")]
        public bool autoLoadOnSceneLoaded = true;

        [LabelText("场景卸载时自动释放预热")]
        public bool unloadPrewarmOnSceneUnloaded = true;

        [LabelText("当前Space")]
        public string currentSpaceName;

        [LabelText("自动修补间隔")]
        public float autoRepairInterval = 0.5f;

        [ShowInInspector, ReadOnly, LabelText("池组数量")]
        public int GroupCount => groupsByKey != null ? groupsByKey.Count : 0;

        private readonly Dictionary<string, ESGameObjectPoolGroup> groupsByKey = new Dictionary<string, ESGameObjectPoolGroup>(DefaultGroupCapacity);
        private readonly Dictionary<GameObject, ESGameObjectPoolGroup> groupsByPrefab = new Dictionary<GameObject, ESGameObjectPoolGroup>(DefaultGroupCapacity);
        private readonly List<ESGameObjectPoolGroup> groupBuffer = new List<ESGameObjectPoolGroup>(DefaultGroupCapacity);
        private readonly List<ParticleSystem> particleBuffer = new List<ParticleSystem>(16);
        private readonly List<TrailRenderer> trailBuffer = new List<TrailRenderer>(8);
        private readonly Dictionary<PrefabPrewarmDataInfo, HashSet<ESGameObjectPoolPrewarmScope>> loadedPrewarmScopes = new Dictionary<PrefabPrewarmDataInfo, HashSet<ESGameObjectPoolPrewarmScope>>(16);
        private readonly Dictionary<PrefabPrewarmDataInfo, Dictionary<ESGameObjectPoolPrewarmScope, ESGameObjectPoolAsyncPrewarmContext>> asyncPrewarmContexts = new Dictionary<PrefabPrewarmDataInfo, Dictionary<ESGameObjectPoolPrewarmScope, ESGameObjectPoolAsyncPrewarmContext>>(16);

        private Transform root;
        private float nextRepairTime;
        private bool sceneEventsSubscribed;
        private int activeSpawnDispatchCount;
        private bool isClearingAll;
        private bool clearAllRequested;

        public override void Start()
        {
            EnsureRoot();
            EnsureSceneEvents();

            if (loadPrewarmOnStart)
                LoadConfiguredPrewarmForCurrentScene();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            EnsureSceneEvents();
        }

        protected override void OnDisable()
        {
            RemoveSceneEvents();
            base.OnDisable();
        }

        protected override void Update()
        {
            if (autoRepairInterval <= 0f || Time.time < nextRepairTime)
                return;

            nextRepairTime = Time.time + autoRepairInterval;
            AutoRepairAll();
        }

        public GameObject GetInPool(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            if (prefab == null || isClearingAll || clearAllRequested)
                return null;

            if (!TryGetAccessibleGroup(prefab, null, null, out ESGameObjectPoolGroup group))
                return null;
            return GetFromGroup(
                group,
                position,
                rotation,
                parent,
                group.config.defaultAutoReturn,
                group.config.defaultAutoReturnDelay);
        }

        public GameObject GetInPool(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent, bool autoReturn, float autoReturnDelay)
        {
            if (prefab == null || isClearingAll || clearAllRequested)
                return null;

            if (!TryGetAccessibleGroup(prefab, null, null, out ESGameObjectPoolGroup group))
                return null;
            return GetFromGroup(group, position, rotation, parent, autoReturn, autoReturnDelay);
        }

        internal GameObject Internal_GetInPool(
            GameObject prefab,
            Vector3 position,
            Quaternion rotation,
            Transform parent,
            bool autoReturn,
            float autoReturnDelay,
            out MonoBehaviour poolRootLifecycle)
        {
            poolRootLifecycle = null;
            if (prefab == null || isClearingAll || clearAllRequested)
                return null;

            if (!TryGetAccessibleGroup(prefab, null, null, out ESGameObjectPoolGroup group))
                return null;
            return GetFromGroup(
                group,
                position,
                rotation,
                parent,
                autoReturn,
                autoReturnDelay,
                out poolRootLifecycle);
        }

        public GameObject GetInPool(string key, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            if (string.IsNullOrEmpty(key) || !groupsByKey.TryGetValue(key, out ESGameObjectPoolGroup group))
                return null;

            return GetFromGroup(
                group,
                position,
                rotation,
                parent,
                group.config.defaultAutoReturn,
                group.config.defaultAutoReturnDelay);
        }

        public GameObject GetInPool(string key, Vector3 position, Quaternion rotation, Transform parent, bool autoReturn, float autoReturnDelay)
        {
            if (string.IsNullOrEmpty(key) || !groupsByKey.TryGetValue(key, out ESGameObjectPoolGroup group))
                return null;

            return GetFromGroup(group, position, rotation, parent, autoReturn, autoReturnDelay);
        }

        public void Register(GameObject prefab, string key = null, ESGameObjectPoolConfig config = null)
        {
            if (prefab == null || isClearingAll || clearAllRequested)
                return;

            TryGetAccessibleGroup(prefab, key, config, out _);
        }

        /// <summary>
        /// Registers a prefab under one explicit owner key without silently joining a group
        /// already owned by another subsystem. Use this when the caller later clears/releases
        /// the group as part of its own lifecycle.
        /// </summary>
        public bool TryRegister(GameObject prefab, string key, out string error, ESGameObjectPoolConfig config = null)
        {
            if (prefab == null)
            {
                error = "对象池注册缺少 Prefab。";
                return false;
            }

            if (string.IsNullOrWhiteSpace(key))
            {
                error = "对象池显式注册需要非空 Key。";
                return false;
            }

            if (isClearingAll || clearAllRequested)
            {
                error = "对象池正在清理，不能注册新组。";
                return false;
            }

            if (groupsByPrefab.TryGetValue(prefab, out ESGameObjectPoolGroup groupByPrefab)
                && (!groupByPrefab.requiresExplicitKey
                    || !string.Equals(groupByPrefab.key, key, StringComparison.Ordinal)))
            {
                error = groupByPrefab.requiresExplicitKey
                    ? "Prefab 已属于对象池组 '" + groupByPrefab.key + "'，不能再以 '" + key + "' 注册。"
                    : "Prefab 已被未隔离的对象池组 '" + groupByPrefab.key + "' 使用，不能转为 UI 等独占组。";
                return false;
            }

            if (groupsByKey.TryGetValue(key, out ESGameObjectPoolGroup groupByKey)
                && (!ReferenceEquals(groupByKey.prefab, prefab) || !groupByKey.requiresExplicitKey))
            {
                error = ReferenceEquals(groupByKey.prefab, prefab)
                    ? "对象池 Key 已被未隔离组使用，不能接管为独占组：" + key
                    : "对象池 Key 已属于其他 Prefab：" + key;
                return false;
            }

            ESGameObjectPoolGroup group = GetOrCreateGroup(prefab, key, config);
            group.requiresExplicitKey = true;
            error = null;
            return true;
        }

        public bool TryGetStats(string key, out ESGameObjectPoolStats stats)
        {
            if (!string.IsNullOrEmpty(key) && groupsByKey.TryGetValue(key, out ESGameObjectPoolGroup group))
            {
                stats = BuildStats(group);
                return true;
            }

            stats = default;
            return false;
        }

        public bool TryGetStats(GameObject prefab, out ESGameObjectPoolStats stats)
        {
            if (prefab != null && groupsByPrefab.TryGetValue(prefab, out ESGameObjectPoolGroup group))
            {
                stats = BuildStats(group);
                return true;
            }

            stats = default;
            return false;
        }

        public void Prewarm(GameObject prefab, int count, string key = null, ESGameObjectPoolConfig config = null)
        {
            if (prefab == null || count <= 0 || isClearingAll || clearAllRequested)
                return;

            if (!TryGetAccessibleGroup(prefab, key, config, out ESGameObjectPoolGroup group))
                return;
            CreateInactive(group, count);
        }

        internal void PrewarmOwned(GameObject prefab, int count, object source, string key = null, ESGameObjectPoolConfig config = null)
        {
            if (prefab == null || count <= 0 || source == null || isClearingAll || clearAllRequested)
                return;
            if (!TryGetAccessibleGroup(prefab, key, config, out ESGameObjectPoolGroup group))
                return;
            if (IsGroupTerminationPending(group))
                return;

            AddPrewarmSource(group, source, count);
            CreateInactive(group, count);
        }

        internal void ReleaseOwnedPrewarm(GameObject prefab, int count, object source, string key = null, bool clearExclusiveInactive = true)
        {
            ESGameObjectPoolGroup group = ResolveGroup(prefab, key);
            if (group == null || source == null)
                return;
            RemovePrewarmSource(group, source, count);
            if (clearExclusiveInactive && group.PrewarmSourceCount == 0)
            {
                ClearExclusiveGroup(group, false);
                RemoveGroupIfUnused(group);
            }
        }

        public async UniTask<GameObject> PrewarmAsync(
            ESAssetReferPrefabConfigKey prefabKey,
            ESAssetScope assetScope,
            int count,
            string key = null,
            ESGameObjectPoolConfig config = null,
            CancellationToken cancellationToken = default)
        {
            if (prefabKey == null) throw new ArgumentNullException(nameof(prefabKey));
            if (assetScope == null) throw new ArgumentNullException(nameof(assetScope));
            if (prefabKey.EnumKeyInt == 0 && string.IsNullOrEmpty(prefabKey.StringKey))
                throw new InvalidOperationException("[ESRes][Prewarm] Prefab ConfigKey 缺少 EnumKey/StringKey。");
            if (!prefabKey.HasGuid)
                throw new InvalidOperationException("[ESRes][Prewarm] Prefab ConfigKey 尚未解析到 GUID，请先在资源注册表完成登记。");

            var refer = new ESAssetReferPrefab();
            refer.InitializeGeneratedReference(
                prefabKey.guid,
                prefabKey.localFileId,
                ESAssetReferKind.Prefab,
                prefabKey.EnumKeyInt,
                prefabKey.StringKey);

            GameObject prefab = await assetScope.LoadAsync(refer, cancellationToken);
            if (prefab == null)
                throw new InvalidOperationException("[ESRes][Prewarm] Prefab ConfigKey 加载结果为空。");

            Prewarm(prefab, count, key, config);
            return prefab;
        }

        public void Prewarm(PrefabPrewarmDataInfo dataInfo)
        {
            LoadPrewarmForCurrentScene(dataInfo);
        }

        public void Prewarm(PrefabPrewarmDataInfo dataInfo, string sceneName)
        {
            LoadPrewarmForScene(dataInfo, sceneName, currentSpaceName);
        }

        public void Prewarm(PrefabPrewarmDataInfo dataInfo, string sceneName, string spaceName)
        {
            PrewarmAsync(dataInfo, sceneName, spaceName).Forget();
        }

        public UniTask<bool> PrewarmAsync(PrefabPrewarmDataInfo dataInfo, string sceneName, string spaceName, CancellationToken cancellationToken = default)
        {
            return LoadPrewarmForSceneAsync(dataInfo, sceneName, spaceName, cancellationToken);
        }

        public void PrewarmForCurrentScene(PrefabPrewarmDataInfo dataInfo)
        {
            LoadPrewarmForCurrentScene(dataInfo);
        }

        public void PrewarmForScene(PrefabPrewarmDataInfo dataInfo, string sceneName)
        {
            LoadPrewarmForScene(dataInfo, sceneName);
        }

        public bool LoadPrewarmForCurrentScene(PrefabPrewarmDataInfo dataInfo)
        {
            return LoadPrewarmForScene(dataInfo, SceneManager.GetActiveScene().name, currentSpaceName);
        }

        public bool LoadPrewarmForScene(PrefabPrewarmDataInfo dataInfo, string sceneName)
        {
            return LoadPrewarmForScene(dataInfo, sceneName, currentSpaceName);
        }

        public bool LoadPrewarmForScene(PrefabPrewarmDataInfo dataInfo, string sceneName, string spaceName)
        {
            if (dataInfo == null || string.IsNullOrEmpty(sceneName) || !dataInfo.Supports(sceneName, spaceName))
                return false;
            ESGameObjectPoolPrewarmScope scope = new ESGameObjectPoolPrewarmScope(sceneName, spaceName);
            if (TryGetAsyncPrewarmContext(dataInfo, scope, out _))
                return false;
            LoadPrewarmForSceneAsync(dataInfo, sceneName, spaceName).Forget();
            return true;
        }

        public async UniTask<bool> LoadPrewarmForSceneAsync(PrefabPrewarmDataInfo dataInfo, string sceneName, string spaceName, CancellationToken cancellationToken = default)
        {
            if (dataInfo == null || dataInfo.entries == null || string.IsNullOrEmpty(sceneName) || !dataInfo.Supports(sceneName, spaceName))
                return false;

            ESGameObjectPoolPrewarmScope scopeKey = new ESGameObjectPoolPrewarmScope(sceneName, spaceName);
            if (TryGetAsyncPrewarmContext(dataInfo, scopeKey, out _))
                return false;

            var context = new ESGameObjectPoolAsyncPrewarmContext(ESAssets.CreateScope());
            GetAsyncPrewarmContextMap(dataInfo).Add(scopeKey, context);
            using (CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, context.cancellation.Token))
            {
                try
                {
                    for (int i = 0; i < dataInfo.entries.Count; i++)
                    {
                        PrefabPrewarmEntry entry = dataInfo.entries[i];
                        if (entry == null || !entry.enabled || entry.prefabKey == null || entry.prewarmCount <= 0)
                            continue;

                        ESGameObjectPoolConfig config = entry.useCustomConfig ? entry.config : defaultConfig;
                        GameObject prefab = await PrewarmAsync(entry.prefabKey, context.assetScope, entry.prewarmCount, entry.key, config, linked.Token);
                        context.loadedPrefabs[entry] = prefab;
                        ESGameObjectPoolGroup group = ResolveGroup(prefab, entry.key);
                        AddPrewarmSource(group, dataInfo, entry.prewarmCount);
                    }

                    context.ready = true;
                    GetLoadedScopeSet(dataInfo).Add(scopeKey);
                    return true;
                }
                catch (OperationCanceledException)
                {
                    RollbackAsyncPrewarm(dataInfo, scopeKey, context, true);
                    return false;
                }
                catch (Exception exception)
                {
                    Debug.LogError($"[ESRes][Prewarm] Prefab 预热失败：Data={dataInfo.name}, Scene={sceneName}, Space={spaceName}, Error={exception.Message}", dataInfo);
                    RollbackAsyncPrewarm(dataInfo, scopeKey, context, true);
                    return false;
                }
            }
        }

        public void ReleasePrewarm(PrefabPrewarmDataInfo dataInfo, bool clearExclusiveInactive = true)
        {
            ReleasePrewarm(dataInfo, clearExclusiveInactive, false);
        }

        public void ReleasePrewarm(PrefabPrewarmDataInfo dataInfo, bool clearExclusiveInactive, bool destroyActiveIfExclusive)
        {
            if (dataInfo == null || dataInfo.entries == null)
                return;

            int count = dataInfo.entries.Count;
            for (int i = 0; i < count; i++)
            {
                PrefabPrewarmEntry entry = dataInfo.entries[i];
                if (entry == null || entry.prefabKey == null)
                    continue;

                ESGameObjectPoolGroup group = ResolveGroup(ResolvePrewarmedPrefab(dataInfo, entry), entry.key);
                if (group == null)
                    continue;

                RemovePrewarmSource(group, dataInfo, entry.prewarmCount);
                if (clearExclusiveInactive && group.PrewarmSourceCount == 0)
                {
                    ClearExclusiveGroup(group, destroyActiveIfExclusive);
                    RemoveGroupIfUnused(group);
                }
            }
        }

        public void ReleasePrewarmForCurrentScene(PrefabPrewarmDataInfo dataInfo, bool clearExclusiveInactive = true)
        {
            UnloadPrewarmForCurrentScene(dataInfo, clearExclusiveInactive);
        }

        public void ReleasePrewarmForScene(PrefabPrewarmDataInfo dataInfo, string sceneName, bool clearExclusiveInactive = true)
        {
            UnloadPrewarmForScene(dataInfo, sceneName, clearExclusiveInactive);
        }

        public bool UnloadPrewarmForCurrentScene(PrefabPrewarmDataInfo dataInfo, bool clearExclusiveInactive = true)
        {
            return UnloadPrewarmForScene(dataInfo, SceneManager.GetActiveScene().name, currentSpaceName, clearExclusiveInactive);
        }

        public bool UnloadPrewarmForScene(PrefabPrewarmDataInfo dataInfo, string sceneName, bool clearExclusiveInactive = true)
        {
            return UnloadPrewarmForScene(dataInfo, sceneName, currentSpaceName, clearExclusiveInactive);
        }

        public bool UnloadPrewarmForScene(PrefabPrewarmDataInfo dataInfo, string sceneName, string spaceName, bool clearExclusiveInactive = true)
        {
            if (dataInfo == null || string.IsNullOrEmpty(sceneName))
                return false;

            ESGameObjectPoolPrewarmScope scope = new ESGameObjectPoolPrewarmScope(sceneName, spaceName);
            bool wasReady = loadedPrewarmScopes.TryGetValue(dataInfo, out HashSet<ESGameObjectPoolPrewarmScope> scopes) && scopes.Remove(scope);
            bool wasLoading = TryGetAsyncPrewarmContext(dataInfo, scope, out ESGameObjectPoolAsyncPrewarmContext context) && !context.ready;
            if (!wasReady && !wasLoading)
                return false;

            if (scopes != null && scopes.Count == 0)
                loadedPrewarmScopes.Remove(dataInfo);

            ReleasePrewarm(dataInfo, clearExclusiveInactive);
            DisposeAsyncPrewarmContext(dataInfo, scope);
            return true;
        }

        public void RegisterPrewarmSource(PrefabPrewarmDataInfo dataInfo, bool loadImmediately = false)
        {
            if (dataInfo == null)
                return;

            if (prewarmSources == null)
                prewarmSources = new List<PrefabPrewarmDataInfo>(8);

            if (!prewarmSources.Contains(dataInfo))
                prewarmSources.Add(dataInfo);

            if (loadImmediately)
                LoadPrewarmForCurrentScene(dataInfo);
        }

        public void RemovePrewarmSource(PrefabPrewarmDataInfo dataInfo, bool unloadImmediately = false)
        {
            if (dataInfo == null || prewarmSources == null)
                return;

            prewarmSources.Remove(dataInfo);

            if (unloadImmediately)
                ReleasePrewarm(dataInfo);
        }

        public void LoadConfiguredPrewarmForCurrentScene()
        {
            LoadConfiguredPrewarmForScene(SceneManager.GetActiveScene().name, currentSpaceName);
        }

        public void LoadConfiguredPrewarmForScene(string sceneName)
        {
            LoadConfiguredPrewarmForScene(sceneName, currentSpaceName);
        }

        public void LoadConfiguredPrewarmForScene(string sceneName, string spaceName)
        {
            if (prewarmSources == null || string.IsNullOrEmpty(sceneName))
                return;

            int count = prewarmSources.Count;
            for (int i = 0; i < count; i++)
                LoadPrewarmForScene(prewarmSources[i], sceneName, spaceName);
        }

        public void UnloadConfiguredPrewarmForCurrentScene(bool clearExclusiveInactive = true)
        {
            UnloadConfiguredPrewarmForScene(SceneManager.GetActiveScene().name, currentSpaceName, clearExclusiveInactive);
        }

        public void UnloadConfiguredPrewarmForScene(string sceneName, bool clearExclusiveInactive = true)
        {
            UnloadConfiguredPrewarmForScene(sceneName, currentSpaceName, clearExclusiveInactive);
        }

        public void UnloadConfiguredPrewarmForScene(string sceneName, string spaceName, bool clearExclusiveInactive = true)
        {
            if (prewarmSources == null || string.IsNullOrEmpty(sceneName))
                return;

            int count = prewarmSources.Count;
            for (int i = 0; i < count; i++)
                UnloadPrewarmForScene(prewarmSources[i], sceneName, spaceName, clearExclusiveInactive);
        }

        public void RefreshPrewarmForCurrentScene(bool clearExclusiveInactive = true)
        {
            string sceneName = SceneManager.GetActiveScene().name;
            UnloadConfiguredPrewarmForScene(sceneName, currentSpaceName, clearExclusiveInactive);
            LoadConfiguredPrewarmForScene(sceneName, currentSpaceName);
        }

        public void NotifySpaceChanged(string spaceName)
        {
            NotifySpaceChanged(spaceName, true);
        }

        public void NotifySpaceChanged(string spaceName, bool unloadOldSpace)
        {
            if (string.Equals(currentSpaceName, spaceName, StringComparison.Ordinal))
                return;

            string sceneName = SceneManager.GetActiveScene().name;
            string oldSpaceName = currentSpaceName;
            if (unloadOldSpace)
                UnloadConfiguredPrewarmForScene(sceneName, oldSpaceName, true);

            currentSpaceName = spaceName;
            LoadConfiguredPrewarmForScene(sceneName, currentSpaceName);
        }

        public bool PushToPool(GameObject instance)
        {
            if (instance == null)
                return false;

            ESPooledGameObject pooled = instance.GetComponent<ESPooledGameObject>();
            if (pooled == null || string.IsNullOrEmpty(pooled.PoolKey))
                return false;

            if (pooled.TryDeferReturnDuringPoolSpawn())
                return true;

            if (!groupsByKey.TryGetValue(pooled.PoolKey, out ESGameObjectPoolGroup group))
                return false;

            PushToGroup(group, instance, pooled);
            return true;
        }

        /// <summary>
        /// Terminates one borrowed instance while preserving Pool bookkeeping and its Despawn
        /// lifecycle. It is the safe per-instance counterpart to destroying a pooled object
        /// directly.
        /// </summary>
        public bool DestroyPooledInstance(GameObject instance)
        {
            if (!TryResolveActivePooledInstance(instance, out ESGameObjectPoolGroup group, out _))
                return false;

            TerminateActiveInstance(group, instance);
            return true;
        }

        /// <summary>
        /// Transfers one borrowed instance out of the pool after a normal Despawn reset. The
        /// caller becomes responsible for the object and may later reattach it through
        /// <see cref="TryAttachInactiveInstance"/>.
        /// </summary>
        public bool DetachPooledInstance(GameObject instance)
        {
            if (!TryResolveActivePooledInstance(instance, out ESGameObjectPoolGroup group, out ESPooledGameObject pooled))
                return false;

            group.active.Remove(instance);
            if (!ResetInstanceForReturn(group, instance, pooled) || instance == null)
            {
                group.createdCount = Mathf.Max(0, group.createdCount - 1);
                if (instance != null)
                    DiscardInstance(group, instance, false);
                return false;
            }

            group.createdCount = Mathf.Max(0, group.createdCount - 1);
            pooled.UnbindFromPool();
            return true;
        }

        /// <summary>
        /// Reattaches an instance previously detached by <see cref="DetachPooledInstance"/> as
        /// an inactive member of the exact prefab/key group. This does not borrow the object or
        /// invoke a Spawn edge; it only restores the Pool's ownership for a future reuse.
        /// </summary>
        public bool TryAttachInactiveInstance(GameObject prefab, string key, GameObject instance)
        {
            if (ReferenceEquals(instance, null)
                || !TryRegister(prefab, key, out _)
                || !groupsByKey.TryGetValue(key, out ESGameObjectPoolGroup group)
                || !ReferenceEquals(group.prefab, prefab))
            {
                return false;
            }

            ESPooledGameObject pooled = instance.GetComponent<ESPooledGameObject>();
            if (pooled == null || pooled.IsSpawned || pooled.HasPoolOwner)
                return false;

            pooled.Bind(this, group.key, group.prefab);
            group.createdCount++;
            if (!ResetInstanceForReturn(group, instance, pooled) || instance == null)
            {
                if (instance != null)
                    DiscardInstance(group, instance);
                else
                    group.createdCount = Mathf.Max(0, group.createdCount - 1);
                return false;
            }

            return StoreNewInactive(group, instance);
        }

        /// <summary>
        /// Closes the accounting record for an object Unity has already destroyed externally.
        /// No lifecycle callback is attempted because the instance is no longer a valid
        /// execution target.
        /// </summary>
        public bool NotifyPooledInstanceDestroyed(string key, GameObject instance)
        {
            if (string.IsNullOrEmpty(key)
                || ReferenceEquals(instance, null)
                || !groupsByKey.TryGetValue(key, out ESGameObjectPoolGroup group)
                || !group.active.Remove(instance))
            {
                return false;
            }

            group.createdCount = Mathf.Max(0, group.createdCount - 1);
            return true;
        }

        public void Clear(GameObject prefab)
        {
            if (prefab == null || !groupsByPrefab.TryGetValue(prefab, out ESGameObjectPoolGroup group))
                return;

            if (group.requiresExplicitKey)
            {
                Debug.LogError(
                    "[ESGameObjectPool] Explicit pool group '" + group.key
                    + "' must be cleared by its exact key.",
                    prefab);
                return;
            }

            ClearGroup(group);
        }

        public void Clear(string key)
        {
            if (string.IsNullOrEmpty(key) || !groupsByKey.TryGetValue(key, out ESGameObjectPoolGroup group))
                return;

            ClearGroup(group);
        }

        /// <summary>
        /// Terminates and removes one explicitly owned group so the pool no longer retains its
        /// Prefab or Pool root. It is reserved for a lifecycle owner that registered the group
        /// with <see cref="TryRegister"/>.
        /// </summary>
        public bool ClearAndRelease(string key)
        {
            if (string.IsNullOrEmpty(key) || !groupsByKey.TryGetValue(key, out ESGameObjectPoolGroup group))
                return false;

            if (!group.requiresExplicitKey || group.PrewarmSourceCount > 0)
                return false;

            if (group.spawnDispatchDepth > 0)
            {
                group.clearRequested = true;
                group.releaseWhenCleared = true;
                return true;
            }

            ClearGroup(group);
            RemoveGroupIfUnused(group);
            return !groupsByKey.ContainsKey(key);
        }

        public void ClearAll()
        {
            if (isClearingAll)
                return;

            if (activeSpawnDispatchCount > 0)
            {
                // ClearAll may be requested by OnPoolSpawned user code. Despawn cannot re-enter
                // that dispatch, so the terminal pass starts when the outermost Spawn completes.
                clearAllRequested = true;
                return;
            }

            ClearAllImmediate();
        }

        private void ClearAllImmediate()
        {
            clearAllRequested = false;

            isClearingAll = true;
            try
            {
                // Despawn callbacks are user code and may re-enter the pool. Snapshot groups so
                // callbacks cannot invalidate dictionary enumeration; new rents/registration are
                // rejected until the terminal pass has completed.
                groupBuffer.Clear();
                foreach (ESGameObjectPoolGroup group in groupsByKey.Values)
                    groupBuffer.Add(group);

                for (int i = 0; i < groupBuffer.Count; i++)
                    ClearGroup(groupBuffer[i]);

                groupsByKey.Clear();
                groupsByPrefab.Clear();
                loadedPrewarmScopes.Clear();
                foreach (Dictionary<ESGameObjectPoolPrewarmScope, ESGameObjectPoolAsyncPrewarmContext> contexts in asyncPrewarmContexts.Values)
                {
                    foreach (ESGameObjectPoolAsyncPrewarmContext context in contexts.Values)
                    {
                        try
                        {
                            context.Dispose();
                        }
                        catch (Exception exception)
                        {
                            // One broken resource scope must not prevent the remaining scopes and
                            // the pool hierarchy from reaching their terminal state.
                            Debug.LogException(exception);
                        }
                    }
                }
                asyncPrewarmContexts.Clear();

                // ClearAll owns the complete pool hierarchy. Releasing the root prevents empty
                // Pool_* nodes from accumulating across repeated world/session resets.
                if (root != null)
                {
                    UnityEngine.Object.Destroy(root.gameObject);
                    root = null;
                }
            }
            finally
            {
                groupBuffer.Clear();
                isClearingAll = false;
            }
        }

        public override void OnDestroy()
        {
            RemoveSceneEvents();
            ClearAll();
            base.OnDestroy();
        }

        private void EnsureSceneEvents()
        {
            if (sceneEventsSubscribed || !autoLoadOnSceneLoaded)
                return;

            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            sceneEventsSubscribed = true;
        }

        private void RemoveSceneEvents()
        {
            if (!sceneEventsSubscribed)
                return;

            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            sceneEventsSubscribed = false;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!autoLoadOnSceneLoaded)
                return;

            LoadConfiguredPrewarmForScene(scene.name, currentSpaceName);
        }

        private void OnSceneUnloaded(Scene scene)
        {
            if (!unloadPrewarmOnSceneUnloaded)
                return;

            UnloadConfiguredPrewarmForScene(scene.name, currentSpaceName, true);
        }

        private GameObject GetFromGroup(ESGameObjectPoolGroup group, Vector3 position, Quaternion rotation, Transform parent, bool autoReturn, float autoReturnDelay)
        {
            return GetFromGroup(
                group,
                position,
                rotation,
                parent,
                autoReturn,
                autoReturnDelay,
                out _);
        }

        private GameObject GetFromGroup(
            ESGameObjectPoolGroup group,
            Vector3 position,
            Quaternion rotation,
            Transform parent,
            bool autoReturn,
            float autoReturnDelay,
            out MonoBehaviour poolRootLifecycle)
        {
            poolRootLifecycle = null;
            if (group == null || IsGroupTerminationPending(group) || isClearingAll || clearAllRequested)
                return null;

            GameObject instance = null;
            while (group.inactive.Count > 0 && instance == null)
            {
                GameObject candidate = group.inactive.Dequeue();
                if (candidate == null)
                {
                    // Unity may have destroyed an inactive object externally. It is no longer a
                    // member of the group, so its capacity must not keep blocking replacement.
                    group.createdCount = Mathf.Max(0, group.createdCount - 1);
                    continue;
                }

                instance = candidate;
            }

            if (instance == null)
            {
                if (!CanCreate(group))
                    return null;

                instance = CreateInstance(group);
                if (instance == null)
                    return null;

                group.missCount++;
            }

            ESPooledGameObject pooled = instance.GetComponent<ESPooledGameObject>();
            if (pooled == null)
            {
                Debug.LogError("[ESGameObjectPool] A pooled instance lost its ESPooledGameObject bridge and will be discarded.", instance);
                DiscardInstance(group, instance);
                return null;
            }

            bool handedToCaller = false;
            // The active set is established before any operation that can invoke user or Unity
            // code. From this point every failure path has one recoverable pool record.
            group.active.Add(instance);
            try
            {
                Transform instanceTransform = instance.transform;
                instanceTransform.SetParent(parent, false);
                instanceTransform.SetPositionAndRotation(position, rotation);

                pooled.MarkGetInPool(autoReturn, autoReturnDelay);
                bool spawnSucceeded;
                BeginPoolSpawnDispatch(group);
                try
                {
                    spawnSucceeded = pooled.NotifyPoolSpawned();
                }
                finally
                {
                    EndPoolSpawnDispatch(group);
                }

                if (!spawnSucceeded)
                    return null;

                // A receiver may request return during OnPoolSpawned. It is deferred until all
                // receivers have finished, so Despawn never re-enters the active Spawn dispatch.
                if (pooled.ConsumeDeferredReturnAfterPoolSpawn())
                {
                    PushToGroup(group, instance, pooled);
                    return null;
                }

                // Clear/ClearAll requested inside Spawn is terminal for this borrow. It is flushed
                // only after Spawn dispatch has unwound, so Despawn never re-enters Spawn.
                TryFlushPendingTermination(group);
                if (IsGroupTerminationPending(group) || !pooled.IsSpawned || !group.active.Contains(instance))
                    return null;

                // A callback is allowed to request a return. In that case the nested return has
                // already completed and this caller must not reactivate or hand out the instance.
                instance.SetActive(true);
                if (!pooled.IsSpawned || !group.active.Contains(instance))
                    return null;

                poolRootLifecycle = pooled.PoolRootLifecycleComponent;
                handedToCaller = true;
                group.rentCount++;
                return instance;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, instance);
                return null;
            }
            finally
            {
                // Pool callbacks and activation must never leave an instance untracked. A nested
                // return removes it from active itself; otherwise this finally path closes it.
                if (!handedToCaller && group.active.Contains(instance))
                    ReturnFailedSpawnToTerminalState(group, instance, pooled);

                TryFlushPendingTermination(group);
            }
        }

        private void PushToGroup(ESGameObjectPoolGroup group, GameObject instance, ESPooledGameObject pooled)
        {
            if (!pooled.IsSpawned || !group.active.Remove(instance))
                return;

            bool resetSucceeded = ResetInstanceForReturn(group, instance, pooled);
            group.returnCount++;
            if (resetSucceeded)
            {
                StoreInactiveOrDiscard(group, instance, pooled);
                return;
            }

            Debug.LogError("[ESGameObjectPool] Pool Despawn or return reset failed; the instance was discarded and will not be reused.", instance);
            DiscardInstance(group, instance);
        }

        private bool TryResolveActivePooledInstance(
            GameObject instance,
            out ESGameObjectPoolGroup group,
            out ESPooledGameObject pooled)
        {
            group = null;
            pooled = null;
            if (instance == null)
                return false;

            pooled = instance.GetComponent<ESPooledGameObject>();
            if (pooled == null
                || !pooled.IsSpawned
                || !pooled.IsBoundTo(this)
                || string.IsNullOrEmpty(pooled.PoolKey)
                || !groupsByKey.TryGetValue(pooled.PoolKey, out group)
                || !group.active.Contains(instance))
            {
                group = null;
                pooled = null;
                return false;
            }

            return true;
        }

        private bool TryGetAccessibleGroup(
            GameObject prefab,
            string key,
            ESGameObjectPoolConfig config,
            out ESGameObjectPoolGroup group)
        {
            if (!string.IsNullOrEmpty(key)
                && groupsByKey.TryGetValue(key, out ESGameObjectPoolGroup groupByKey)
                && !ReferenceEquals(groupByKey.prefab, prefab))
            {
                Debug.LogError(
                    "[ESGameObjectPool] Pool key '" + key
                    + "' already belongs to another prefab and cannot be shared implicitly.",
                    prefab);
                group = null;
                return false;
            }

            if (groupsByPrefab.TryGetValue(prefab, out group)
                && group.requiresExplicitKey
                && !string.Equals(group.key, key, StringComparison.Ordinal))
            {
                Debug.LogError(
                    "[ESGameObjectPool] This prefab belongs to explicit pool group '"
                    + group.key + "'. Borrow or configure it with that exact key.",
                    prefab);
                group = null;
                return false;
            }

            group = GetOrCreateGroup(prefab, key, config);
            return true;
        }

        private ESGameObjectPoolGroup GetOrCreateGroup(GameObject prefab, string key, ESGameObjectPoolConfig config)
        {
            if (groupsByPrefab.TryGetValue(prefab, out ESGameObjectPoolGroup group))
                return group;

            string useKey = !string.IsNullOrEmpty(key) ? key : BuildPrefabKey(prefab);
            if (groupsByKey.TryGetValue(useKey, out group))
            {
                groupsByPrefab[prefab] = group;
                return group;
            }

            EnsureRoot();
            group = new ESGameObjectPoolGroup
            {
                key = useKey,
                prefab = prefab,
                config = CloneConfig(config ?? defaultConfig),
                poolRoot = new GameObject($"Pool_{useKey}").transform
            };
            group.poolRoot.SetParent(root, false);
            groupsByKey.Add(useKey, group);
            groupsByPrefab.Add(prefab, group);
            return group;
        }

        private void CreateInactive(ESGameObjectPoolGroup group, int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (!CanCreate(group))
                    return;

                GameObject instance = CreateInstance(group);
                if (instance == null)
                    continue;

                StoreNewInactive(group, instance);
            }
        }

        private GameObject CreateInstance(ESGameObjectPoolGroup group)
        {
            GameObject instance = null;
            try
            {
                instance = UnityEngine.Object.Instantiate(group.prefab);
                instance.name = $"{group.prefab.name}_Pooled";
                // Awake may already have run during Instantiate; Unity does not offer a way to defer
                // it. Pool contracts therefore forbid pool-dependent work in Awake. All Pool work
                // starts only after this explicit inactive baseline has been established.
                instance.SetActive(false);
                ESPooledGameObject pooled = instance.GetComponent<ESPooledGameObject>();
                if (pooled == null)
                    pooled = instance.AddComponent<ESPooledGameObject>();

                pooled.Bind(this, group.key, group.prefab);
                if (!ResetInstanceForReturn(group, instance, pooled))
                {
                    Debug.LogError("[ESGameObjectPool] New instance failed its inactive Despawn baseline and was discarded.", instance);
                    DiscardInstance(group, instance, false);
                    return null;
                }

                group.createdCount++;
                return instance;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, instance);
                if (instance != null)
                    DiscardInstance(group, instance, false);

                return null;
            }
        }

        private bool CanCreate(ESGameObjectPoolGroup group)
        {
            if (group == null || group.prefab == null || IsGroupTerminationPending(group) || isClearingAll || clearAllRequested)
                return false;

            if (group.config.maxTotalCount > 0 && group.TotalCount >= group.config.maxTotalCount)
                return false;

            return group.config.allowExpand || group.createdCount < group.config.prewarmCount;
        }

        private void AutoRepairAll()
        {
            foreach (KeyValuePair<string, ESGameObjectPoolGroup> pair in groupsByKey)
            {
                ESGameObjectPoolGroup group = pair.Value;
                if (group == null || group.config == null || !group.config.autoRepair)
                    continue;

                int target = Mathf.Max(group.config.repairInactiveTarget, group.config.prewarmCount);
                int need = target - group.inactive.Count;
                if (need <= 0)
                    continue;

                CreateInactive(group, need);
                group.repairCount += need;
            }
        }

        private bool ResetInstanceForReturn(ESGameObjectPoolGroup group, GameObject instance, ESPooledGameObject pooled)
        {
            try
            {
                if (!pooled.NotifyPoolDespawned())
                    return false;

                Rigidbody body = instance.GetComponent<Rigidbody>();
                if (body != null)
                {
                    body.velocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }

                if (group.config.stopParticlesOnReturn)
                {
                    particleBuffer.Clear();
                    instance.GetComponentsInChildren(true, particleBuffer);
                    for (int i = 0; i < particleBuffer.Count; i++)
                        particleBuffer[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }

                if (group.config.clearTrailsOnReturn)
                {
                    trailBuffer.Clear();
                    instance.GetComponentsInChildren(true, trailBuffer);
                    for (int i = 0; i < trailBuffer.Count; i++)
                        trailBuffer[i].Clear();
                }

                if (group.config.clearParentOnReturn)
                    instance.transform.SetParent(group.poolRoot, false);

                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, instance);
                return false;
            }
            finally
            {
                // Pool bookkeeping is authoritative even when a receiver fails. Old tags, handles
                // and auto-return timers can no longer claim this instance as spawned afterwards.
                pooled.MarkPushToPool();
            }
        }

        private void ReturnFailedSpawnToTerminalState(ESGameObjectPoolGroup group, GameObject instance, ESPooledGameObject pooled)
        {
            group.active.Remove(instance);
            if (ResetInstanceForReturn(group, instance, pooled))
            {
                StoreInactiveOrDiscard(group, instance, pooled);
                return;
            }

            Debug.LogError("[ESGameObjectPool] Spawn compensation Despawn failed; the instance was discarded and will not be reused.", instance);
            DiscardInstance(group, instance);
        }

        private bool StoreNewInactive(ESGameObjectPoolGroup group, GameObject instance)
        {
            try
            {
                instance.SetActive(false);
                instance.transform.SetParent(group.poolRoot, false);
                group.inactive.Enqueue(instance);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, instance);
                DiscardInstance(group, instance);
                return false;
            }
        }

        private void StoreInactiveOrDiscard(ESGameObjectPoolGroup group, GameObject instance, ESPooledGameObject pooled)
        {
            if (instance == null)
                return;

            try
            {
                if (group.isTerminating)
                {
                    DiscardInstance(group, instance);
                    return;
                }

                if (group.config.destroyOverflow && group.inactive.Count >= group.config.maxInactiveCount)
                {
                    group.overflowDestroyCount++;
                    DiscardInstance(group, instance);
                    return;
                }

                // MarkPushToPool is idempotent and is repeated here only if a Unity-side reset
                // failed before its finally block could be observed by a custom implementation.
                if (pooled.IsSpawned)
                    pooled.MarkPushToPool();

                instance.SetActive(false);
                instance.transform.SetParent(group.poolRoot, false);
                group.inactive.Enqueue(instance);
            }
            catch (Exception exception)
            {
                // Destroying is safer than retaining an object that is in neither active nor
                // inactive tracking. The created count remains exact for the surviving group.
                Debug.LogException(exception, instance);
                DiscardInstance(group, instance);
            }
        }

        private static void DiscardInstance(ESGameObjectPoolGroup group, GameObject instance, bool wasCounted = true)
        {
            if (instance == null)
                return;

            group?.active.Remove(instance);
            if (wasCounted && group != null)
                group.createdCount = Mathf.Max(0, group.createdCount - 1);

            try
            {
                // Destroy is deferred by Unity. A terminally discarded object must stop running
                // immediately after its Despawn edge instead of remaining active until end of frame.
                if (instance.activeSelf)
                    instance.SetActive(false);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, instance);
            }

            UnityEngine.Object.Destroy(instance);
        }

        private void TerminateActiveInstance(ESGameObjectPoolGroup group, GameObject instance)
        {
            if (group == null || !group.active.Remove(instance))
                return;

            // A Unity object may already have been destroyed externally while its managed
            // reference is still present in the active set. Its pool count still has to close.
            if (instance == null)
            {
                group.createdCount = Mathf.Max(0, group.createdCount - 1);
                return;
            }

            ESPooledGameObject pooled = instance.GetComponent<ESPooledGameObject>();
            if (pooled == null)
            {
                Debug.LogError("[ESGameObjectPool] An active instance lost its ESPooledGameObject bridge during termination; Despawn could not be dispatched.", instance);
            }
            else if (pooled.IsSpawned && !ResetInstanceForReturn(group, instance, pooled))
            {
                Debug.LogError("[ESGameObjectPool] Active instance termination Despawn failed; the instance will still be destroyed.", instance);
            }

            // Termination never returns the object to inactive storage. ResetInstanceForReturn
            // already closes Spawn state in its finally block even when a receiver throws.
            DiscardInstance(group, instance);
        }

        private void TerminateAllActiveInstances(ESGameObjectPoolGroup group)
        {
            while (group != null && group.active.Count > 0)
            {
                GameObject active = null;
                HashSet<GameObject>.Enumerator enumerator = group.active.GetEnumerator();
                if (enumerator.MoveNext())
                    active = enumerator.Current;
                enumerator.Dispose();

                TerminateActiveInstance(group, active);
            }
        }

        private void ClearGroup(ESGameObjectPoolGroup group)
        {
            if (group == null || group.isTerminating)
                return;

            if (group.spawnDispatchDepth > 0)
            {
                group.clearRequested = true;
                return;
            }

            group.clearRequested = false;
            group.destroyActiveWhenExclusiveRequested = false;
            group.isTerminating = true;
            try
            {
                // Only active instances still owe a Despawn. Inactive instances have already
                // completed that lifecycle edge and must not receive it a second time.
                TerminateAllActiveInstances(group);

                while (group.inactive.Count > 0)
                {
                    GameObject inactive = group.inactive.Dequeue();
                    if (inactive != null)
                        UnityEngine.Object.Destroy(inactive);
                }

                group.createdCount = 0;
            }
            finally
            {
                group.isTerminating = false;
            }
        }

        private void EnsureRoot()
        {
            if (root != null)
                return;

            root = new GameObject("ESGameObjectPoolRoot").transform;
            if (Game != null)
                root.SetParent(Game.transform, false);
        }

        private static string BuildPrefabKey(GameObject prefab)
        {
            return prefab != null ? $"prefab:{prefab.GetInstanceID()}" : string.Empty;
        }

        private static ESGameObjectPoolConfig CloneConfig(ESGameObjectPoolConfig source)
        {
            if (source == null)
                return new ESGameObjectPoolConfig();

            return new ESGameObjectPoolConfig
            {
                prewarmCount = Mathf.Max(0, source.prewarmCount),
                maxInactiveCount = Mathf.Max(0, source.maxInactiveCount),
                maxTotalCount = Mathf.Max(0, source.maxTotalCount),
                allowExpand = source.allowExpand,
                destroyOverflow = source.destroyOverflow,
                autoRepair = source.autoRepair,
                repairInactiveTarget = Mathf.Max(0, source.repairInactiveTarget),
                clearParentOnReturn = source.clearParentOnReturn,
                stopParticlesOnReturn = source.stopParticlesOnReturn,
                clearTrailsOnReturn = source.clearTrailsOnReturn,
                defaultAutoReturn = source.defaultAutoReturn,
                defaultAutoReturnDelay = Mathf.Max(0f, source.defaultAutoReturnDelay)
            };
        }

        private static ESGameObjectPoolStats BuildStats(ESGameObjectPoolGroup group)
        {
            return new ESGameObjectPoolStats
            {
                key = group.key,
                activeCount = group.ActiveCount,
                inactiveCount = group.InactiveCount,
                totalCount = group.TotalCount,
                createdCount = group.createdCount,
                rentCount = group.rentCount,
                returnCount = group.returnCount,
                missCount = group.missCount,
                repairCount = group.repairCount,
                overflowDestroyCount = group.overflowDestroyCount
                ,
                prewarmSourceCount = group.PrewarmSourceCount
            };
        }

        private HashSet<ESGameObjectPoolPrewarmScope> GetLoadedScopeSet(PrefabPrewarmDataInfo dataInfo)
        {
            if (loadedPrewarmScopes.TryGetValue(dataInfo, out HashSet<ESGameObjectPoolPrewarmScope> scopes))
                return scopes;

            scopes = new HashSet<ESGameObjectPoolPrewarmScope>();
            loadedPrewarmScopes.Add(dataInfo, scopes);
            return scopes;
        }

        private Dictionary<ESGameObjectPoolPrewarmScope, ESGameObjectPoolAsyncPrewarmContext> GetAsyncPrewarmContextMap(PrefabPrewarmDataInfo dataInfo)
        {
            if (asyncPrewarmContexts.TryGetValue(dataInfo, out Dictionary<ESGameObjectPoolPrewarmScope, ESGameObjectPoolAsyncPrewarmContext> contexts))
                return contexts;

            contexts = new Dictionary<ESGameObjectPoolPrewarmScope, ESGameObjectPoolAsyncPrewarmContext>();
            asyncPrewarmContexts.Add(dataInfo, contexts);
            return contexts;
        }

        private bool TryGetAsyncPrewarmContext(PrefabPrewarmDataInfo dataInfo, ESGameObjectPoolPrewarmScope scope, out ESGameObjectPoolAsyncPrewarmContext context)
        {
            context = null;
            return dataInfo != null
                && asyncPrewarmContexts.TryGetValue(dataInfo, out Dictionary<ESGameObjectPoolPrewarmScope, ESGameObjectPoolAsyncPrewarmContext> contexts)
                && contexts.TryGetValue(scope, out context);
        }

        private GameObject ResolvePrewarmedPrefab(PrefabPrewarmDataInfo dataInfo, PrefabPrewarmEntry entry)
        {
            if (dataInfo == null || entry == null || !asyncPrewarmContexts.TryGetValue(dataInfo, out Dictionary<ESGameObjectPoolPrewarmScope, ESGameObjectPoolAsyncPrewarmContext> contexts))
                return null;

            foreach (ESGameObjectPoolAsyncPrewarmContext context in contexts.Values)
                if (context.loadedPrefabs.TryGetValue(entry, out GameObject prefab) && prefab != null)
                    return prefab;
            return null;
        }

        private void DisposeAsyncPrewarmContext(PrefabPrewarmDataInfo dataInfo, ESGameObjectPoolPrewarmScope scope)
        {
            if (!TryGetAsyncPrewarmContext(dataInfo, scope, out ESGameObjectPoolAsyncPrewarmContext context))
                return;

            Dictionary<ESGameObjectPoolPrewarmScope, ESGameObjectPoolAsyncPrewarmContext> contexts = asyncPrewarmContexts[dataInfo];
            contexts.Remove(scope);
            if (contexts.Count == 0)
                asyncPrewarmContexts.Remove(dataInfo);
            context.Dispose();
        }

        private void RollbackAsyncPrewarm(PrefabPrewarmDataInfo dataInfo, ESGameObjectPoolPrewarmScope scope, ESGameObjectPoolAsyncPrewarmContext context, bool clearExclusiveInactive)
        {
            foreach (KeyValuePair<PrefabPrewarmEntry, GameObject> pair in context.loadedPrefabs)
            {
                PrefabPrewarmEntry entry = pair.Key;
                ESGameObjectPoolGroup group = ResolveGroup(pair.Value, entry.key);
                if (group == null)
                    continue;

                RemovePrewarmSource(group, dataInfo, entry.prewarmCount);
                if (clearExclusiveInactive && group.PrewarmSourceCount == 0)
                {
                    ClearExclusiveGroup(group, false);
                    RemoveGroupIfUnused(group);
                }
            }
            DisposeAsyncPrewarmContext(dataInfo, scope);
        }

        private void AddPrewarmSource(ESGameObjectPoolGroup group, object source, int count)
        {
            if (group == null || source == null)
                return;

            int addCount = Mathf.Max(0, count);
            if (group.prewarmSources.TryGetValue(source, out int oldCount))
                group.prewarmSources[source] = oldCount + addCount;
            else
                group.prewarmSources.Add(source, addCount);
        }

        private void RemovePrewarmSource(ESGameObjectPoolGroup group, object source, int count)
        {
            if (group == null || source == null)
                return;

            if (!group.prewarmSources.TryGetValue(source, out int oldCount))
                return;

            int newCount = oldCount - Mathf.Max(0, count);
            if (newCount > 0)
                group.prewarmSources[source] = newCount;
            else
                group.prewarmSources.Remove(source);
        }

        private ESGameObjectPoolGroup ResolveGroup(GameObject prefab, string key)
        {
            if (prefab != null && groupsByPrefab.TryGetValue(prefab, out ESGameObjectPoolGroup byPrefab))
                return byPrefab;

            if (!string.IsNullOrEmpty(key) && groupsByKey.TryGetValue(key, out ESGameObjectPoolGroup byKey))
                return byKey;

            return null;
        }

        private void ClearExclusiveGroup(ESGameObjectPoolGroup group, bool destroyActive)
        {
            if (group == null || group.PrewarmSourceCount > 0 || group.isTerminating)
                return;

            if (destroyActive && group.spawnDispatchDepth > 0)
            {
                group.destroyActiveWhenExclusiveRequested = true;
                return;
            }

            if (destroyActive)
                group.destroyActiveWhenExclusiveRequested = false;
            group.isTerminating = true;
            try
            {
                while (group.inactive.Count > 0)
                {
                    GameObject inactive = group.inactive.Dequeue();
                    if (inactive != null)
                        UnityEngine.Object.Destroy(inactive);
                }

                if (destroyActive)
                    TerminateAllActiveInstances(group);

                group.createdCount = group.TotalCount;
            }
            finally
            {
                group.isTerminating = false;
            }
        }

        private void BeginPoolSpawnDispatch(ESGameObjectPoolGroup group)
        {
            group.spawnDispatchDepth++;
            activeSpawnDispatchCount++;
        }

        private void EndPoolSpawnDispatch(ESGameObjectPoolGroup group)
        {
            if (group.spawnDispatchDepth <= 0 || activeSpawnDispatchCount <= 0)
            {
                Debug.LogError("[ESGameObjectPool] Pool Spawn dispatch depth became unbalanced.");
                group.spawnDispatchDepth = 0;
                activeSpawnDispatchCount = 0;
                return;
            }

            group.spawnDispatchDepth--;
            activeSpawnDispatchCount--;
        }

        private bool TryFlushPendingTermination(ESGameObjectPoolGroup group)
        {
            if (clearAllRequested)
            {
                if (activeSpawnDispatchCount > 0)
                    return false;

                ClearAllImmediate();
                return true;
            }

            if (group == null || group.spawnDispatchDepth > 0)
                return false;

            if (group.clearRequested)
            {
                ClearGroup(group);
                if (group.releaseWhenCleared)
                {
                    group.releaseWhenCleared = false;
                    RemoveGroupIfUnused(group);
                }
                return true;
            }

            if (group.destroyActiveWhenExclusiveRequested)
            {
                group.destroyActiveWhenExclusiveRequested = false;
                ClearExclusiveGroup(group, true);
                return true;
            }

            return false;
        }

        private bool IsGroupTerminationPending(ESGameObjectPoolGroup group)
        {
            return isClearingAll
                || clearAllRequested
                || (group != null
                    && (group.isTerminating || group.clearRequested || group.destroyActiveWhenExclusiveRequested));
        }

        private void RemoveGroupIfUnused(ESGameObjectPoolGroup group)
        {
            if (group == null || group.PrewarmSourceCount > 0 || group.ActiveCount > 0 || group.InactiveCount > 0)
                return;

            groupsByKey.Remove(group.key);
            if (group.prefab != null)
                groupsByPrefab.Remove(group.prefab);

            if (group.poolRoot != null)
                UnityEngine.Object.Destroy(group.poolRoot.gameObject);
        }
    }
}
