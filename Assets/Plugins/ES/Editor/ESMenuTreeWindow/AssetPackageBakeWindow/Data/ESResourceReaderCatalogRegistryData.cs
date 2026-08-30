using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace ES
{
    [Serializable]
    public sealed class ESResourceReaderCatalogSource
    {
        [LabelText("来源 ID"), Required] public string sourceId;
        [LabelText("索引路径"), Required] public string sourceIndexPath;
        [LabelText("来源类型")] public string sourceKind = "project";
        [LabelText("启用")] public bool enabled = true;
        [LabelText("期望 SHA-256"), ReadOnly] public string expectedSha256;
        [LabelText("备注"), MultiLineProperty(2)] public string notes;
    }

    [CreateAssetMenu(menuName = "ES/资源读取/Catalog 来源注册", fileName = "ESResourceReaderCatalogRegistryData")]
    public sealed class ESResourceReaderCatalogRegistryData : ESSO
    {
        public const int CurrentSchemaVersion = 1;

        [LabelText("Schema 版本"), ReadOnly] public int schemaVersion = CurrentSchemaVersion;
        [LabelText("注册表 ID"), ReadOnly] public string registryId = "es-resource-reader.catalog-registry.v1";
        [LabelText("来源列表"), ListDrawerSettings(ShowIndexLabels = true, NumberOfItemsPerPage = 16)]
        public List<ESResourceReaderCatalogSource> sources = new List<ESResourceReaderCatalogSource>();
        [LabelText("注册表 Hash"), ReadOnly] public string registryHash;

        public bool IsValid()
        {
            if (schemaVersion != CurrentSchemaVersion || string.IsNullOrWhiteSpace(registryId)) return false;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var source in sources ?? new List<ESResourceReaderCatalogSource>())
            {
                if (source == null || string.IsNullOrWhiteSpace(source.sourceId) || string.IsNullOrWhiteSpace(source.sourceIndexPath) || !seen.Add(source.sourceId)) return false;
            }
            return string.Equals(registryHash, ComputeRegistryHash(), StringComparison.OrdinalIgnoreCase);
        }

        public void Seal()
        {
            schemaVersion = CurrentSchemaVersion;
            registryHash = ComputeRegistryHash();
        }

        public string ComputeRegistryHash()
        {
            var builder = new StringBuilder().Append(schemaVersion).Append('|').Append(registryId).Append('|');
            foreach (var source in sources ?? new List<ESResourceReaderCatalogSource>())
            {
                if (source == null) continue;
                builder.Append(source.sourceId).Append('|').Append(source.sourceIndexPath).Append('|').Append(source.sourceKind).Append('|').Append(source.enabled).Append('|').Append(source.expectedSha256).Append(';');
            }
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString()))).Replace("-", string.Empty).ToLowerInvariant();
        }
    }
}
