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
        // 从Hashes类
        public Dictionary<string, string> PreToHashes = new Dictionary<string, string>();

        // 从ABKeys类
        public List<ESResKey> ABKeys = new List<ESResKey>();

        // 从Dependences类
        public Dictionary<string, string[]> Dependences = new Dictionary<string, string[]>();
    }
}