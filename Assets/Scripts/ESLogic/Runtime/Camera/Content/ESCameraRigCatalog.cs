using System;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// RigKey 到 Rig Prefab 的内容目录。此资产绝不保存当前场景的 VCam 实例；实例
    /// 只由 ESCameraSceneRigRegistry 在其所属 View 生命周期中创建和销毁。
    /// </summary>
    [CreateAssetMenu(menuName = "ES/Camera/Rig Catalog", fileName = "ESCameraRigCatalog")]
    public sealed class ESCameraRigCatalog : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string rigKey;
            public GameObject rigPrefab;
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();
        [NonSerialized] private Dictionary<string, GameObject> byKey;

        public int EntryCount => entries != null ? entries.Count : 0;

        public bool TryGetPrefab(string rigKey, out GameObject prefab)
        {
            prefab = null;
            EnsureIndex();
            return !string.IsNullOrWhiteSpace(rigKey) && byKey.TryGetValue(rigKey, out prefab) && prefab != null;
        }

        /// <summary>供 SceneBinding 预热当前 View 的所有已配置 Rig；不暴露可写集合。</summary>
        public bool TryGetEntry(int index, out string rigKey, out GameObject prefab)
        {
            rigKey = null;
            prefab = null;
            if (entries == null || (uint)index >= (uint)entries.Count)
                return false;

            Entry entry = entries[index];
            rigKey = entry.rigKey;
            prefab = entry.rigPrefab;
            return !string.IsNullOrWhiteSpace(rigKey) && prefab != null;
        }

        private void OnEnable()
        {
            RebuildIndex();
        }

#if UNITY_EDITOR
        /// <summary>明确的内容制作入口；资产只保存 Prefab 定义，绝不写入场景 Rig 实例。</summary>
        public void SetEntriesForAuthoring(IReadOnlyList<Entry> source)
        {
            entries.Clear();
            if (source != null)
            {
                for (int i = 0; i < source.Count; i++)
                    entries.Add(source[i]);
            }

            RebuildIndex();
        }

        private void OnValidate()
        {
            RebuildIndex();
        }
#endif

        private void EnsureIndex()
        {
            if (byKey == null)
                RebuildIndex();
        }

        private void RebuildIndex()
        {
            if (byKey == null)
                byKey = new Dictionary<string, GameObject>(entries != null ? entries.Count : 0, StringComparer.Ordinal);
            else
                byKey.Clear();

            if (entries == null)
                return;

            for (int i = 0; i < entries.Count; i++)
            {
                Entry entry = entries[i];
                if (string.IsNullOrWhiteSpace(entry.rigKey) || entry.rigPrefab == null || byKey.ContainsKey(entry.rigKey))
                    continue;

                byKey.Add(entry.rigKey, entry.rigPrefab);
            }
        }
    }
}
