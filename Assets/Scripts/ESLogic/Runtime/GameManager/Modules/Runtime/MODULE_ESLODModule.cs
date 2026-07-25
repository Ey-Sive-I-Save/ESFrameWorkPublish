using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    public enum ESGlobalLODLevel : byte
    {
        [InspectorName("高质量")]
        HighQuality = 0,
        [InspectorName("均衡")]
        Balanced = 1,
        [InspectorName("性能优先")]
        Performance = 2,
        [InspectorName("压测")]
        Stress = 3
    }

    public enum ESLODLevel : byte
    {
        [InspectorName("完整更新")]
        Full = 0,
        [InspectorName("降频更新")]
        Reduced = 1,
        [InspectorName("仅表现")]
        VisualOnly = 2,
        [InspectorName("休眠")]
        Sleep = 3
    }

    [Flags]
    public enum ESLODGate : ushort
    {
        [InspectorName("无")]
        None = 0,
        [InspectorName("暂停")]
        Pause = 1 << 0,
        [InspectorName("停用")]
        Stop = 1 << 1,
        [InspectorName("死亡")]
        Death = 1 << 2,
        [InspectorName("始终完整")]
        AlwaysFull = 1 << 3,
        [InspectorName("始终休眠")]
        AlwaysSleep = 1 << 4
    }

    public struct ESLODCacheEntry
    {
        public int key;
        public ESGlobalLODLevel globalLevel;
        public ESLODLevel level;
        public ESLODGate gate;
        public ESLODLevel resolvedLevel;
        public float maxCatchupDeltaTime;
        public int version;
        public bool active;
    }

    [Serializable]
    [TypeRegistryItem("LOD模块")]
    public sealed class ESLODModule : ESRuntimeModule
    {
        [Title("全局LOD")]
        [LabelText("全局档位")]
        public ESGlobalLODLevel globalLevel = ESGlobalLODLevel.HighQuality;

        [Title("缓存")]
        [LabelText("预热容量"), MinValue(1)]
        public int warmupCapacity = 256;

        [Title("默认策略")]
        [LabelText("最大补偿时间"), MinValue(0.001f)]
        public float maxCatchupDeltaTime = 0.066f;

        [ShowInInspector, ReadOnly, LabelText("已注册对象")]
        public int RegisteredCount => activeCount;

        private readonly Dictionary<int, int> keyToIndex = new Dictionary<int, int>(256);
        private readonly Stack<int> freeIndices = new Stack<int>(64);
        private ESLODCacheEntry[] cacheEntries;
        private int cacheCount;
        private int activeCount;
        private int globalVersion;

        public override void Start()
        {
            Warmup(warmupCapacity);
        }

        public void Warmup(int capacity)
        {
            int safeCapacity = Mathf.Max(1, capacity);
            if (cacheEntries == null)
            {
                cacheEntries = new ESLODCacheEntry[safeCapacity];
                return;
            }

            if (cacheEntries.Length >= safeCapacity)
                return;

            Array.Resize(ref cacheEntries, safeCapacity);
        }

        public void SetGlobalLevel(ESGlobalLODLevel level)
        {
            if (globalLevel == level)
                return;

            globalLevel = level;
            globalVersion++;
            RefreshAllResolvedCaches();
        }

        public int Register(int key, ESLODLevel initialLevel = ESLODLevel.Full, ESLODGate initialGate = ESLODGate.None)
        {
            if (key == 0)
                return -1;

            if (keyToIndex.TryGetValue(key, out int existingIndex))
                return existingIndex;

            int index;
            if (freeIndices.Count > 0)
            {
                index = freeIndices.Pop();
            }
            else
            {
                EnsureCapacityForOneMore();
                index = cacheCount++;
            }

            keyToIndex.Add(key, index);
            cacheEntries[index] = CreateEntry(key, initialLevel, initialGate);
            activeCount++;
            return index;
        }

        public void Unregister(int key)
        {
            if (!keyToIndex.TryGetValue(key, out int index))
                return;

            cacheEntries[index] = default;
            freeIndices.Push(index);
            activeCount--;
            keyToIndex.Remove(key);
        }

        public bool TryGetCacheIndex(int key, out int index)
        {
            return keyToIndex.TryGetValue(key, out index);
        }

        public ref readonly ESLODCacheEntry GetCacheReadOnly(int cacheIndex)
        {
            return ref cacheEntries[cacheIndex];
        }

        public ESLODLevel GetResolvedLevelFast(int cacheIndex)
        {
            return cacheEntries[cacheIndex].resolvedLevel;
        }

        public ESLODGate GetGateFast(int cacheIndex)
        {
            return cacheEntries[cacheIndex].gate;
        }

        public float GetMaxCatchupDeltaTimeFast(int cacheIndex)
        {
            return cacheEntries[cacheIndex].maxCatchupDeltaTime;
        }

        public bool IsValidCacheIndex(int cacheIndex)
        {
            return cacheEntries != null
                   && cacheIndex >= 0
                   && cacheIndex < cacheCount
                   && cacheEntries[cacheIndex].active;
        }

        public void SetLevel(int cacheIndex, ESLODLevel level)
        {
            if (!IsValidCacheIndex(cacheIndex))
                return;

            ref ESLODCacheEntry entry = ref cacheEntries[cacheIndex];
            if (entry.level == level)
                return;

            entry.level = level;
            RefreshResolvedCache(ref entry);
        }

        public void SetGate(int cacheIndex, ESLODGate gate)
        {
            if (!IsValidCacheIndex(cacheIndex))
                return;

            ref ESLODCacheEntry entry = ref cacheEntries[cacheIndex];
            if (entry.gate == gate)
                return;

            entry.gate = gate;
            RefreshResolvedCache(ref entry);
        }

        public void AddGate(int cacheIndex, ESLODGate gate)
        {
            if (!IsValidCacheIndex(cacheIndex))
                return;

            ref ESLODCacheEntry entry = ref cacheEntries[cacheIndex];
            ESLODGate next = entry.gate | gate;
            if (entry.gate == next)
                return;

            entry.gate = next;
            RefreshResolvedCache(ref entry);
        }

        public void RemoveGate(int cacheIndex, ESLODGate gate)
        {
            if (!IsValidCacheIndex(cacheIndex))
                return;

            ref ESLODCacheEntry entry = ref cacheEntries[cacheIndex];
            ESLODGate next = entry.gate & ~gate;
            if (entry.gate == next)
                return;

            entry.gate = next;
            RefreshResolvedCache(ref entry);
        }

        private ESLODCacheEntry CreateEntry(int key, ESLODLevel level, ESLODGate gate)
        {
            var entry = new ESLODCacheEntry
            {
                key = key,
                globalLevel = globalLevel,
                level = level,
                gate = gate,
                active = true
            };
            RefreshResolvedCache(ref entry);
            return entry;
        }

        private void RefreshAllResolvedCaches()
        {
            for (int i = 0; i < cacheCount; i++)
            {
                if (!cacheEntries[i].active)
                    continue;

                RefreshResolvedCache(ref cacheEntries[i]);
            }
        }

        private void RefreshResolvedCache(ref ESLODCacheEntry entry)
        {
            entry.globalLevel = globalLevel;
            entry.resolvedLevel = ResolveLevel(entry.level, entry.gate, globalLevel);
            entry.maxCatchupDeltaTime = ResolveMaxCatchupDeltaTime(entry.resolvedLevel, globalLevel);
            entry.version = ++globalVersion;
        }

        private static ESLODLevel ResolveLevel(ESLODLevel level, ESLODGate gate, ESGlobalLODLevel global)
        {
            if ((gate & (ESLODGate.Stop | ESLODGate.Death | ESLODGate.AlwaysSleep)) != 0)
                return ESLODLevel.Sleep;

            if ((gate & ESLODGate.Pause) != 0)
                return ESLODLevel.Sleep;

            if ((gate & ESLODGate.AlwaysFull) != 0)
                return ESLODLevel.Full;

            if (global == ESGlobalLODLevel.Stress && level < ESLODLevel.VisualOnly)
                return ESLODLevel.VisualOnly;

            if (global == ESGlobalLODLevel.Performance && level < ESLODLevel.Reduced)
                return ESLODLevel.Reduced;

            return level;
        }

        private float ResolveMaxCatchupDeltaTime(ESLODLevel level, ESGlobalLODLevel global)
        {
            if (level == ESLODLevel.Sleep)
                return 0f;

            float value = Mathf.Max(0.001f, maxCatchupDeltaTime);
            if (global == ESGlobalLODLevel.Stress)
                value = Mathf.Min(value, 0.05f);
            return value;
        }

        private void EnsureCapacityForOneMore()
        {
            if (cacheEntries == null)
                Warmup(warmupCapacity);

            if (cacheCount < cacheEntries.Length)
                return;

            int nextCapacity = Mathf.Max(cacheEntries.Length + 1, cacheEntries.Length * 2);
            Array.Resize(ref cacheEntries, nextCapacity);
        }
    }
}
