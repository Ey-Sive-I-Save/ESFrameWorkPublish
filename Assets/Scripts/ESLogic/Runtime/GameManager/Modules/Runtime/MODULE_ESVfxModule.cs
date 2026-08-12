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
        SceneChanged = 10
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
        BackendFailure = 8
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

        [LabelText("优先级修正")]
        public int priorityOffset;
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
            public ESVfxInstanceRoot root;
            public Transform owner;
            public bool followOwner;
            public float startedAt;
            public ESAssetConfigPayloadLease<GameObject> prefabLease;
            public ESVfxStatus terminal;
        }

        private readonly List<ActiveVfx> active = new List<ActiveVfx>(128);
        private readonly Dictionary<int, ESVfxStatus> terminal = new Dictionary<int, ESVfxStatus>(128);
        private int nextId = 1;
        private int nextGeneration = 1;

        public int ActiveInstanceCount => active.Count;

        public ESVfxHandle Play(ESVfxKey key, ESVfxPlayRequest request = default)
        {
            if (key == null || !key.IsConfigured)
                return Reject(key, ESVfxFailureCode.InvalidKey);
            if (active.Count >= Mathf.Max(1, maxInstances))
                return Reject(key, ESVfxFailureCode.BudgetRejected);
            if (!ESVfxGameCoreTable.Table.TryGet(key, out ESVfxRuntimeData data) || data == null || !data.Ready || data.source == null)
                return Reject(key, ESVfxFailureCode.VfxNotRegistered);
            if (!data.source.TrySelectVariant(out ESVfxVariant variant))
                return Reject(key, ESVfxFailureCode.NoUsableVariant);
            int concurrent = 0;
            for (int i = 0; i < active.Count; i++)
                if (string.Equals(active[i].key, ESConfigKeyMatch.Describe(key.EnumKeyInt, key.StringKey), StringComparison.Ordinal))
                    concurrent++;
            if (data.source.maxConcurrent > 0 && concurrent >= data.source.maxConcurrent)
            {
                if (data.source.preemptionPolicy == ESVfxPreemptionPolicy.RejectNew)
                    return Reject(key, ESVfxFailureCode.BudgetRejected);
                if (data.source.preemptionPolicy == ESVfxPreemptionPolicy.StopOldest)
                    EndActive(FindOldest(key), ESVfxEndReason.Preempted, ESVfxFailureCode.BudgetRejected);
                else
                    EndActive(FindLowestPriority(key), ESVfxEndReason.Preempted, ESVfxFailureCode.BudgetRejected);
            }
            if (!ESGameManager.TryGetModule(out ESGameObjectPoolModule pool) || pool == null)
                return Reject(key, ESVfxFailureCode.PoolUnavailable);
            if (!ESGameManager.RuntimePrefabAssets.TryAcquireReady(variant.prefabKey, out ESAssetConfigPayloadLease<GameObject> prefabLease))
                return Reject(key, ESVfxFailureCode.PrefabNotPrewarmed);

            GameObject instance = null;
            try
            {
                Quaternion rotation = request.rotation == default ? Quaternion.identity : request.rotation;
                instance = pool.GetInPool(prefabLease.Asset, request.position, rotation, request.parent, false, 0f);
                if (instance == null)
                {
                    prefabLease.Dispose();
                    return Reject(key, ESVfxFailureCode.PoolUnavailable);
                }

                ESVfxInstanceRoot root = instance.GetComponent<ESVfxInstanceRoot>();
                if (root == null)
                {
                    pool.PushToPool(instance);
                    prefabLease.Dispose();
                    return Reject(key, ESVfxFailureCode.BackendFailure);
                }
                root.CacheReceivers();
                root.Play();

                ESVfxHandle handle = new ESVfxHandle(nextId++, nextGeneration++);
                active.Add(new ActiveVfx
                {
                    handle = handle,
                    key = ESConfigKeyMatch.Describe(key.EnumKeyInt, key.StringKey),
                    info = data.source,
                    root = root,
                    owner = request.owner,
                    followOwner = request.followOwner,
                    startedAt = Clock(data.source.timeMode),
                    prefabLease = prefabLease
                });
                return handle;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, instance);
                if (instance != null) pool.PushToPool(instance);
                prefabLease.Dispose();
                return Reject(key, ESVfxFailureCode.BackendFailure);
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
            return terminal.TryGetValue(handle.id, out status);
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
                if (item.followOwner && item.owner != null)
                    item.root.transform.SetPositionAndRotation(item.owner.position, item.owner.rotation);

                float elapsed = Clock(item.info.timeMode) - item.startedAt;
                if ((item.info.maxLifetime > 0f && elapsed >= item.info.maxLifetime)
                    || (!item.info.loop && item.root.IsFinished()))
                    EndActive(i, ESVfxEndReason.NaturalEnd, ESVfxFailureCode.None);
            }
        }

        protected override void OnDisable()
        {
            for (int i = active.Count - 1; i >= 0; i--)
                EndActive(i, ESVfxEndReason.ModuleDisabled, ESVfxFailureCode.None);
            base.OnDisable();
        }

        private void EndActive(int index, ESVfxEndReason reason, ESVfxFailureCode failure)
        {
            if (index < 0 || index >= active.Count)
                return;
            ActiveVfx item = active[index];
            active.RemoveAt(index);
            try
            {
                item.root?.Stop();
                ESGameObjectPoolModule pool = ESGameManager.PoolModule;
                if (item.root != null && pool != null)
                    pool.PushToPool(item.root.gameObject);
            }
            finally
            {
                item.prefabLease?.Dispose();
                ESVfxStatus status = new ESVfxStatus(item.handle, ESVfxState.Ended, reason, failure, item.key);
                terminal[item.handle.id] = status;
                if (terminal.Count > 256)
                    terminal.Remove(item.handle.id);
            }
        }

        private ESVfxHandle Reject(ESVfxKey key, ESVfxFailureCode code)
        {
            string keyName = key == null ? string.Empty : ESConfigKeyMatch.Describe(key.EnumKeyInt, key.StringKey);
            Debug.LogWarning("[ESVfx] 请求被拒绝：" + code + (string.IsNullOrEmpty(keyName) ? string.Empty : "，Key=" + keyName));
            return default;
        }

        private int FindOldest(ESVfxKey key)
        {
            string name = ESConfigKeyMatch.Describe(key.EnumKeyInt, key.StringKey);
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

        private int FindLowestPriority(ESVfxKey key)
        {
            string name = ESConfigKeyMatch.Describe(key.EnumKeyInt, key.StringKey);
            int result = -1;
            int priority = int.MaxValue;
            for (int i = 0; i < active.Count; i++)
                if (active[i].key == name && active[i].info.priority < priority)
                {
                    priority = active[i].info.priority;
                    result = i;
                }
            return result;
        }

        private static float Clock(ESVfxTimeMode mode) => mode == ESVfxTimeMode.UnscaledTime ? Time.unscaledTime : Time.time;
    }
}
