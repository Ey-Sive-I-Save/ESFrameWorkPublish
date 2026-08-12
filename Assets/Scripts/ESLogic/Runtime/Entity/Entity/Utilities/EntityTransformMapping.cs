using System;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{
    public enum DefaultTransformKey
    {
        Root,
        Head,
        Chest,
        Hip,
        LeftHand,
        RightHand,
        LeftFoot,
        RightFoot,
        Weapon,
        Camera,
        CustomA,
        CustomB
    }

    /// <summary>
    /// Entity-specific serialized identity for transform sockets. Generic mirror mechanics stay in the shared container.
    /// </summary>
    [Serializable]
    public sealed class EntityTransformMap : ESEnumStringMirrorMap<DefaultTransformKey, Transform>
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
            bool hasStringKey = !string.IsNullOrEmpty(key);
            if (!ValidateStringKey(key, hasStringKey, -1, out conflict)
                || !ValidateValue(value, -1, out conflict))
            {
                return false;
            }

            if (base.ContainsKey(key))
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
            bool committed = base.TrySet(key, value, out conflict);
            if (committed)
                runtimeDynamicValues?.Remove(key);
            return committed;
        }

        public new bool TrySet(
            DefaultTransformKey defaultKey,
            string stringKey,
            Transform value,
            out Conflict conflict)
        {
            bool committed = base.TrySet(defaultKey, stringKey, value, out conflict);
            if (committed)
                runtimeDynamicValues?.Remove(stringKey);
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
            if (runtimeDynamicValues != null && runtimeDynamicValues.Count > 0)
            {
                runtimeDynamicValues.Clear();
                AdvanceCombinedGeneration();
            }
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

#if UNITY_EDITOR
        [Serializable]
        public struct LegacyOdinSerializationNode
        {
            public string Name;
            public int Entry;
            public string Data;
        }

        [Serializable]
        public struct LegacyOdinSerializationData
        {
            public int SerializedFormat;
            public byte[] SerializedBytes;
            public List<UnityEngine.Object> ReferencedUnityObjects;
            public string SerializedBytesString;
            public UnityEngine.Object Prefab;
            public List<UnityEngine.Object> PrefabModificationsReferencedUnityObjects;
            public List<string> PrefabModifications;
            public List<LegacyOdinSerializationNode> SerializationNodes;

            public bool ContainsData => (SerializedBytes != null && SerializedBytes.Length > 0)
                                        || !string.IsNullOrEmpty(SerializedBytesString)
                                        || (SerializationNodes != null && SerializationNodes.Count > 0);

            public void Reset()
            {
                this = default;
            }
        }

        // One-version migration bridge for the former SerializedMonoBehaviour payload.
        // Runtime code never reads this data; the explicit editor migration must consume and clear it.
        [SerializeField, HideInInspector] private LegacyOdinSerializationData serializationData;

        public bool HasLegacyOdinMappings => serializationData.ContainsData;
#endif

        public EntityTransformMap TransformMappings
        {
            get
            {
                EnsureMap();
                return transformMappings;
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
        public void RebuildRuntimeCache()
        {
#if UNITY_EDITOR
            if (HasLegacyOdinMappings)
            {
                Debug.LogError(
                    "[EntityTransformMapping] 检测到尚未迁移的 Odin 挂点数据。请先执行角色挂点显式迁移，禁止把空的新表当作有效结果。",
                    this);
                return;
            }
#endif
            EnsureMap();
            transformMappings.MarkDirty();
            if (!transformMappings.TryRebuild(out EntityTransformMap.Conflict conflict))
                Debug.LogError("[EntityTransformMapping] 挂点镜像重建失败：" + conflict.Message, this);
        }

#if UNITY_EDITOR
        public LegacyOdinSerializationData CopyLegacyOdinSerializationData()
        {
            return serializationData;
        }

        public void ClearLegacyOdinSerializationData()
        {
            serializationData.Reset();
        }
#endif

        public Transform Resolve(DefaultTransformKey key)
        {
            return TransformMappings.Resolve(key);
        }

        public Transform Resolve(string key)
        {
            return TransformMappings.Resolve(key);
        }

        public bool Set(DefaultTransformKey key, Transform transform)
        {
            return TransformMappings.TrySet(key, transform, out _);
        }

        public bool Set(
            DefaultTransformKey key,
            Transform transform,
            out EntityTransformMap.Conflict conflict)
        {
            return TransformMappings.TrySet(key, transform, out conflict);
        }

        public bool Set(string key, Transform transform)
        {
            return TransformMappings.TrySet(key, transform, out _);
        }

        public bool Set(string key, Transform transform, out EntityTransformMap.Conflict conflict)
        {
            return TransformMappings.TrySet(key, transform, out conflict);
        }

        public bool SetDynamic(string key, Transform transform)
        {
            return TransformMappings.TrySetDynamic(key, transform, out _);
        }

        public bool SetDynamic(string key, Transform transform, out EntityTransformMap.Conflict conflict)
        {
            return TransformMappings.TrySetDynamic(key, transform, out conflict);
        }

        public bool Set(
            DefaultTransformKey defaultKey,
            string stringKey,
            Transform transform,
            out EntityTransformMap.Conflict conflict)
        {
            return TransformMappings.TrySet(defaultKey, stringKey, transform, out conflict);
        }

        public bool Remove(DefaultTransformKey key)
        {
            return TransformMappings.Remove(key);
        }

        public bool Remove(string key)
        {
            return TransformMappings.Remove(key);
        }

        public void ClearDynamic()
        {
            TransformMappings.ClearDynamicOnlyEntries();
        }

        private void EnsureMap()
        {
            if (transformMappings == null)
                transformMappings = new EntityTransformMap();
        }

    }
}
