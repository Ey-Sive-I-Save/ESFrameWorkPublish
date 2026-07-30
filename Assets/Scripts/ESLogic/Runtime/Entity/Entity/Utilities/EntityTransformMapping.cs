using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
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

    [Serializable]
    public class EntityTransformMapping : SerializedMonoBehaviour
    {
        [Title("Default (Enum)")]
        [InfoBox("用于高频/固定语义的变换绑定，如 Root/Head/Hand 等。")]
        [OdinSerialize]
        public Dictionary<DefaultTransformKey, Transform> defaultMap = new Dictionary<DefaultTransformKey, Transform>();

        [Title("Dynamic (String)")]
        [InfoBox("用于复杂或运行期扩展的变换绑定，如 Skill/IK/Camera 等自定义 Key。")]
        [OdinSerialize]
        public Dictionary<string, Transform> dynamicMap = new Dictionary<string, Transform>();

        [NonSerialized] private Transform[] _defaultCache;
        [NonSerialized] private bool _runtimeCacheReady;

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
        /// 将枚举挂点压入连续数组。运行时读取固定语义挂点不会再走 Dictionary 或层级查找。
        /// </summary>
        public void RebuildRuntimeCache()
        {
            int count = Enum.GetValues(typeof(DefaultTransformKey)).Length;
            if (_defaultCache == null || _defaultCache.Length != count)
                _defaultCache = new Transform[count];

            Array.Clear(_defaultCache, 0, _defaultCache.Length);
            if (defaultMap != null)
            {
                foreach (KeyValuePair<DefaultTransformKey, Transform> pair in defaultMap)
                {
                    int index = (int)pair.Key;
                    if ((uint)index < (uint)_defaultCache.Length)
                        _defaultCache[index] = pair.Value;
                }
            }

            _runtimeCacheReady = true;
        }

        public Transform Resolve(DefaultTransformKey key)
        {
            if (!_runtimeCacheReady)
                RebuildRuntimeCache();

            int index = (int)key;
            return (uint)index < (uint)_defaultCache.Length ? _defaultCache[index] : null;
        }

        public Transform Resolve(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            return dynamicMap != null && dynamicMap.TryGetValue(key, out var t) ? t : null;
        }

        public void Set(DefaultTransformKey key, Transform transform)
        {
            if (defaultMap == null) defaultMap = new Dictionary<DefaultTransformKey, Transform>();
            defaultMap[key] = transform;

            if (!_runtimeCacheReady)
                return;

            int index = (int)key;
            if ((uint)index < (uint)_defaultCache.Length)
                _defaultCache[index] = transform;
        }

        public void Set(string key, Transform transform)
        {
            if (string.IsNullOrEmpty(key)) return;
            if (dynamicMap == null) dynamicMap = new Dictionary<string, Transform>();
            dynamicMap[key] = transform;
        }

        public bool Remove(DefaultTransformKey key)
        {
            return defaultMap != null && defaultMap.Remove(key);
        }

        public bool Remove(string key)
        {
            return dynamicMap != null && dynamicMap.Remove(key);
        }

        public void ClearDynamic()
        {
            dynamicMap?.Clear();
        }
    }
}
