using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace ES
{
    [Serializable]
    public sealed class ESResourceReaderCatalogDiffEntry
    {
        [LabelText("GUID")] public string guid;
        [LabelText("来源 ID")] public string sourceId;
        [LabelText("资源路径")] public string sourcePath;
        [LabelText("对象 ID")] public string objectStableId;
        [LabelText("旧 Hash")] public string beforeSha256;
        [LabelText("新 Hash")] public string afterSha256;
    }

    [CreateAssetMenu(menuName = "ES/资源读取/Catalog Diff", fileName = "ESResourceReaderCatalogDiffData")]
    public sealed class ESResourceReaderCatalogDiffData : ESSO
    {
        public const int CurrentSchemaVersion = 1;
        [LabelText("Schema 版本"), ReadOnly] public int schemaVersion = CurrentSchemaVersion;
        [LabelText("Diff ID"), ReadOnly] public string diffId = "es-resource-reader.reference-catalog-diff.v1";
        [LabelText("基线 Hash"), ReadOnly] public string baselineSha256;
        [LabelText("当前 Hash"), ReadOnly] public string currentSha256;
        [LabelText("新增数量"), ReadOnly] public int addedCount;
        [LabelText("移除数量"), ReadOnly] public int removedCount;
        [LabelText("变更数量"), ReadOnly] public int changedCount;
        [LabelText("新增条目"), ListDrawerSettings(NumberOfItemsPerPage = 16)] public List<ESResourceReaderCatalogDiffEntry> added = new List<ESResourceReaderCatalogDiffEntry>();
        [LabelText("移除条目"), ListDrawerSettings(NumberOfItemsPerPage = 16)] public List<ESResourceReaderCatalogDiffEntry> removed = new List<ESResourceReaderCatalogDiffEntry>();
        [LabelText("变更条目"), ListDrawerSettings(NumberOfItemsPerPage = 16)] public List<ESResourceReaderCatalogDiffEntry> changed = new List<ESResourceReaderCatalogDiffEntry>();
        [LabelText("对象 Hash"), ReadOnly] public string objectHash;

        public bool IsValid()
        {
            return schemaVersion == CurrentSchemaVersion
                && diffId == "es-resource-reader.reference-catalog-diff.v1"
                && addedCount >= added.Count && removedCount >= removed.Count && changedCount >= changed.Count
                && objectHash == ComputeObjectHash();
        }

        public void Seal() { schemaVersion = CurrentSchemaVersion; objectHash = ComputeObjectHash(); }

        public string ComputeObjectHash()
        {
            var text = new StringBuilder().Append(schemaVersion).Append('|').Append(diffId).Append('|').Append(baselineSha256).Append('|').Append(currentSha256).Append('|').Append(addedCount).Append('|').Append(removedCount).Append('|').Append(changedCount);
            Append(text, added); Append(text, removed); Append(text, changed);
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()))).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static void Append(StringBuilder text, List<ESResourceReaderCatalogDiffEntry> entries)
        {
            foreach (var e in entries ?? new List<ESResourceReaderCatalogDiffEntry>())
                if (e != null) text.Append('|').Append(e.guid).Append('|').Append(e.sourceId).Append('|').Append(e.sourcePath).Append('|').Append(e.objectStableId).Append('|').Append(e.beforeSha256).Append('|').Append(e.afterSha256);
        }
    }
}
