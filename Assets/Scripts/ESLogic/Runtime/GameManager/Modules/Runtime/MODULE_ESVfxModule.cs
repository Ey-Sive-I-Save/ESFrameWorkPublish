using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    public enum ESVfxState : byte
    {
        PendingLoad,
        Playing,
        Stopping,
        Ended
    }

    public enum ESVfxEndReason : byte
    {
        None = 0,
        NaturalEnd = 1,
        ExplicitStop = 2,
        OwnerDestroyed = 3,
        OwnerDisabled = 4,
        OwnerDespawned = 5,
        Preempted = 6,
        BudgetRejected = 7,
        BackendFailure = 8,
        ModuleDisabled = 9,
        SceneChanged = 10,
        ProviderTransition = 11,
        ResourceOwnerReleased = 12
    }

    public enum ESVfxFailureCode : byte
    {
        None = 0,
        InvalidKey = 1,
        VfxNotRegistered = 2,
        RuntimeAssetsNotReady = 3,
        NoUsableVariant = 4,
        PrefabNotPrewarmed = 5,
        PoolUnavailable = 6,
        BudgetRejected = 7,
        BackendFailure = 8,
        PoolReturnRejected = 9,
        MissingOwner = 10,
        DirectPrefabUnavailable = 11,
        InvalidDirectPrefabConfig = 12,
        LoopWithoutLifetime = 13
    }

    public static class ESVfxDiagnostics
    {
        public static string DescribeFailure(ESVfxFailureCode code)
        {
            switch (code)
            {
                case ESVfxFailureCode.None: return "无失败";
                case ESVfxFailureCode.InvalidKey: return "VFX Key 未配置";
                case ESVfxFailureCode.VfxNotRegistered: return "VFX 未注册到当前 GameCore";
                case ESVfxFailureCode.RuntimeAssetsNotReady: return "运行时特效资源尚未就绪";
                case ESVfxFailureCode.NoUsableVariant: return "VFX 没有可用变体";
                case ESVfxFailureCode.PrefabNotPrewarmed: return "VFX Prefab 未由当前 ResourcePlan 预热";
                case ESVfxFailureCode.PoolUnavailable: return "VFX 对象池不可用";
                case ESVfxFailureCode.BudgetRejected: return "VFX 预算或并发策略拒绝请求";
                case ESVfxFailureCode.BackendFailure: return "VFX 播放后端失败";
                case ESVfxFailureCode.PoolReturnRejected: return "VFX 回池代际校验失败";
                case ESVfxFailureCode.MissingOwner: return "附着播放缺少有效 Transform";
                case ESVfxFailureCode.DirectPrefabUnavailable: return "直接 Prefab Lease 不可用";
                case ESVfxFailureCode.InvalidDirectPrefabConfig: return "直接 Prefab 播放配置无效";
                case ESVfxFailureCode.LoopWithoutLifetime: return "循环 VFX 缺少最大生命周期";
                default: return "未知 VFX 失败";
            }
        }
    }

    /// <summary>Presentation-only labels for the same observable states used by Audio.</summary>
    public static class ESVfxDiagnosticText
    {
        public static string GetChineseState(ESVfxState state)
        {
            switch (state)
            {
                case ESVfxState.PendingLoad: return "等待资源加载";
                case ESVfxState.Playing: return "正在播放";
                case ESVfxState.Stopping: return "正在停止";
                case ESVfxState.Ended: return "已结束";
                default: return "未知状态";
            }
        }

        public static string GetChineseEndReason(ESVfxEndReason reason)
        {
            switch (reason)
            {
                case ESVfxEndReason.None: return "未结束";
                case ESVfxEndReason.NaturalEnd: return "自然结束";
                case ESVfxEndReason.ExplicitStop: return "显式停止";
                case ESVfxEndReason.OwnerDestroyed: return "Owner 已销毁";
                case ESVfxEndReason.OwnerDisabled: return "Owner 已禁用";
                case ESVfxEndReason.OwnerDespawned: return "Owner 已回收到对象池";
                case ESVfxEndReason.Preempted: return "被预算策略抢占";
                case ESVfxEndReason.BudgetRejected: return "被预算拒绝";
                case ESVfxEndReason.BackendFailure: return "VFX 后端失败";
                case ESVfxEndReason.ModuleDisabled: return "VFX 模块已停用";
                case ESVfxEndReason.SceneChanged: return "场景已切换";
                case ESVfxEndReason.ProviderTransition: return "资源后端切换";
                case ESVfxEndReason.ResourceOwnerReleased: return "资源 Owner 已释放所借用的 VFX 资源";
                default: return "未知结束原因";
            }
        }
    }

    public readonly struct ESVfxHandle : IEquatable<ESVfxHandle>
    {
        internal readonly int id;
        internal readonly int generation;

        internal ESVfxHandle(int id, int generation)
        {
            this.id = id;
            this.generation = generation;
        }

        public bool IsValid => id != 0 && generation != 0;
        public bool Equals(ESVfxHandle other) => id == other.id && generation == other.generation;
        public override bool Equals(object obj) => obj is ESVfxHandle other && Equals(other);
        public override int GetHashCode() => (id * 397) ^ generation;
    }

    public readonly struct ESVfxStatus
    {
        public readonly ESVfxHandle Handle;
        public readonly ESVfxState State;
        public readonly ESVfxEndReason EndReason;
        public readonly ESVfxFailureCode FailureCode;
        public readonly string Key;

        internal ESVfxStatus(ESVfxHandle handle, ESVfxState state, ESVfxEndReason endReason, ESVfxFailureCode failureCode, string key)
        {
            Handle = handle;
            State = state;
            EndReason = endReason;
            FailureCode = failureCode;
            Key = key;
        }
    }

    /// <summary>Bounded machine-readable active/terminal projection, symmetric with Audio diagnostics.</summary>
    public readonly struct ESVfxDiagnostic
    {
        public readonly ESVfxHandle Handle;
        public readonly string Key;
        public readonly ESVfxState State;
        public readonly ESVfxEndReason EndReason;
        public readonly ESVfxFailureCode FailureCode;
        public readonly ESVfxCategory Category;
        public readonly bool Loading;
        public readonly bool Loop;
        public readonly int Priority;
        public readonly int PoolVersion;

        internal ESVfxDiagnostic(ESVfxHandle handle, string key, ESVfxState state,
            ESVfxEndReason endReason, ESVfxFailureCode failureCode, ESVfxCategory category,
            bool loading, bool loop, int priority, int poolVersion)
        {
            Handle = handle;
            Key = key;
            State = state;
            EndReason = endReason;
            FailureCode = failureCode;
            Category = category;
            Loading = loading;
            Loop = loop;
            Priority = priority;
            PoolVersion = poolVersion;
        }
    }

    [Serializable]
    public struct ESVfxPlayRequest
    {
        [LabelText("Owner")]
        public Transform owner;

        [LabelText("位置")]
        public Vector3 position;

        [LabelText("旋转")]
        public Quaternion rotation;

        [LabelText("父节点")]
        public Transform parent;

        [LabelText("跟随 Owner")]
        public bool followOwner;

        [System.NonSerialized]
        internal bool forceOneShot;

        [System.NonSerialized]
        internal bool forceLoop;

        [LabelText("优先级修正")]
        public int priorityOffset;
    }

    /// <summary>
    /// Playback-only policy for an already acquired prefab lease. The lease is transferred to
    /// ESVfxModule on call; rejected requests release it immediately, accepted requests release it
    /// with the terminal Handle.
    /// </summary>
    [Serializable]
    public sealed class ESVfxPrefabPlayConfig
    {
        [LabelText("预算 Key")]
        public string budgetKey = "vfx:direct";
        public ESVfxCategory category = ESVfxCategory.Combat;
        public bool loop;
        [Range(0, 256)] public int priority = 128;
        [MinValue(0)] public int maxConcurrent;
        public ESVfxPreemptionPolicy preemptionPolicy = ESVfxPreemptionPolicy.RejectNew;
        [MinValue(0f)] public float maxLifetime;
        public ESVfxTimeMode timeMode = ESVfxTimeMode.ScaledGameTime;

        public bool TryValidate(out string error)
        {
            if (string.IsNullOrWhiteSpace(budgetKey))
            {
                error = "直接 Prefab 播放必须提供预算 Key。";
                return false;
            }
            if (priority < 0 || priority > 256 || maxConcurrent < 0 || maxLifetime < 0f)
            {
                error = "直接 Prefab 播放配置包含无效预算或生命周期数值。";
                return false;
            }
            if (loop && maxLifetime <= 0f)
            {
                error = "循环 VFX 必须提供大于 0 的最大生命周期。";
                return false;
            }
            error = null;
            return true;
        }
    }

    /// <summary>Cached runtime bridge for a pooled VFX root. It scans children only once.</summary>
    [DisallowMultipleComponent]
    public sealed class ESVfxInstanceRoot : MonoBehaviour, IESGameObjectPoolLifecycle
    {
        private ParticleSystem[] particles = Array.Empty<ParticleSystem>();
        private bool cached;

        public void CacheReceivers()
        {
            if (cached) return;
            particles = GetComponentsInChildren<ParticleSystem>(true);
            cached = true;
        }

        public void Play()
        {
            CacheReceivers();
            for (int i = 0; i < particles.Length; i++)
                if (particles[i] != null) particles[i].Play(true);
        }

        public void Stop()
        {
            CacheReceivers();
            for (int i = 0; i < particles.Length; i++)
                if (particles[i] != null) particles[i].Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        public bool IsFinished()
        {
            CacheReceivers();
            if (particles.Length == 0) return true;
            for (int i = 0; i < particles.Length; i++)
                if (particles[i] != null && (particles[i].isPlaying || particles[i].IsAlive(true))) return false;
            return true;
        }

        void IESGameObjectPoolLifecycle.OnPoolSpawned() { CacheReceivers(); }
        void IESGameObjectPoolLifecycle.OnPoolDespawned() { Stop(); }
    }

    [Serializable, TypeRegistryItem("系统模块/VFX")]
    public sealed class ESVfxModule : ESSystemModule
    {
        [TitleGroup("预算"), LabelText("最大实例数"), MinValue(1)]
        public int maxInstances = 128;

        private sealed class ActiveVfx
        {
            public ESVfxHandle handle;
            public string key;
            public ESVfxInfo info;
            public ESVfxCategory category;
            public int priority;
            public int maxConcurrent;
            public ESVfxPreemptionPolicy preemptionPolicy;
            public float maxLifetime;
            public ESVfxTimeMode timeMode;
            public ESVfxInstanceRoot root;
            public Transform owner;
            public ESPooledGameObject ownerPool;
            public int ownerPoolVersion;
            public int poolVersion;
            public bool followOwner;
            public bool loop;
            public float startedAt;
            public ESAssetConfigPayloadLease<GameObject> prefabLease;
            public ESAssetIdentity prefabIdentity;
            public bool hasPrefabIdentity;
            public ESVfxStatus terminal;
        }

        private readonly List<ActiveVfx> active = new List<ActiveVfx>(128);
        private readonly Dictionary<ESVfxHandle, ESVfxStatus> terminal = new Dictionary<ESVfxHandle, ESVfxStatus>(128);
        private readonly Queue<ESVfxHandle> terminalOrder = new Queue<ESVfxHandle>(128);
        private readonly List<ESVfxDiagnostic> recentFailures = new List<ESVfxDiagnostic>(64);
        private bool subscribedToResourceTransitions;
        private int nextId = 1;
        private int nextGeneration = 1;

        public int ActiveInstanceCount => active.Count;

        protected override void OnEnable()
        {
            base.OnEnable();
            if (!subscribedToResourceTransitions)
            {
                ESAssets.RuntimeBackendTransitionStarting += OnRuntimeBackendTransitionStarting;
                ESAssets.ActivePlanAssetOwnershipEnding += OnActivePlanAssetOwnershipEnding;
                subscribedToResourceTransitions = true;
            }
        }

        public void CopyDiagnostics(List<ESVfxDiagnostic> destination)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            destination.Clear();
            for (int i = 0; i < active.Count; i++)
            {
                ActiveVfx item = active[i];
                destination.Add(new ESVfxDiagnostic(item.handle, item.key, ESVfxState.Playing,
                    ESVfxEndReason.None, ESVfxFailureCode.None, item.category, false, item.loop,
                    item.priority, item.poolVersion));
            }
        }

        /// <summary>Explicitly named alias matching Audio's CopyVoiceDiagnostics surface.</summary>
        public void CopyVfxDiagnostics(List<ESVfxDiagnostic> destination)
            => CopyDiagnostics(destination);

        public void CopyRecentFailures(List<ESVfxDiagnostic> destination)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            destination.Clear();
            destination.AddRange(recentFailures);
        }

        /// <summary>Concise one-shot alias matching the Audio module's public vocabulary.</summary>
        public ESVfxHandle PlayOneShot(ESVfxKey key, ESVfxPlayRequest request = default)
        {
            request.forceOneShot = true;
            return Play(key, request);
        }

        /// <summary>Stable Prefab ConfigKey track; the module acquires and owns the Lease.</summary>
        public ESVfxHandle PlayOneShot(ESAssetReferPrefabConfigKey prefabKey, ESVfxPrefabPlayConfig config = null,
            ESVfxPlayRequest request = default)
            => PlayPrefabKey(prefabKey, config, request, forceLoop: false);

        /// <summary>Plays a VFX definition attached to and following an Owner.</summary>
        public ESVfxHandle PlayAttached(ESVfxKey key, Transform owner, ESVfxPlayRequest request = default)
        {
            if (owner == null)
                return Reject(key, ESVfxFailureCode.MissingOwner);

            request.owner = owner;
            request.parent = owner;
            request.followOwner = true;
            request.position = owner.position;
            return Play(key, request);
        }

        public ESVfxHandle PlayAttached(ESAssetReferPrefabConfigKey prefabKey, Transform owner,
            ESVfxPrefabPlayConfig config = null, ESVfxPlayRequest request = default)
        {
            if (owner == null)
                return RejectDirect(string.Empty, ESVfxFailureCode.MissingOwner);
            request.owner = owner;
            request.parent = owner;
            request.followOwner = true;
            request.position = owner.position;
            return PlayPrefabKey(prefabKey, config, request, forceLoop: false);
        }

        /// <summary>Plays a VFX definition at a world position under the normal VFX budgets.</summary>
        public ESVfxHandle PlayAtPosition(ESVfxKey key, Vector3 position, ESVfxPlayRequest request = default)
        {
            request.position = position;
            return Play(key, request);
        }

        public ESVfxHandle PlayAtPosition(ESAssetReferPrefabConfigKey prefabKey, Vector3 position,
            ESVfxPrefabPlayConfig config = null, ESVfxPlayRequest request = default)
        {
            request.position = position;
            return PlayPrefabKey(prefabKey, config, request, forceLoop: false);
        }

        /// <summary>Forces a definition to loop for this instance; maxLifetime remains mandatory.</summary>
        public ESVfxHandle PlayLoop(ESVfxKey key, ESVfxPlayRequest request = default)
        {
            request.forceLoop = true;
            return Play(key, request);
        }

        public ESVfxHandle PlayLoop(ESVfxKey key, Transform owner, ESVfxPlayRequest request = default)
        {
            request.forceLoop = true;
            return PlayAttached(key, owner, request);
        }

        public ESVfxHandle PlayLoop(ESAssetReferPrefabConfigKey prefabKey, Transform owner,
            ESVfxPrefabPlayConfig config, ESVfxPlayRequest request = default)
        {
            if (owner == null)
                return RejectDirect(string.Empty, ESVfxFailureCode.MissingOwner);
            request.owner = owner;
            request.parent = owner;
            request.followOwner = true;
            request.position = owner.position;
            return PlayPrefabKey(prefabKey, config, request, forceLoop: true);
        }

        private ESVfxHandle PlayPrefabKey(ESAssetReferPrefabConfigKey prefabKey,
            ESVfxPrefabPlayConfig config, ESVfxPlayRequest request, bool forceLoop)
        {
            if (prefabKey == null || !prefabKey.IsConfigured)
                return RejectDirect(string.Empty, ESVfxFailureCode.InvalidKey);
            if (!ESGameManager.RuntimePrefabAssets.TryAcquireReady(prefabKey,
                out ESAssetConfigPayloadLease<GameObject> prefabLease))
                return RejectDirect(ESConfigKeyMatch.Describe(prefabKey.EnumKeyInt, prefabKey.StringKey),
                    ESVfxFailureCode.PrefabNotPrewarmed);

            if (config == null)
                config = new ESVfxPrefabPlayConfig();
            request.forceLoop = forceLoop;
            return Play(prefabLease, config, request);
        }

        public ESVfxHandle Play(ESVfxKey key, ESVfxPlayRequest request = default)
        {
            if (key == null || !key.IsConfigured)
                return Reject(key, ESVfxFailureCode.InvalidKey);
            if (!ESVfxGameCoreTable.Table.TryGet(key, out ESVfxRuntimeData data) || data == null || !data.Ready || data.source == null)
                return Reject(key, ESVfxFailureCode.VfxNotRegistered);
            if (!data.source.TrySelectVariant(out ESVfxVariant variant))
                return Reject(key, ESVfxFailureCode.NoUsableVariant);
            if (!ESGameManager.RuntimePrefabAssets.TryAcquireReady(variant.prefabKey, out ESAssetConfigPayloadLease<GameObject> prefabLease))
                return Reject(key, ESVfxFailureCode.PrefabNotPrewarmed);

            bool hasPrefabIdentity = ESGameManager.RuntimePrefabAssets.TryResolveAssetIdentity(
                variant.prefabKey.EnumKeyInt, variant.prefabKey.StringKey, out ESAssetIdentity prefabIdentity);

            return PlayLeasedPrefab(
                ESConfigKeyMatch.Describe(key.EnumKeyInt, key.StringKey),
                data.source,
                prefabLease,
                request,
                key,
                null,
                prefabIdentity,
                hasPrefabIdentity);
        }

        /// <summary>
        /// Plays an already acquired prefab lease under the same pool, budget and Handle rules as
        /// a VFX definition. Ownership transfers only when a Handle is accepted.
        /// </summary>
        public ESVfxHandle Play(
            ESAssetConfigPayloadLease<GameObject> prefabLease,
            ESVfxPrefabPlayConfig config,
            ESVfxPlayRequest request = default)
        {
            if (prefabLease == null || prefabLease.IsDisposed || prefabLease.Asset == null)
                return RejectDirect(string.Empty, ESVfxFailureCode.DirectPrefabUnavailable);
            if (config == null || !config.TryValidate(out _))
            {
                prefabLease.Dispose();
                return RejectDirect(config != null ? config.budgetKey : string.Empty, ESVfxFailureCode.InvalidDirectPrefabConfig);
            }

            return PlayLeasedPrefab(config.budgetKey, null, prefabLease, request, null, config, default, false);
        }

        public ESVfxHandle PlayOneShot(
            ESAssetConfigPayloadLease<GameObject> prefabLease,
            ESVfxPrefabPlayConfig config,
            ESVfxPlayRequest request = default)
        {
            request.forceOneShot = true;
            return Play(prefabLease, config, request);
        }

        public ESVfxHandle PlayLoop(
            ESAssetConfigPayloadLease<GameObject> prefabLease,
            ESVfxPrefabPlayConfig config,
            ESVfxPlayRequest request = default)
        {
            request.forceLoop = true;
            return Play(prefabLease, config, request);
        }

        public ESVfxHandle PlayLoop(
            ESAssetConfigPayloadLease<GameObject> prefabLease,
            ESVfxPrefabPlayConfig config,
            Transform owner,
            ESVfxPlayRequest request = default)
        {
            request.forceLoop = true;
            return PlayAttached(prefabLease, config, owner, request);
        }

        public ESVfxHandle PlayAttached(
            ESAssetConfigPayloadLease<GameObject> prefabLease,
            ESVfxPrefabPlayConfig config,
            Transform owner,
            ESVfxPlayRequest request = default)
        {
            if (owner == null)
            {
                prefabLease?.Dispose();
                return RejectDirect(config != null ? config.budgetKey : string.Empty, ESVfxFailureCode.MissingOwner);
            }
            request.owner = owner;
            request.parent = owner;
            request.followOwner = true;
            request.position = owner.position;
            return Play(prefabLease, config, request);
        }

        public ESVfxHandle PlayAtPosition(
            ESAssetConfigPayloadLease<GameObject> prefabLease,
            ESVfxPrefabPlayConfig config,
            Vector3 position,
            ESVfxPlayRequest request = default)
        {
            request.position = position;
            return Play(prefabLease, config, request);
        }

        private ESVfxHandle PlayLeasedPrefab(
            string budgetKey,
            ESVfxInfo info,
            ESAssetConfigPayloadLease<GameObject> prefabLease,
            ESVfxPlayRequest request,
            ESVfxKey key,
            ESVfxPrefabPlayConfig directConfig,
            ESAssetIdentity prefabIdentity,
            bool hasPrefabIdentity)
        {
            int configuredMaxConcurrent = info != null ? info.maxConcurrent : directConfig.maxConcurrent;
            ESVfxPreemptionPolicy configuredPreemption = info != null ? info.preemptionPolicy : directConfig.preemptionPolicy;
            int configuredPriority = info != null ? info.priority : directConfig.priority;
            ESVfxCategory configuredCategory = info != null ? info.category : directConfig.category;
            float configuredLifetime = info != null ? info.maxLifetime : directConfig.maxLifetime;
            ESVfxTimeMode configuredTimeMode = info != null ? info.timeMode : directConfig.timeMode;
            bool configuredLoop = request.forceLoop || (info != null ? info.loop : directConfig.loop);

            if (configuredLoop && configuredLifetime <= 0f)
                return RejectAndRelease(ESVfxFailureCode.LoopWithoutLifetime);

            ESVfxHandle RejectAndRelease(ESVfxFailureCode code)
            {
                prefabLease?.Dispose();
                return RejectOrDirect(key, budgetKey, code);
            }

            if (active.Count >= Mathf.Max(1, maxInstances))
                return RejectAndRelease(ESVfxFailureCode.BudgetRejected);

            int concurrent = 0;
            for (int i = 0; i < active.Count; i++)
                if (string.Equals(active[i].key, budgetKey, StringComparison.Ordinal))
                    concurrent++;
            if (configuredMaxConcurrent > 0 && concurrent >= configuredMaxConcurrent)
            {
                if (configuredPreemption == ESVfxPreemptionPolicy.RejectNew)
                    return RejectAndRelease(ESVfxFailureCode.BudgetRejected);
                if (configuredPreemption == ESVfxPreemptionPolicy.StopOldest)
                    EndActive(FindOldest(budgetKey), ESVfxEndReason.Preempted, ESVfxFailureCode.BudgetRejected);
                else
                    EndActive(FindLowestPriority(budgetKey), ESVfxEndReason.Preempted, ESVfxFailureCode.BudgetRejected);
            }
            if (!ESGameManager.TryGetModule(out ESGameObjectPoolModule pool) || pool == null)
                return RejectAndRelease(ESVfxFailureCode.PoolUnavailable);

            GameObject instance = null;

            try
            {
                if (prefabLease == null || prefabLease.IsDisposed || prefabLease.Asset == null)
                    return RejectAndRelease(ESVfxFailureCode.DirectPrefabUnavailable);
                Quaternion rotation = request.rotation == default ? Quaternion.identity : request.rotation;
                instance = pool.GetInPool(prefabLease.Asset, request.position, rotation, request.parent, false, 0f);
                if (instance == null)
                {
                    prefabLease.Dispose();
                    return RejectOrDirect(key, budgetKey, ESVfxFailureCode.PoolUnavailable);
                }

                ESPooledGameObject pooled = instance.GetComponent<ESPooledGameObject>();
                int acquiredPoolVersion = pooled != null ? pooled.Version : 0;

                ESVfxInstanceRoot root = instance.GetComponent<ESVfxInstanceRoot>();
                if (root == null)
                {
                    if (pooled != null)
                        pool.PushToPool(instance, acquiredPoolVersion);
                    else
                        UnityEngine.Object.Destroy(instance);
                    prefabLease.Dispose();
                    return RejectOrDirect(key, budgetKey, ESVfxFailureCode.BackendFailure);
                }
                root.CacheReceivers();
                root.Play();

                if (pooled == null)
                {
                    UnityEngine.Object.Destroy(instance);
                    prefabLease.Dispose();
                    return RejectOrDirect(key, budgetKey, ESVfxFailureCode.BackendFailure);
                }

                ESPooledGameObject ownerPool = request.owner != null
                    ? request.owner.GetComponentInParent<ESPooledGameObject>()
                    : null;

                ESVfxHandle handle = new ESVfxHandle(NextVfxId(), NextVfxGeneration());
                active.Add(new ActiveVfx
                {
                    handle = handle,
                    key = budgetKey,
                    info = info,
                    category = configuredCategory,
                    priority = configuredPriority + request.priorityOffset,
                    maxConcurrent = configuredMaxConcurrent,
                    preemptionPolicy = configuredPreemption,
                    maxLifetime = configuredLifetime,
                    timeMode = configuredTimeMode,
                    root = root,
                    owner = request.owner,
                    ownerPool = ownerPool,
                    ownerPoolVersion = ownerPool != null ? ownerPool.Version : 0,
                    poolVersion = pooled.Version,
                    followOwner = request.followOwner,
                    loop = request.forceOneShot ? false : configuredLoop,
                    startedAt = Clock(configuredTimeMode),
                    prefabLease = prefabLease,
                    prefabIdentity = prefabIdentity,
                    hasPrefabIdentity = hasPrefabIdentity
                });
                return handle;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, instance);
                if (instance != null)
                {
                    ESPooledGameObject pooled = instance.GetComponent<ESPooledGameObject>();
                    if (pooled != null)
                        pool.PushToPool(instance, pooled.Version);
                }
                prefabLease.Dispose();
                return RejectOrDirect(key, budgetKey, ESVfxFailureCode.BackendFailure);
            }
        }

        public bool Stop(ESVfxHandle handle, ESVfxEndReason reason = ESVfxEndReason.ExplicitStop)
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                if (!active[i].handle.Equals(handle)) continue;
                EndActive(i, reason, ESVfxFailureCode.None);
                return true;
            }
            return false;
        }

        public bool TryGetStatus(ESVfxHandle handle, out ESVfxStatus status)
        {
            for (int i = 0; i < active.Count; i++)
                if (active[i].handle.Equals(handle))
                {
                    status = new ESVfxStatus(handle, ESVfxState.Playing, ESVfxEndReason.None, ESVfxFailureCode.None, active[i].key);
                    return true;
                }
            return terminal.TryGetValue(handle, out status);
        }

        /// <summary>Status alias aligned with Audio's TryGetVoiceStatus naming.</summary>
        public bool TryGetVfxStatus(ESVfxHandle handle, out ESVfxStatus status)
            => TryGetStatus(handle, out status);

        public void StopAll(ESVfxEndReason reason = ESVfxEndReason.ExplicitStop)
        {
            for (int i = active.Count - 1; i >= 0; i--)
                EndActive(i, reason, ESVfxFailureCode.None);
        }

        public int StopCategory(ESVfxCategory category, ESVfxEndReason reason = ESVfxEndReason.ExplicitStop)
        {
            int stopped = 0;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                if (active[i].category != category)
                    continue;
                EndActive(i, reason, ESVfxFailureCode.None);
                stopped++;
            }
            return stopped;
        }

        protected override void Update()
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                ActiveVfx item = active[i];
                if (item.owner == null && item.followOwner)
                {
                    EndActive(i, ESVfxEndReason.OwnerDestroyed, ESVfxFailureCode.None);
                    continue;
                }
                // Match Audio's owner-pool guard: a despawn can occur without a version bump
                // on some pool implementations, so IsSpawned is part of the admission identity.
                if (item.ownerPool != null && (!item.ownerPool.IsSpawned || item.ownerPool.Version != item.ownerPoolVersion))
                {
                    EndActive(i, ESVfxEndReason.OwnerDespawned, ESVfxFailureCode.None);
                    continue;
                }
                if (item.followOwner && item.owner != null && !item.owner.gameObject.activeInHierarchy)
                {
                    ESVfxEndReason reason = item.ownerPool != null && !item.ownerPool.IsSpawned
                        ? ESVfxEndReason.OwnerDespawned
                        : ESVfxEndReason.OwnerDisabled;
                    EndActive(i, reason, ESVfxFailureCode.None);
                    continue;
                }
                if (item.followOwner && item.owner != null)
                    item.root.transform.SetPositionAndRotation(item.owner.position, item.owner.rotation);

                float elapsed = Clock(item.timeMode) - item.startedAt;
                if ((item.maxLifetime > 0f && elapsed >= item.maxLifetime)
                    || (!item.loop && item.root.IsFinished()))
                    EndActive(i, ESVfxEndReason.NaturalEnd, ESVfxFailureCode.None);
            }
        }

        protected override void OnDisable()
        {
            for (int i = active.Count - 1; i >= 0; i--)
                EndActive(i, ESVfxEndReason.ModuleDisabled, ESVfxFailureCode.None);
            if (subscribedToResourceTransitions)
            {
                ESAssets.RuntimeBackendTransitionStarting -= OnRuntimeBackendTransitionStarting;
                ESAssets.ActivePlanAssetOwnershipEnding -= OnActivePlanAssetOwnershipEnding;
                subscribedToResourceTransitions = false;
            }
            base.OnDisable();
        }

        public override void OnDestroy()
        {
            // Audio closes the same lifecycle boundary explicitly; VFX must release every
            // accepted Prefab Lease and return pooled instances even when disabled callbacks
            // are skipped by a host teardown path.
            StopAll(ESVfxEndReason.ModuleDisabled);
            if (subscribedToResourceTransitions)
            {
                ESAssets.RuntimeBackendTransitionStarting -= OnRuntimeBackendTransitionStarting;
                ESAssets.ActivePlanAssetOwnershipEnding -= OnActivePlanAssetOwnershipEnding;
                subscribedToResourceTransitions = false;
            }
            base.OnDestroy();
        }

        private void OnRuntimeBackendTransitionStarting()
        {
            // A Lease pins its generation, but active pooled instances still reference the old
            // provider/backend. End them synchronously, matching Audio's transition boundary.
            StopAll(ESVfxEndReason.ProviderTransition);
        }

        private void OnActivePlanAssetOwnershipEnding(ESAssetIdentity identity)
        {
            if (!identity.IsValid)
                return;

            for (int i = active.Count - 1; i >= 0; i--)
            {
                ActiveVfx item = active[i];
                if (item.hasPrefabIdentity && item.prefabIdentity.Equals(identity))
                    EndActive(i, ESVfxEndReason.ResourceOwnerReleased, ESVfxFailureCode.None);
            }
        }

        private void EndActive(int index, ESVfxEndReason reason, ESVfxFailureCode failure)
        {
            if (index < 0 || index >= active.Count)
                return;
            ActiveVfx item = active[index];
            active.RemoveAt(index);
            try
            {
                ESGameObjectPoolModule pool = ESGameManager.PoolModule;
                if (item.root != null && pool != null)
                {
                    ESPooledGameObject pooled = item.root.GetComponent<ESPooledGameObject>();
                    if (pooled == null || !pooled.IsSpawned || pooled.Version != item.poolVersion)
                    {
                        failure = ESVfxFailureCode.PoolReturnRejected;
                    }
                    else
                    {
                        item.root.Stop();
                        if (!pool.PushToPool(item.root.gameObject, item.poolVersion))
                            failure = ESVfxFailureCode.PoolReturnRejected;
                    }
                }
                else
                {
                    item.root?.Stop();
                    if (item.root != null && pool == null)
                        UnityEngine.Object.Destroy(item.root.gameObject);
                }

                if (failure == ESVfxFailureCode.PoolReturnRejected)
                {
                    int observedVersion = item.root != null
                        ? item.root.GetComponent<ESPooledGameObject>()?.Version ?? 0
                        : 0;
                    Debug.LogWarning($"[ESVfx] 回池被拒绝：Key={item.key}，预期Version={item.poolVersion}，当前Version={observedVersion}");
                }
            }
            finally
            {
                item.prefabLease?.Dispose();
                ESVfxStatus status = new ESVfxStatus(item.handle, ESVfxState.Ended, reason, failure, item.key);
                terminal[item.handle] = status;
                if (failure != ESVfxFailureCode.None)
                    recentFailures.Add(new ESVfxDiagnostic(item.handle, item.key, ESVfxState.Ended, reason, failure,
                        item.category, false, item.loop, item.priority, item.poolVersion));
                while (recentFailures.Count > 64)
                    recentFailures.RemoveAt(0);
                terminalOrder.Enqueue(item.handle);
                while (terminal.Count > 256 && terminalOrder.Count > 0)
                    terminal.Remove(terminalOrder.Dequeue());
            }
        }

        private ESVfxHandle Reject(ESVfxKey key, ESVfxFailureCode code)
        {
            string keyName = key == null ? string.Empty : ESConfigKeyMatch.Describe(key.EnumKeyInt, key.StringKey);
            RecordFailure(keyName, code);
            Debug.LogWarning("[ESVfx] 请求被拒绝：" + code + "（" + ESVfxDiagnostics.DescribeFailure(code) + ")"
                + (string.IsNullOrEmpty(keyName) ? string.Empty : "，Key=" + keyName));
            return default;
        }

        private ESVfxHandle RejectDirect(string budgetKey, ESVfxFailureCode code)
        {
            RecordFailure(budgetKey, code);
            Debug.LogWarning("[ESVfx] 直接 Prefab 请求被拒绝：" + code + "（"
                + ESVfxDiagnostics.DescribeFailure(code) + ")"
                + (string.IsNullOrEmpty(budgetKey) ? string.Empty : "，预算Key=" + budgetKey));
            return default;
        }

        private ESVfxHandle RejectOrDirect(ESVfxKey key, string budgetKey, ESVfxFailureCode code)
            => key != null ? Reject(key, code) : RejectDirect(budgetKey, code);

        private void RecordFailure(string key, ESVfxFailureCode code)
        {
            recentFailures.Add(new ESVfxDiagnostic(default, key ?? string.Empty, ESVfxState.Ended,
                ESVfxEndReason.None, code, default, false, false, 0, 0));
            if (recentFailures.Count > 64)
                recentFailures.RemoveAt(0);
        }

        private int FindOldest(string name)
        {
            int result = -1;
            float oldest = float.MaxValue;
            for (int i = 0; i < active.Count; i++)
                if (active[i].key == name && active[i].startedAt < oldest)
                {
                    oldest = active[i].startedAt;
                    result = i;
                }
            return result;
        }

        private int FindLowestPriority(string name)
        {
            int result = -1;
            int priority = int.MaxValue;
            for (int i = 0; i < active.Count; i++)
                if (active[i].key == name && active[i].priority < priority)
                {
                    priority = active[i].priority;
                    result = i;
                }
            return result;
        }

        private int NextVfxId()
        {
            int id = nextId;
            nextId = nextId == int.MaxValue ? 1 : nextId + 1;
            return id;
        }

        private int NextVfxGeneration()
        {
            int generation = nextGeneration;
            nextGeneration = nextGeneration == int.MaxValue ? 1 : nextGeneration + 1;
            return generation;
        }

        private static float Clock(ESVfxTimeMode mode) => mode == ESVfxTimeMode.UnscaledTime ? Time.unscaledTime : Time.time;
    }
}
