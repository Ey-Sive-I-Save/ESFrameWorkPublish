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

        public Transform Resolve(DefaultTransformKey key)
        {
            return defaultMap != null && defaultMap.TryGetValue(key, out var t) ? t : null;
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
