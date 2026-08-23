using System;
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

    /// <summary>
    /// Entity-specific serialized identity for transform sockets. Authoring and runtime mutations share the base entries authority.
    /// </summary>
    [Serializable]
    public sealed class EntityTransformMap : ESEnumStringMirrorMap<DefaultTransformKey, Transform>
    {
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
            return TryGetValue(key, out value);
        }

        public bool TryGet(DefaultTransformKey defaultKey, string stringKey, out Transform value)
        {
            return TryGetValue(defaultKey, stringKey, out value);
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

        public EntityTransformMap TransformMappings
        {
            get
            {
                EnsureMap();
                return transformMappings;
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

        private void EnsureMap()
        {
            if (transformMappings == null)
                transformMappings = new EntityTransformMap();
        }

    }
}
