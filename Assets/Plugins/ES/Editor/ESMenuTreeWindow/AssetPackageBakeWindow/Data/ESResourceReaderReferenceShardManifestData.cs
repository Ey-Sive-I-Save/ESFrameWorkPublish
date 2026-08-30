using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace ES
{
    [Serializable] public sealed class ESResourceReaderReferenceShardInfo
    {
        [LabelText("前缀"), ReadOnly] public string prefix;
        [LabelText("分片路径"), ReadOnly] public string path;
        [LabelText("GUID 数"), ReadOnly] public int guidCount;
        [LabelText("引用边"), ReadOnly] public int edgeCount;
        [LabelText("SHA-256"), ReadOnly] public string sha256;
    }

    [CreateAssetMenu(menuName = "ES/资源读取/Reference 分片 Manifest", fileName = "ESResourceReaderReferenceShardManifestData")]
    public sealed class ESResourceReaderReferenceShardManifestData : ESSO
    {
        public const int CurrentSchemaVersion = 1;
        [LabelText("Schema 版本"), ReadOnly] public int schemaVersion = CurrentSchemaVersion;
        [LabelText("Manifest ID"), ReadOnly] public string manifestId = "es-resource-reader.reference-catalog-shards.v1";
        [LabelText("Catalog Hash"), ReadOnly] public string catalogSha256;
        [LabelText("前缀长度"), ReadOnly] public int prefixLength = 1;
        [LabelText("分片列表"), ListDrawerSettings(NumberOfItemsPerPage = 16)] public List<ESResourceReaderReferenceShardInfo> shards = new List<ESResourceReaderReferenceShardInfo>();
        [LabelText("对象 Hash"), ReadOnly] public string objectHash;
        public bool IsValid() => schemaVersion == CurrentSchemaVersion && manifestId == "es-resource-reader.reference-catalog-shards.v1" && objectHash == ComputeObjectHash();
        public void Seal() { schemaVersion = CurrentSchemaVersion; objectHash = ComputeObjectHash(); }
        public string ComputeObjectHash()
        {
            var b = new StringBuilder().Append(schemaVersion).Append('|').Append(manifestId).Append('|').Append(catalogSha256).Append('|').Append(prefixLength);
            foreach (var s in shards ?? new List<ESResourceReaderReferenceShardInfo>()) if (s != null) b.Append('|').Append(s.prefix).Append('|').Append(s.path).Append('|').Append(s.guidCount).Append('|').Append(s.edgeCount).Append('|').Append(s.sha256);
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(b.ToString()))).Replace("-", string.Empty).ToLowerInvariant();
        }
    }
}
