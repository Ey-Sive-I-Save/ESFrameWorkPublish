using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    public enum ESGlobalLODLevel : byte
    {
        HighQuality = 0,
        Balanced = 1,
        Performance = 2,
        Stress = 3
    }

    public enum ESEntityLODLevel : byte
    {
        Full = 0,
        Reduced = 1,
        VisualOnly = 2,
        Sleep = 3
    }

    [Flags]
    public enum ESEntityLODGate : ushort
    {
        None = 0,
        Frozen = 1 << 0,
        Disabled = 1 << 1,
        Dead = 1 << 2,
        ForceFull = 1 << 3,
        ForceSleep = 1 << 4
    }

    public struct ESLODCacheEntry
    {
        public int entityId;
        public ESGlobalLODLevel globalLevel;
        public ESEntityLODLevel entityLevel;
        public ESEntityLODGate gate;
        public ESEntityLODLevel resolvedLevel;
        public int stateMachineUpdateInterval;
        public int ikUpdateInterval;
        public int aiUpdateInterval;
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
        [LabelText("Reduced状态机间隔"), MinValue(1)]
        public int reducedStateMachineInterval = 2;

        [LabelText("Reduced IK间隔"), MinValue(1)]
        public int reducedIKInterval = 2;

        [LabelText("Reduced AI间隔"), MinValue(1)]
        public int reducedAIInterval = 5;

        [LabelText("VisualOnly状态机间隔"), MinValue(1)]
        public int visualOnlyStateMachineInterval = 4;

        [LabelText("最大补偿时间"), MinValue(0.001f)]
        public float maxCatchupDeltaTime = 0.066f;

        [ShowInInspector, ReadOnly, LabelText("已注册实体")]
        public int RegisteredCount => activeCount;

        private readonly Dictionary<int, int> entityIdToIndex = new Dictionary<int, int>(256);
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

        public int RegisterEntity(Entity entity, ESEntityLODLevel initialLevel = ESEntityLODLevel.Full, ESEntityLODGate initialGate = ESEntityLODGate.None)
        {
            if (entity == null)
                return -1;

            return RegisterEntityId(entity.GetInstanceID(), initialLevel, initialGate);
        }

        public int RegisterEntityId(int entityId, ESEntityLODLevel initialLevel = ESEntityLODLevel.Full, ESEntityLODGate initialGate = ESEntityLODGate.None)
        {
            if (entityId == 0)
                return -1;

            if (entityIdToIndex.TryGetValue(entityId, out int existingIndex))
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

            entityIdToIndex.Add(entityId, index);
            cacheEntries[index] = CreateEntry(entityId, initialLevel, initialGate);
            activeCount++;
            return index;
        }

        public void UnregisterEntity(Entity entity)
        {
            if (entity == null)
                return;

            UnregisterEntityId(entity.GetInstanceID());
        }

        public void UnregisterEntityId(int entityId)
        {
            if (!entityIdToIndex.TryGetValue(entityId, out int index))
                return;

            cacheEntries[index] = default;
            freeIndices.Push(index);
            activeCount--;
            entityIdToIndex.Remove(entityId);
        }

        public bool TryGetCacheIndex(int entityId, out int index)
        {
            return entityIdToIndex.TryGetValue(entityId, out index);
        }

        public ref readonly ESLODCacheEntry GetCacheReadOnly(int cacheIndex)
        {
            return ref cacheEntries[cacheIndex];
        }

        public ESEntityLODLevel GetResolvedLevelFast(int cacheIndex)
        {
            return cacheEntries[cacheIndex].resolvedLevel;
        }

        public ESEntityLODGate GetGateFast(int cacheIndex)
        {
            return cacheEntries[cacheIndex].gate;
        }

        public int GetStateMachineUpdateIntervalFast(int cacheIndex)
        {
            return cacheEntries[cacheIndex].stateMachineUpdateInterval;
        }

        public int GetIKUpdateIntervalFast(int cacheIndex)
        {
            return cacheEntries[cacheIndex].ikUpdateInterval;
        }

        public int GetAIUpdateIntervalFast(int cacheIndex)
        {
            return cacheEntries[cacheIndex].aiUpdateInterval;
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

        public void SetEntityLevel(int cacheIndex, ESEntityLODLevel level)
        {
            if (!IsValidCacheIndex(cacheIndex))
                return;

            ref ESLODCacheEntry entry = ref cacheEntries[cacheIndex];
            if (entry.entityLevel == level)
                return;

            entry.entityLevel = level;
            RefreshResolvedCache(ref entry);
        }

        public void SetEntityGate(int cacheIndex, ESEntityLODGate gate)
        {
            if (!IsValidCacheIndex(cacheIndex))
                return;

            ref ESLODCacheEntry entry = ref cacheEntries[cacheIndex];
            if (entry.gate == gate)
                return;

            entry.gate = gate;
            RefreshResolvedCache(ref entry);
        }

        public void AddEntityGate(int cacheIndex, ESEntityLODGate gate)
        {
            if (!IsValidCacheIndex(cacheIndex))
                return;

            ref ESLODCacheEntry entry = ref cacheEntries[cacheIndex];
            ESEntityLODGate next = entry.gate | gate;
            if (entry.gate == next)
                return;

            entry.gate = next;
            RefreshResolvedCache(ref entry);
        }

        public void RemoveEntityGate(int cacheIndex, ESEntityLODGate gate)
        {
            if (!IsValidCacheIndex(cacheIndex))
                return;

            ref ESLODCacheEntry entry = ref cacheEntries[cacheIndex];
            ESEntityLODGate next = entry.gate & ~gate;
            if (entry.gate == next)
                return;

            entry.gate = next;
            RefreshResolvedCache(ref entry);
        }

        private ESLODCacheEntry CreateEntry(int entityId, ESEntityLODLevel level, ESEntityLODGate gate)
        {
            var entry = new ESLODCacheEntry
            {
                entityId = entityId,
                globalLevel = globalLevel,
                entityLevel = level,
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
            entry.resolvedLevel = ResolveLevel(entry.entityLevel, entry.gate, globalLevel);
            ResolveIntervals(entry.resolvedLevel, globalLevel, out entry.stateMachineUpdateInterval, out entry.ikUpdateInterval, out entry.aiUpdateInterval);
            entry.maxCatchupDeltaTime = ResolveMaxCatchupDeltaTime(entry.resolvedLevel, globalLevel);
            entry.version = ++globalVersion;
        }

        private static ESEntityLODLevel ResolveLevel(ESEntityLODLevel entityLevel, ESEntityLODGate gate, ESGlobalLODLevel global)
        {
            if ((gate & (ESEntityLODGate.Disabled | ESEntityLODGate.Dead | ESEntityLODGate.ForceSleep)) != 0)
                return ESEntityLODLevel.Sleep;

            if ((gate & ESEntityLODGate.Frozen) != 0)
                return ESEntityLODLevel.Sleep;

            if ((gate & ESEntityLODGate.ForceFull) != 0)
                return ESEntityLODLevel.Full;

            if (global == ESGlobalLODLevel.Stress && entityLevel < ESEntityLODLevel.VisualOnly)
                return ESEntityLODLevel.VisualOnly;

            if (global == ESGlobalLODLevel.Performance && entityLevel < ESEntityLODLevel.Reduced)
                return ESEntityLODLevel.Reduced;

            return entityLevel;
        }

        private void ResolveIntervals(ESEntityLODLevel level, ESGlobalLODLevel global, out int stateMachine, out int ik, out int ai)
        {
            switch (level)
            {
                case ESEntityLODLevel.Full:
                    stateMachine = 1;
                    ik = 1;
                    ai = 1;
                    break;
                case ESEntityLODLevel.Reduced:
                    stateMachine = Mathf.Max(1, reducedStateMachineInterval);
                    ik = Mathf.Max(1, reducedIKInterval);
                    ai = Mathf.Max(1, reducedAIInterval);
                    break;
                case ESEntityLODLevel.VisualOnly:
                    stateMachine = Mathf.Max(1, visualOnlyStateMachineInterval);
                    ik = 0;
                    ai = 0;
                    break;
                default:
                    stateMachine = 0;
                    ik = 0;
                    ai = 0;
                    break;
            }

            if (global == ESGlobalLODLevel.Stress && level == ESEntityLODLevel.Reduced)
            {
                stateMachine = Mathf.Max(stateMachine, 4);
                ik = Mathf.Max(ik, 4);
                ai = Mathf.Max(ai, 8);
            }
        }

        private float ResolveMaxCatchupDeltaTime(ESEntityLODLevel level, ESGlobalLODLevel global)
        {
            if (level == ESEntityLODLevel.Sleep)
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
