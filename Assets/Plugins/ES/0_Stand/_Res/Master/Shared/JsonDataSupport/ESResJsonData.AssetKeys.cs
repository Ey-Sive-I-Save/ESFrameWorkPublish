using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
namespace ES
{
    /// <summary>
    /// 专属于资源键的 JSON 数据结构,用于存储和加载资源键信息。
    /// </summary>
    [Serializable]
    public class ESResJsonData_AssetsKeys
    {
        /// <summary>
        /// 资源键列表，包含所有资源的标识信息,使用List让用索引访问更高效。
        /// </summary>
        public List<ESResKey> AssetKeys = new List<ESResKey>();
        /// <summary>
        /// 资源 GUID 到资源键索引的映射，便于通过 GUID 快速查找对应的资源键。
        /// </summary>
        public Dictionary<string, int> GUIDToAssetKeys = new Dictionary<string, int>();
        /// <summary>
        /// 资源路径到资源键索引的映射，便于通过路径快速查找对应的资源键。
        /// </summary>
        public Dictionary<string, int> PathToAssetKeys = new Dictionary<string, int>();

    }

}
