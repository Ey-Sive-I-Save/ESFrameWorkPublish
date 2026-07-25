using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
namespace ES
{
    /// <summary>
    /// 专属于AB包元数据的 JSON 数据结构,用于存储和加载AB包的哈希、键和依赖信息。
    /// </summary>
    [Serializable]
    public class ESResJsonData_ABMetadata
    {
        public int SchemaVersion = 2;
        public string ReleaseId;
        public string LibraryId;
        // 从Hashes类
        public Dictionary<string, string> PreToHashes = new Dictionary<string, string>();

        // 从ABKeys类
        public List<ESResKey> ABKeys = new List<ESResKey>();

        // 从Dependences类
        public Dictionary<string, string[]> Dependences = new Dictionary<string, string[]>();

        public Dictionary<string, ESResBundleRecord> BundleRecords = new Dictionary<string, ESResBundleRecord>();

    }

    [Serializable]
    public class ESResBundleRecord
    {
        public string BundleId;
        public string PreName;
        public string FileName;
        public string Sha256;
        public long SizeBytes;
        public string[] Dependencies;
    }
}
