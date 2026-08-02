using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
namespace ES
{
    /// <summary>
    /// 专属于依赖关系的 JSON 数据结构,用于存储和加载资源依赖信息。
    /// </summary>
    [Serializable]
    public class ESResJsonData_Dependences
    {
        public Dictionary<string, string[]> Dependences = new Dictionary<string, string[]>();
    }
}
