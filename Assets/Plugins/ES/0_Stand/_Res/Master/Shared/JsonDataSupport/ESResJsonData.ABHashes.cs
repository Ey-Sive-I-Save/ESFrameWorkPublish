using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
namespace ES
{
  /// <summary>
    /// 专属于哈希值的 JSON 数据结构,用于存储和加载资源哈希信息。
    /// </summary>
    [Serializable]
    public class ESResJsonData_Hashes
    {
        public Dictionary<string, string> PreToHashes = new Dictionary<string, string>();



    }
}
