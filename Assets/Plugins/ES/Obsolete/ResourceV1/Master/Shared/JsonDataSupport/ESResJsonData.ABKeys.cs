using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
namespace ES
{

    /// <summary>
    /// 专属于AB包键的 JSON 数据结构,用于存储和加载AB包键信息。
    /// </summary>
    [Serializable]
    public class ESResJsonData_ABKeys
    {
        public List<ESResKey> ABKeys = new List<ESResKey>();
        public Dictionary<string, int> NameToABKeys = new Dictionary<string, int>();
    }

}
