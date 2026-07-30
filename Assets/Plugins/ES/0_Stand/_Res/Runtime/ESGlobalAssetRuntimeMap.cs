using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

namespace ES
{
    /// <summary>
    /// 全局运行时资产映射：只负责稳定资产身份到当前构建物理加载位置的映射。
    /// </summary>
    [CreateAssetMenu(menuName = "【ES】/资源与发布/运行时配置/全局资源运行时映射")]
    public class ESGlobalAssetRuntimeMap : ScriptableObject
    {
        [FormerlySerializedAs("packages")]
        [SerializeField] private ESRuntimeAssetBundleRecord[] assetBundles = Array.Empty<ESRuntimeAssetBundleRecord>();
        [SerializeField] private ESRuntimeAssetRecord[] assets = Array.Empty<ESRuntimeAssetRecord>();
        [SerializeField] private ESRuntimeSubAssetRecord[] subAssets = Array.Empty<ESRuntimeSubAssetRecord>();

        private Dictionary<string, ESRuntimeAssetRecord> mainAssetsByGuid;
        private Dictionary<ESSubAssetId, ESRuntimeSubAssetRecord> subAssetsById;
        private Dictionary<string, ESRuntimeAssetBundleRecord> assetBundlesByKey;

        public bool TryGetMainAsset(string guid, out ESRuntimeAssetRecord record)
        {
            EnsureIndex();
            if (!string.IsNullOrEmpty(guid))
                return mainAssetsByGuid.TryGetValue(guid, out record);

            record = null;
            return false;
        }

        public void SetRecords(ESRuntimeAssetBundleRecord[] assetBundleRecords, ESRuntimeAssetRecord[] assetRecords, ESRuntimeSubAssetRecord[] subAssetRecords)
        {
            assetBundles = assetBundleRecords ?? Array.Empty<ESRuntimeAssetBundleRecord>();
            assets = assetRecords ?? Array.Empty<ESRuntimeAssetRecord>();
            subAssets = subAssetRecords ?? Array.Empty<ESRuntimeSubAssetRecord>();
            ValidateReleaseRecordsOrThrow();
            RebuildRuntimeIndex();
        }

        /// <summary>
        /// Creates one immutable-in-practice runtime view from independently downloaded release
        /// fragments. A fragment may repeat a shared dependency, but it may never redefine an
        /// existing identity with different content. This keeps Consumer/Library on-demand
        /// loading deterministic and prevents a partial result from replacing the boot map.
        /// </summary>
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static ESGlobalAssetRuntimeMap Merge(ESGlobalAssetRuntimeMap current, ESGlobalAssetRuntimeMap addition)
        {
            if (addition == null) throw new ArgumentNullException(nameof(addition));
            if (current == null) return addition;

            var bundles = new Dictionary<string, ESRuntimeAssetBundleRecord>(StringComparer.Ordinal);
            var assets = new Dictionary<string, ESRuntimeAssetRecord>(StringComparer.Ordinal);
            var subAssets = new Dictionary<ESSubAssetId, ESRuntimeSubAssetRecord>();
            AddRecords(current, bundles, assets, subAssets);
            AddRecords(addition, bundles, assets, subAssets);

            var merged = CreateInstance<ESGlobalAssetRuntimeMap>();
            merged.SetRecords(bundles.Values.OrderBy(item => item.AssetBundleKey, StringComparer.Ordinal).ToArray(),
                assets.Values.OrderBy(item => item.Guid, StringComparer.Ordinal).ToArray(),
                subAssets.Values.OrderBy(item => item.Guid, StringComparer.Ordinal).ThenBy(item => item.LocalFileId).ToArray());
            return merged;
        }

        private static void AddRecords(ESGlobalAssetRuntimeMap source, Dictionary<string, ESRuntimeAssetBundleRecord> bundles,
            Dictionary<string, ESRuntimeAssetRecord> assets, Dictionary<ESSubAssetId, ESRuntimeSubAssetRecord> subAssets)
        {
            foreach (ESRuntimeAssetBundleRecord record in source.assetBundles)
                AddOrValidate(bundles, record.AssetBundleKey, record, SameBundle, "BundleKey");
            foreach (ESRuntimeAssetRecord record in source.assets)
                AddOrValidate(assets, record.Guid, record, SameAsset, "Asset GUID");
            foreach (ESRuntimeSubAssetRecord record in source.subAssets)
                AddOrValidate(subAssets, record.Id, record, SameSubAsset, "SubAsset identity");
        }

        private static void AddOrValidate<TKey, TValue>(Dictionary<TKey, TValue> records, TKey key, TValue value, Func<TValue, TValue, bool> same, string identity)
        {
            if (records.TryGetValue(key, out TValue existing))
            {
                if (!same(existing, value))
                    throw new InvalidOperationException("[ESRes][RuntimeMap] 增量发布内容重定义了 " + identity + "：" + key);
                return;
            }
            records.Add(key, value);
        }

        private static bool SameBundle(ESRuntimeAssetBundleRecord left, ESRuntimeAssetBundleRecord right)
        {
            return left.Size == right.Size && left.Crc == right.Crc
                && string.Equals(left.ContentHash, right.ContentHash, StringComparison.OrdinalIgnoreCase)
                && string.Equals(left.LocalPath, right.LocalPath, StringComparison.Ordinal)
                && string.Equals(left.StreamingUrl, right.StreamingUrl, StringComparison.Ordinal)
                && string.Equals(left.RemoteUrl, right.RemoteUrl, StringComparison.Ordinal)
                && left.Dependencies.SequenceEqual(right.Dependencies, StringComparer.Ordinal);
        }

        private static bool SameAsset(ESRuntimeAssetRecord left, ESRuntimeAssetRecord right)
        {
            return string.Equals(left.AssetBundleKey, right.AssetBundleKey, StringComparison.Ordinal)
                && string.Equals(left.InternalName, right.InternalName, StringComparison.Ordinal)
                && string.Equals(left.TypeName, right.TypeName, StringComparison.Ordinal);
        }

        private static bool SameSubAsset(ESRuntimeSubAssetRecord left, ESRuntimeSubAssetRecord right)
        {
            return string.Equals(left.AssetBundleKey, right.AssetBundleKey, StringComparison.Ordinal)
                && string.Equals(left.InternalName, right.InternalName, StringComparison.Ordinal)
                && string.Equals(left.Selector, right.Selector, StringComparison.Ordinal)
                && string.Equals(left.TypeName, right.TypeName, StringComparison.Ordinal);
        }

        private void ValidateReleaseRecordsOrThrow()
        {
            var bundleKeys = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < assetBundles.Length; i++)
            {
                ESRuntimeAssetBundleRecord record = assetBundles[i];
                if (record == null || string.IsNullOrWhiteSpace(record.AssetBundleKey) || !bundleKeys.Add(record.AssetBundleKey))
                    throw new InvalidOperationException("[ESRes][RuntimeMap] 运行时文件索引包含无效或重复的 BundleKey。");
                if (string.IsNullOrEmpty(record.LocalPath) && string.IsNullOrEmpty(record.StreamingUrl) && string.IsNullOrEmpty(record.RemoteUrl))
                    throw new InvalidOperationException("[ESRes][RuntimeMap] 运行时文件索引缺少物理定位：BundleKey=" + record.AssetBundleKey);
            }

            foreach (ESRuntimeAssetBundleRecord record in assetBundles)
                foreach (string dependency in record.Dependencies)
                    if (string.IsNullOrWhiteSpace(dependency) || !bundleKeys.Contains(dependency))
                        throw new InvalidOperationException("[ESRes][RuntimeMap] 运行时文件索引依赖缺失：BundleKey=" + record.AssetBundleKey + ", Dependency=" + dependency);

            var mainGuids = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < assets.Length; i++)
            {
                ESRuntimeAssetRecord record = assets[i];
                if (record == null || string.IsNullOrWhiteSpace(record.Guid) || string.IsNullOrWhiteSpace(record.AssetBundleKey)
                    || string.IsNullOrWhiteSpace(record.InternalName) || !bundleKeys.Contains(record.AssetBundleKey) || !mainGuids.Add(record.Guid))
                    throw new InvalidOperationException("[ESRes][RuntimeMap] 运行时主资源索引包含无效或重复身份：Index=" + i + ", GUID=" + (record?.Guid ?? "<null>") + ", BundleKey=" + (record?.AssetBundleKey ?? "<null>"));
            }

            var subIds = new HashSet<ESSubAssetId>();
            for (var i = 0; i < subAssets.Length; i++)
            {
                ESRuntimeSubAssetRecord record = subAssets[i];
                ESSubAssetId id = record == null ? default : record.Id;
                if (record == null || !id.IsValid || string.IsNullOrWhiteSpace(record.AssetBundleKey)
                    || string.IsNullOrWhiteSpace(record.InternalName) || string.IsNullOrWhiteSpace(record.Selector) || string.IsNullOrWhiteSpace(record.TypeName)
                    || !bundleKeys.Contains(record.AssetBundleKey) || !subIds.Add(id))
                    throw new InvalidOperationException("[ESRes][SubAsset] 运行时子资产索引包含无效或重复身份：Index=" + i + ", GUID=" + id.Guid + ", LocalFileId=" + id.LocalFileId + ", BundleKey=" + (record?.AssetBundleKey ?? "<null>") + ", InternalName=" + (record?.InternalName ?? "<null>") + ", Selector=" + (record?.Selector ?? "<null>") + ", Type=" + (record?.TypeName ?? "<null>"));
            }
        }

        public bool TryGetSubAsset(ESSubAssetId id, out ESRuntimeSubAssetRecord record)
        {
            EnsureIndex();
            if (id.IsValid)
                return subAssetsById.TryGetValue(id, out record);

            record = null;
            return false;
        }

        public bool TryGetAssetBundle(string assetBundleKey, out ESRuntimeAssetBundleRecord record)
        {
            EnsureIndex();
            return assetBundlesByKey.TryGetValue(assetBundleKey, out record);
        }

        public void RebuildRuntimeIndex()
        {
            mainAssetsByGuid = new Dictionary<string, ESRuntimeAssetRecord>(assets.Length, StringComparer.Ordinal);
            subAssetsById = new Dictionary<ESSubAssetId, ESRuntimeSubAssetRecord>(subAssets.Length);
            assetBundlesByKey = new Dictionary<string, ESRuntimeAssetBundleRecord>(assetBundles.Length, StringComparer.Ordinal);

            AddAssetBundles();
            AddAssets();
            AddSubAssets();
        }

        private void EnsureIndex()
        {
            if (mainAssetsByGuid == null) RebuildRuntimeIndex();
        }

        private void AddAssetBundles()
        {
            for (var i = 0; i < assetBundles.Length; i++)
            {
                var record = assetBundles[i];
                if (record == null || string.IsNullOrEmpty(record.AssetBundleKey) || assetBundlesByKey.ContainsKey(record.AssetBundleKey))
                    Debug.LogError("[ESRes][RuntimeMap] Duplicate or invalid AssetBundle record.", this);
                else assetBundlesByKey.Add(record.AssetBundleKey, record);
            }
        }

        private void AddAssets()
        {
            for (var i = 0; i < assets.Length; i++)
            {
                var record = assets[i];
                if (record == null || string.IsNullOrEmpty(record.Guid) || mainAssetsByGuid.ContainsKey(record.Guid))
                    Debug.LogError("[ESRes][RuntimeMap] Duplicate or invalid main asset GUID.", this);
                else mainAssetsByGuid.Add(record.Guid, record);
            }
        }

        private void AddSubAssets()
        {
            for (var i = 0; i < subAssets.Length; i++)
            {
                var record = subAssets[i];
                var id = record == null ? default : new ESSubAssetId(record.Guid, record.LocalFileId);
                if (record == null || string.IsNullOrEmpty(record.Guid) || record.LocalFileId == 0 || string.IsNullOrEmpty(record.AssetBundleKey) || string.IsNullOrEmpty(record.InternalName) || string.IsNullOrEmpty(record.Selector) || string.IsNullOrEmpty(record.TypeName) || subAssetsById.ContainsKey(id))
                    Debug.LogError("[ESRes][SubAsset] Duplicate or invalid sub-asset identity: " + id + ".", this);
                else subAssetsById.Add(id, record);
            }
        }
    }

    [Serializable]
    public readonly struct ESSubAssetId : IEquatable<ESSubAssetId>
    {
        public readonly string Guid;
        public readonly long LocalFileId;

        public ESSubAssetId(string guid, long localFileId)
        {
            Guid = guid ?? string.Empty;
            LocalFileId = localFileId;
        }

        public bool IsValid => !string.IsNullOrEmpty(Guid) && LocalFileId != 0;
        public bool Equals(ESSubAssetId other) => LocalFileId == other.LocalFileId && string.Equals(Guid, other.Guid, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is ESSubAssetId other && Equals(other);
        public override int GetHashCode() => (StringComparer.Ordinal.GetHashCode(Guid ?? string.Empty) * 397) ^ LocalFileId.GetHashCode();
        public override string ToString() => Guid + ":" + LocalFileId;
    }

    [Serializable]
    public sealed class ESRuntimeAssetBundleRecord
    {
        [FormerlySerializedAs("packageKey")]
        [SerializeField] private string assetBundleKey;
        [SerializeField] private string localPath;
        [SerializeField] private string streamingUrl;
        [SerializeField] private string remoteUrl;
        [SerializeField] private string contentHash;
        [SerializeField] private uint crc;
        [SerializeField] private long size;
        [SerializeField] private string[] dependencies = Array.Empty<string>();

        public string AssetBundleKey => assetBundleKey;
        public string LocalPath => localPath;
        public string StreamingUrl => streamingUrl;
        public string RemoteUrl => remoteUrl;
        public string ContentHash => contentHash;
        public uint Crc => crc;
        public long Size => size;
        public IReadOnlyList<string> Dependencies => dependencies;

        public ESRuntimeAssetBundleRecord(string key, string path, string localStreamingUrl, string remoteUrlValue, string hash, uint checksum, long fileSize, string[] assetBundleDependencies)
        {
            assetBundleKey = key;
            localPath = path;
            streamingUrl = localStreamingUrl;
            remoteUrl = remoteUrlValue;
            contentHash = hash;
            crc = checksum;
            size = fileSize;
            dependencies = assetBundleDependencies ?? Array.Empty<string>();
        }

    }

    [Serializable]
    public sealed class ESRuntimeAssetRecord
    {
        [SerializeField] private string guid;
        [FormerlySerializedAs("packageKey")]
        [SerializeField] private string assetBundleKey;
        [SerializeField] private string internalName;
        [SerializeField] private string typeName;

        public string Guid => guid;
        public string AssetBundleKey => assetBundleKey;
        public string InternalName => internalName;
        public string TypeName => typeName;

        public ESRuntimeAssetRecord(string assetGuid, string assetBundle, string name, string assetTypeName)
        {
            guid = assetGuid;
            assetBundleKey = assetBundle;
            internalName = name;
            typeName = assetTypeName;
        }

    }

    [Serializable]
    public sealed class ESRuntimeSubAssetRecord
    {
        [SerializeField] private string guid;
        [SerializeField] private long localFileId;
        [SerializeField] private string assetBundleKey;
        [SerializeField] private string internalName;
        [FormerlySerializedAs("subAssetName")]
        [SerializeField] private string selector;
        [SerializeField] private string typeName;

        public string Guid => guid;
        public long LocalFileId => localFileId;
        public string AssetBundleKey => assetBundleKey;
        public ESSubAssetId Id => new ESSubAssetId(guid, localFileId);
        public string InternalName => internalName;
        public string Selector => selector;
        public string TypeName => typeName;

        public ESRuntimeSubAssetRecord(string assetGuid, long fileId, string bundleKey, string name, string assetSelector, string assetTypeName)
        {
            guid = assetGuid;
            localFileId = fileId;
            assetBundleKey = bundleKey;
            internalName = name;
            selector = assetSelector;
            typeName = assetTypeName;
        }

    }
}
