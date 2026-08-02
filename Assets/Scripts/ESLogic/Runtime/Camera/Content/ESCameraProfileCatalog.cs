using System;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// ProfileKey 到内容 Profile 的只读索引。索引仅在资产载入或 Inspector 改动后重建，
    /// Director 的热路径不会扫描 List 或执行资源查询。
    /// </summary>
    [CreateAssetMenu(menuName = "ES/Camera/Profile Catalog", fileName = "ESCameraProfileCatalog")]
    public sealed class ESCameraProfileCatalog : ScriptableObject
    {
        [SerializeField] private List<ESCameraProfile> profiles = new List<ESCameraProfile>();

        [NonSerialized] private Dictionary<string, ESCameraProfile> byKey;

        public bool TryGet(string profileKey, out ESCameraProfile profile)
        {
            profile = null;
            EnsureIndex();
            return !string.IsNullOrWhiteSpace(profileKey) && byKey.TryGetValue(profileKey, out profile) && profile != null;
        }

        private void OnEnable()
        {
            RebuildIndex();
        }

#if UNITY_EDITOR
        /// <summary>明确的内容制作入口；避免编辑器工具窥探或反射 Runtime 私有字段。</summary>
        public void SetProfilesForAuthoring(IReadOnlyList<ESCameraProfile> source)
        {
            profiles.Clear();
            if (source != null)
            {
                for (int i = 0; i < source.Count; i++)
                {
                    if (source[i] != null)
                        profiles.Add(source[i]);
                }
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
                byKey = new Dictionary<string, ESCameraProfile>(profiles != null ? profiles.Count : 0, StringComparer.Ordinal);
            else
                byKey.Clear();

            if (profiles == null)
                return;

            for (int i = 0; i < profiles.Count; i++)
            {
                ESCameraProfile profile = profiles[i];
                if (profile == null || !profile.IsValid || byKey.ContainsKey(profile.profileKey))
                    continue;

                byKey.Add(profile.profileKey, profile);
            }
        }
    }
}
