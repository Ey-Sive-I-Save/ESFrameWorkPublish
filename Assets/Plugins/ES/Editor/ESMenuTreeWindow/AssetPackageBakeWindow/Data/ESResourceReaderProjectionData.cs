using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace ES
{
    [Serializable]
    public sealed class ESResourceReaderProjectionEntry
    {
        [LabelText("稳定标识"), ReadOnly] public string stableId;
        [LabelText("相对路径"), ReadOnly] public string relativePath;
        [LabelText("类型"), ReadOnly] public string entryType;
        [LabelText("GUID"), ReadOnly] public string guid;
        [LabelText("fileID"), ReadOnly] public string fileId;
        [LabelText("依赖 GUID"), ReadOnly] public List<string> dependencyGuids = new List<string>();
    }

    [CreateAssetMenu(menuName = "ES/资源读取/Projection 数据", fileName = "ESResourceReaderProjectionData")]
    public sealed class ESResourceReaderProjectionData : ESSO
    {
        public const int CurrentSchemaVersion = 1;

        [LabelText("Schema 版本"), ReadOnly] public int schemaVersion = CurrentSchemaVersion;
        [LabelText("源路径"), ReadOnly] public string sourcePath;
        [LabelText("源 SHA-256"), ReadOnly] public string sourceSha256;
        [LabelText("Parser ID"), ReadOnly] public string parserId;
        [LabelText("Parser 版本"), ReadOnly] public string parserVersion;
        [LabelText("检测格式"), ReadOnly] public string detectedFormat;
        [LabelText("条目数量"), ReadOnly] public int entryCount;
        [LabelText("对象数量"), ReadOnly] public int objectCount;
        [LabelText("外部引用数量"), ReadOnly] public int dependencyCount;
        [LabelText("投影 Hash"), ReadOnly] public string projectionHash;
        [LabelText("解析条目"), ListDrawerSettings(ShowIndexLabels = true, NumberOfItemsPerPage = 20)]
        public List<ESResourceReaderProjectionEntry> entries = new List<ESResourceReaderProjectionEntry>();
        [LabelText("警告"), ReadOnly] public List<string> warnings = new List<string>();
        [LabelText("错误"), ReadOnly] public List<string> errors = new List<string>();
        [LabelText("未声明能力"), ReadOnly] public List<string> nonClaims = new List<string>();
        [LabelText("原始 Projection JSON"), MultiLineProperty(3), ReadOnly] public string rawProjectionJson;
        [LabelText("摘要 JSON"), MultiLineProperty(3), ReadOnly] public string summaryJson;

        public bool IsValid()
        {
            return schemaVersion == CurrentSchemaVersion &&
                   !string.IsNullOrWhiteSpace(sourceSha256) &&
                   sourceSha256.Length == 64 &&
                   !string.IsNullOrWhiteSpace(parserId) &&
                   string.Equals(projectionHash, ComputeProjectionHash(), StringComparison.OrdinalIgnoreCase);
        }

        public void Seal()
        {
            schemaVersion = CurrentSchemaVersion;
            projectionHash = ComputeProjectionHash();
        }

        public string ComputeProjectionHash()
        {
            var builder = new StringBuilder();
            builder.Append(schemaVersion).Append('|').Append(sourcePath).Append('|').Append(sourceSha256).Append('|');
            builder.Append(parserId).Append('|').Append(parserVersion).Append('|').Append(detectedFormat).Append('|');
            foreach (var entry in entries ?? new List<ESResourceReaderProjectionEntry>())
            {
                if (entry == null) continue;
                builder.Append(entry.stableId).Append('|').Append(entry.relativePath).Append('|').Append(entry.entryType).Append('|').Append(entry.guid).Append('|').Append(entry.fileId).Append(';');
                foreach (var guid in entry.dependencyGuids ?? new List<string>()) builder.Append(guid).Append(',');
                builder.Append('|');
            }
            using (var sha = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(builder.ToString());
                return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty).ToLowerInvariant();
            }
        }
    }
}
