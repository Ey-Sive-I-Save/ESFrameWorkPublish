using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ES
{
    public interface IESGameObjectPoolResettable
    {
        void OnGetInPool();
        void OnPushToPool();
    }

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
        private float returnAtTime;

        public void Bind(ESGameObjectPoolModule ownerModule, string key, GameObject prefab)
        {
            owner = ownerModule;
            PoolKey = key;
            SourcePrefab = prefab;
        }

        public void MarkGetInPool(bool autoReturn, float delay)
        {
            IsSpawned = true;
            Version++;
            AutoReturnEnabled = autoReturn;
            AutoReturnDelay = Mathf.Max(0f, delay);
            returnAtTime = AutoReturnEnabled ? Time.time + AutoReturnDelay : 0f;
        }

        public void MarkPushToPool()
        {
            IsSpawned = false;
            AutoReturnEnabled = false;
            AutoReturnDelay = 0f;
            returnAtTime = 0f;
            Version++;
        }

        public void RequestPushToPool()
        {
            owner?.PushToPool(gameObject);
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

        public readonly Dictionary<PrefabPrewarmDataInfo, int> prewarmSources = new Dictionary<PrefabPrewarmDataInfo, int>(4);

        public int ActiveCount => active.Count;
        public int InactiveCount => inactive.Count;
        public int TotalCount => active.Count + inactive.Count;
        public int PrewarmSourceCount => prewarmSources.Count;
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
        private readonly List<ParticleSystem> particleBuffer = new List<ParticleSystem>(16);
        private readonly List<TrailRenderer> trailBuffer = new List<TrailRenderer>(8);
        private readonly List<IESGameObjectPoolResettable> resettableBuffer = new List<IESGameObjectPoolResettable>(8);
        private readonly Dictionary<PrefabPrewarmDataInfo, HashSet<ESGameObjectPoolPrewarmScope>> loadedPrewarmScopes = new Dictionary<PrefabPrewarmDataInfo, HashSet<ESGameObjectPoolPrewarmScope>>(16);

        private Transform root;
        private float nextRepairTime;
        private bool sceneEventsSubscribed;

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
            if (prefab == null)
                return null;

            ESGameObjectPoolGroup group = GetOrCreateGroup(prefab, null, null);
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
            if (prefab == null)
                return null;

            ESGameObjectPoolGroup group = GetOrCreateGroup(prefab, null, null);
            return GetFromGroup(group, position, rotation, parent, autoReturn, autoReturnDelay);
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
            if (prefab == null)
                return;

            GetOrCreateGroup(prefab, key, config);
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
            if (prefab == null || count <= 0)
                return;

            ESGameObjectPoolGroup group = GetOrCreateGroup(prefab, key, config);
            CreateInactive(group, count);
        }

        public void Prewarm(PrefabPrewarmDataInfo dataInfo)
        {
            Prewarm(dataInfo, null, null);
        }

        public void Prewarm(PrefabPrewarmDataInfo dataInfo, string sceneName)
        {
            Prewarm(dataInfo, sceneName, currentSpaceName);
        }

        public void Prewarm(PrefabPrewarmDataInfo dataInfo, string sceneName, string spaceName)
        {
            if (dataInfo == null || dataInfo.entries == null)
                return;

            if (!string.IsNullOrEmpty(sceneName) && !dataInfo.Supports(sceneName, spaceName))
                return;

            int count = dataInfo.entries.Count;
            for (int i = 0; i < count; i++)
            {
                PrefabPrewarmEntry entry = dataInfo.entries[i];
                if (entry == null || !entry.enabled || entry.prefab == null)
                    continue;

                ESGameObjectPoolConfig config = entry.useCustomConfig ? entry.config : defaultConfig;
                ESGameObjectPoolGroup group = GetOrCreateGroup(entry.prefab, entry.key, config);
                AddPrewarmSource(group, dataInfo, entry.prewarmCount);
                CreateInactive(group, entry.prewarmCount);
            }
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

            HashSet<ESGameObjectPoolPrewarmScope> scopes = GetLoadedScopeSet(dataInfo);
            ESGameObjectPoolPrewarmScope scope = new ESGameObjectPoolPrewarmScope(sceneName, spaceName);
            if (!scopes.Add(scope))
                return false;

            Prewarm(dataInfo, sceneName, spaceName);
            return true;
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
                if (entry == null || entry.prefab == null)
                    continue;

                ESGameObjectPoolGroup group = ResolveGroup(entry.prefab, entry.key);
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
            if (!loadedPrewarmScopes.TryGetValue(dataInfo, out HashSet<ESGameObjectPoolPrewarmScope> scopes) || !scopes.Remove(scope))
                return false;

            if (scopes.Count == 0)
                loadedPrewarmScopes.Remove(dataInfo);

            ReleasePrewarm(dataInfo, clearExclusiveInactive);
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

            if (!groupsByKey.TryGetValue(pooled.PoolKey, out ESGameObjectPoolGroup group))
                return false;

            PushToGroup(group, instance, pooled);
            return true;
        }

        public void Clear(GameObject prefab)
        {
            if (prefab == null || !groupsByPrefab.TryGetValue(prefab, out ESGameObjectPoolGroup group))
                return;

            ClearGroup(group);
        }

        public void Clear(string key)
        {
            if (string.IsNullOrEmpty(key) || !groupsByKey.TryGetValue(key, out ESGameObjectPoolGroup group))
                return;

            ClearGroup(group);
        }

        public void ClearAll()
        {
            foreach (KeyValuePair<string, ESGameObjectPoolGroup> pair in groupsByKey)
                ClearGroup(pair.Value);

            groupsByKey.Clear();
            groupsByPrefab.Clear();
            loadedPrewarmScopes.Clear();
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
            GameObject instance = null;
            while (group.inactive.Count > 0 && instance == null)
                instance = group.inactive.Dequeue();

            if (instance == null)
            {
                if (!CanCreate(group))
                    return null;

                instance = CreateInstance(group);
                group.missCount++;
            }

            Transform instanceTransform = instance.transform;
            instanceTransform.SetParent(parent, false);
            instanceTransform.SetPositionAndRotation(position, rotation);
            instance.SetActive(true);

            group.active.Add(instance);
            group.rentCount++;

            ESPooledGameObject pooled = instance.GetComponent<ESPooledGameObject>();
            pooled.MarkGetInPool(autoReturn, autoReturnDelay);
            NotifyGetInPool(instance);
            return instance;
        }

        private void PushToGroup(ESGameObjectPoolGroup group, GameObject instance, ESPooledGameObject pooled)
        {
            if (!pooled.IsSpawned || !group.active.Remove(instance))
                return;

            ResetInstanceForReturn(group, instance, pooled);
            group.returnCount++;

            if (group.config.destroyOverflow && group.inactive.Count >= group.config.maxInactiveCount)
            {
                group.overflowDestroyCount++;
                group.createdCount = Mathf.Max(0, group.createdCount - 1);
                UnityEngine.Object.Destroy(instance);
                return;
            }

            instance.SetActive(false);
            instance.transform.SetParent(group.poolRoot, false);
            group.inactive.Enqueue(instance);
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
                instance.SetActive(false);
                instance.transform.SetParent(group.poolRoot, false);
                group.inactive.Enqueue(instance);
            }
        }

        private GameObject CreateInstance(ESGameObjectPoolGroup group)
        {
            GameObject instance = UnityEngine.Object.Instantiate(group.prefab);
            instance.name = $"{group.prefab.name}_Pooled";
            ESPooledGameObject pooled = instance.GetComponent<ESPooledGameObject>();
            if (pooled == null)
                pooled = instance.AddComponent<ESPooledGameObject>();

            pooled.Bind(this, group.key, group.prefab);
            group.createdCount++;
            return instance;
        }

        private bool CanCreate(ESGameObjectPoolGroup group)
        {
            if (group == null || group.prefab == null)
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

        private void ResetInstanceForReturn(ESGameObjectPoolGroup group, GameObject instance, ESPooledGameObject pooled)
        {
            NotifyPushToPool(instance);
            pooled.MarkPushToPool();

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
        }

        private void NotifyGetInPool(GameObject instance)
        {
            resettableBuffer.Clear();
            instance.GetComponentsInChildren(true, resettableBuffer);
            for (int i = 0; i < resettableBuffer.Count; i++)
                resettableBuffer[i].OnGetInPool();
        }

        private void NotifyPushToPool(GameObject instance)
        {
            resettableBuffer.Clear();
            instance.GetComponentsInChildren(true, resettableBuffer);
            for (int i = 0; i < resettableBuffer.Count; i++)
                resettableBuffer[i].OnPushToPool();
        }

        private void ClearGroup(ESGameObjectPoolGroup group)
        {
            if (group == null)
                return;

            foreach (GameObject active in group.active)
            {
                if (active != null)
                    UnityEngine.Object.Destroy(active);
            }

            while (group.inactive.Count > 0)
            {
                GameObject inactive = group.inactive.Dequeue();
                if (inactive != null)
                    UnityEngine.Object.Destroy(inactive);
            }

            group.active.Clear();
            group.createdCount = 0;
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

        private void AddPrewarmSource(ESGameObjectPoolGroup group, PrefabPrewarmDataInfo source, int count)
        {
            if (group == null || source == null)
                return;

            int addCount = Mathf.Max(0, count);
            if (group.prewarmSources.TryGetValue(source, out int oldCount))
                group.prewarmSources[source] = oldCount + addCount;
            else
                group.prewarmSources.Add(source, addCount);
        }

        private void RemovePrewarmSource(ESGameObjectPoolGroup group, PrefabPrewarmDataInfo source, int count)
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
            if (group == null || group.PrewarmSourceCount > 0)
                return;

            while (group.inactive.Count > 0)
            {
                GameObject inactive = group.inactive.Dequeue();
                if (inactive != null)
                    UnityEngine.Object.Destroy(inactive);
            }

            if (destroyActive)
            {
                foreach (GameObject active in group.active)
                {
                    if (active != null)
                        UnityEngine.Object.Destroy(active);
                }

                group.active.Clear();
            }

            group.createdCount = group.TotalCount;
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
