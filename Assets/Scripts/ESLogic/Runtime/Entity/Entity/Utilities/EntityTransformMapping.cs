using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

[assembly: InternalsVisibleTo("ES_Logic.Editor")]
[assembly: InternalsVisibleTo("ES_Logic.Editor.Generation.Tests")]

namespace ES
{
    public enum DefaultTransformKey
    {
        Root = 0,
        Head = 1,
        Chest = 2,
        Hip = 3,
        LeftHand = 4,
        RightHand = 5,
        LeftFoot = 6,
        RightFoot = 7,
        Camera = 9,
        CustomA = 10,
        CustomB = 11
    }

    /// <summary>Allocation-free read-only access to one entity's serialized transform map.</summary>
    public readonly struct EntityTransformMapView
    {
        private readonly EntityTransformMap map;

        internal EntityTransformMapView(EntityTransformMap map)
        {
            this.map = map;
        }

        public bool IsCreated => map != null;
        public int Count => map?.Count ?? 0;
        public int Generation => map?.Generation ?? 0;
        public bool IsValid => map != null && map.IsValid;
        internal EntityTransformMap.Conflict LastConflict => map != null ? map.LastConflict : default;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Transform Resolve(DefaultTransformKey key)
        {
            return map != null ? map.Resolve(key) : null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Transform Resolve(string key)
        {
            return map != null ? map.Resolve(key) : null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGet(DefaultTransformKey key, out Transform value)
        {
            if (map != null)
                return map.TryGet(key, out value);
            value = null;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGet(string key, out Transform value)
        {
            if (map != null)
                return map.TryGet(key, out value);
            value = null;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGet(DefaultTransformKey defaultKey, string stringKey, out Transform value)
        {
            if (map != null)
                return map.TryGet(defaultKey, stringKey, out value);
            value = null;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ContainsAlias(DefaultTransformKey key)
        {
            return map != null && map.ContainsAlias(key);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ContainsAlias(string key)
        {
            return map != null && map.ContainsAlias(key);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ContainsKey(DefaultTransformKey key)
        {
            return map != null && map.ContainsKey(key);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ContainsKey(string key)
        {
            return map != null && map.ContainsKey(key);
        }

        internal bool TryGetEntryAt(int index, out EntityTransformMap.Entry entry)
        {
            if (map != null)
                return map.TryGetEntryAt(index, out entry);
            entry = default;
            return false;
        }

        internal void CopyEntries(List<EntityTransformMap.Entry> destination)
        {
            if (destination == null)
                throw new ArgumentNullException(nameof(destination));
            map?.CopyEntries(destination);
        }
    }

    /// <summary>
    /// Entity-specific serialized identity for transform sockets. Generic mirror mechanics stay in the shared container.
    /// </summary>
    [Serializable]
    internal sealed class EntityTransformMap : ESEnumStringMirrorMap<DefaultTransformKey, Transform>
    {
        [NonSerialized] private Dictionary<string, Transform> runtimeDynamicValues;
        [NonSerialized] private int observedBaseGeneration;
        [NonSerialized] private int combinedGeneration;

        public new int Generation
        {
            get
            {
                SyncBaseGeneration();
                return combinedGeneration;
            }
        }

        public Transform Resolve(DefaultTransformKey key)
        {
            return TryGetValue(key, out Transform value) ? value : null;
        }

        public Transform Resolve(string key)
        {
            return TryGet(key, out Transform value) ? value : null;
        }

        public bool TryGet(DefaultTransformKey key, out Transform value)
        {
            return TryGetValue(key, out value);
        }

        public bool TryGet(string key, out Transform value)
        {
            if (key != null
                && runtimeDynamicValues != null
                && runtimeDynamicValues.TryGetValue(key, out value)
                && value != null)
            {
                return true;
            }

            return TryGetValue(key, out value);
        }

        public bool TryGet(DefaultTransformKey defaultKey, string stringKey, out Transform value)
        {
            return TryGetValue(defaultKey, stringKey, out value);
        }

        public bool TrySetDynamic(string key, Transform value, out Conflict conflict)
        {
            if (string.IsNullOrEmpty(key))
            {
                conflict = NewConflict(
                    ConflictKind.MissingKey,
                    -1,
                    -1,
                    "A non-empty dynamic string key is required.");
                return false;
            }

            if (!ValidateStringKey(key, true, -1, out conflict)
                || !ValidateValue(value, -1, out conflict))
            {
                return false;
            }

            if (!base.IsValid)
            {
                conflict = base.LastConflict;
                return false;
            }

            if (base.ContainsAlias(key))
            {
                return base.TryAdd(key, value, out conflict);
            }

            runtimeDynamicValues ??= new Dictionary<string, Transform>(StringComparer.Ordinal);
            runtimeDynamicValues[key] = value;
            AdvanceCombinedGeneration();
            conflict = Conflict.None;
            return true;
        }

        public new bool TrySet(string key, Transform value, out Conflict conflict)
        {
            bool removedDynamic = TryTakeDynamicValue(key, out Transform dynamicValue);
            bool committed = base.TrySet(key, value, out conflict);
            if (!committed)
            {
                RestoreDynamicValue(key, dynamicValue, removedDynamic);
                return false;
            }
            if (removedDynamic)
                AdvanceCombinedGeneration();
            return committed;
        }

        public new bool TrySet(
            DefaultTransformKey defaultKey,
            string stringKey,
            Transform value,
            out Conflict conflict)
        {
            bool removedDynamic = TryTakeDynamicValue(stringKey, out Transform dynamicValue);
            bool committed = base.TrySet(defaultKey, stringKey, value, out conflict);
            if (!committed)
            {
                RestoreDynamicValue(stringKey, dynamicValue, removedDynamic);
                return false;
            }
            if (removedDynamic)
                AdvanceCombinedGeneration();
            return committed;
        }

        public new bool Remove(string key)
        {
            if (key != null
                && runtimeDynamicValues != null
                && runtimeDynamicValues.Remove(key))
            {
                AdvanceCombinedGeneration();
                return true;
            }

            bool removed = base.Remove(key);
            return removed;
        }

        public void ClearDynamicOnlyEntries()
        {
            if (runtimeDynamicValues == null || runtimeDynamicValues.Count == 0)
                return;

            runtimeDynamicValues.Clear();
            AdvanceCombinedGeneration();
        }

        public new void Clear()
        {
            base.Clear();
        }

        protected override bool ValidateAdditionalEntry(
            Entry entry,
            int entryIndex,
            int ignoredIndex,
            out Conflict conflict)
        {
            if (entry.HasStringKey
                && runtimeDynamicValues != null
                && runtimeDynamicValues.ContainsKey(entry.stringKey))
            {
                conflict = NewConflict(
                    ConflictKind.DuplicateStringKey,
                    entryIndex,
                    -1,
                    "String alias conflicts with a runtime-dynamic entry: " + entry.stringKey + ".");
                return false;
            }

            conflict = Conflict.None;
            return true;
        }

        protected override void OnAuthorityCleared()
        {
            ClearDynamicOnlyEntries();
        }

        private bool TryTakeDynamicValue(string key, out Transform value)
        {
            value = null;
            if (key == null
                || runtimeDynamicValues == null
                || !runtimeDynamicValues.TryGetValue(key, out value))
            {
                return false;
            }

            runtimeDynamicValues.Remove(key);
            return true;
        }

        private void RestoreDynamicValue(string key, Transform value, bool removed)
        {
            if (removed)
                runtimeDynamicValues[key] = value;
        }

        private void SyncBaseGeneration()
        {
            int currentBaseGeneration = base.Generation;
            if (currentBaseGeneration == observedBaseGeneration)
                return;

            observedBaseGeneration = currentBaseGeneration;
            AdvanceCombinedGenerationWithoutSync();
        }

        private void AdvanceCombinedGeneration()
        {
            SyncBaseGeneration();
            AdvanceCombinedGenerationWithoutSync();
        }

        private void AdvanceCombinedGenerationWithoutSync()
        {
            unchecked
            {
                combinedGeneration++;
                if (combinedGeneration == 0)
                    combinedGeneration++;
            }
        }
    }

    [Serializable]
    [RequireComponent(typeof(Entity))]
    public class EntityTransformMapping : MonoBehaviour
    {
        [Header("Transform Mappings")]
        [Tooltip("Enum 用于固定高频挂点，String 用于低频扩展；双键可作为同一挂点的两个别名。")]
        [ESEnumStringTable(
            EnumColumn = "固定挂点",
            StringColumn = "稳定 String Key",
            ValueColumn = "Transform",
            NewEntryMode = ESEnumStringTableNewEntryMode.EnumAndString)]
        [SerializeField] private EntityTransformMap transformMappings = new EntityTransformMap();

        public EntityTransformMapView TransformMappings
        {
            get
            {
                EnsureMap();
                return new EntityTransformMapView(transformMappings);
            }
        }

        internal int MappingGeneration
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                EnsureMap();
                return transformMappings.Generation;
            }
        }

        internal bool IsMappingValid
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                EnsureMap();
                return transformMappings.IsValid;
            }
        }

        internal EntityTransformMap.Conflict LastMappingConflict
        {
            get
            {
                EnsureMap();
                return transformMappings.LastConflict;
            }
        }

        private void Awake()
        {
            RebuildRuntimeCache();
            GetComponent<Entity>()?.BindTransformMapping(this);
        }

        private void OnValidate()
        {
            RebuildRuntimeCache();
        }

        /// <summary>
        /// 从 Unity 序列化条目重建 Enum 连续数组与 String 字典镜像。
        /// </summary>
        internal void RebuildRuntimeCache()
        {
            EnsureMap();
            transformMappings.MarkDirty();
            if (!transformMappings.TryRebuild(out EntityTransformMap.Conflict conflict))
                Debug.LogError("[EntityTransformMapping] 挂点镜像重建失败：" + conflict.Message, this);
        }

        public Transform Resolve(DefaultTransformKey key)
        {
            EnsureMap();
            return transformMappings.Resolve(key);
        }

        public Transform Resolve(string key)
        {
            EnsureMap();
            return transformMappings.Resolve(key);
        }

        internal bool Set(DefaultTransformKey key, Transform transform)
        {
            EnsureMap();
            return transformMappings.TrySet(key, transform, out _);
        }

        internal bool Set(
            DefaultTransformKey key,
            Transform transform,
            out EntityTransformMap.Conflict conflict)
        {
            EnsureMap();
            return transformMappings.TrySet(key, transform, out conflict);
        }

        internal bool Set(string key, Transform transform)
        {
            EnsureMap();
            return transformMappings.TrySet(key, transform, out _);
        }

        internal bool Set(string key, Transform transform, out EntityTransformMap.Conflict conflict)
        {
            EnsureMap();
            return transformMappings.TrySet(key, transform, out conflict);
        }

        public bool SetDynamic(string key, Transform transform)
        {
            EnsureMap();
            return transformMappings.TrySetDynamic(key, transform, out _);
        }

        internal bool SetDynamic(string key, Transform transform, out EntityTransformMap.Conflict conflict)
        {
            EnsureMap();
            return transformMappings.TrySetDynamic(key, transform, out conflict);
        }

        internal bool Set(
            DefaultTransformKey defaultKey,
            string stringKey,
            Transform transform,
            out EntityTransformMap.Conflict conflict)
        {
            EnsureMap();
            return transformMappings.TrySet(defaultKey, stringKey, transform, out conflict);
        }

        internal bool Remove(DefaultTransformKey key)
        {
            EnsureMap();
            return transformMappings.Remove(key);
        }

        internal bool Remove(string key)
        {
            EnsureMap();
            return transformMappings.Remove(key);
        }

        public void ClearDynamic()
        {
            EnsureMap();
            transformMappings.ClearDynamicOnlyEntries();
        }

        private void EnsureMap()
        {
            if (transformMappings == null)
                transformMappings = new EntityTransformMap();
        }

    }
}
