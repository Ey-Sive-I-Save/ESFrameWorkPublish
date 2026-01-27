
using Sirenix.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
namespace ES
{
    public partial class ESResMaster
    {
        public static string[] ABNames;


    
     


    }

        /// <summary>
    /// 查询特定资源的最终标识
    /// </summary>
    [Serializable]
    public class ESResKey : IPoolableAuto
    {

        public ESResSourceLoadType SourceLoadType = ESResSourceLoadType.ABAsset;
        public string LibName;
        public string LibFolderName;
        public string ABName;
        public string ResName;
        public string GUID;
        public string Path;
        public Type TargetType;

        public bool IsRecycled { get; set; }

        public void OnResetAsPoolable()
        {

        }

        public override string ToString()
        {
            return string.Format("资源查询键, 库名:{0} AB包名:{1} 类型:{2},资源名{3}", LibName, ABName,
                ResName, TargetType);
        }

        public void TryAutoPushedToPool()
        {
            ESResMaster.Instance.PoolForESResKey.PushToPool(this);
        }
    }
   
  

}
