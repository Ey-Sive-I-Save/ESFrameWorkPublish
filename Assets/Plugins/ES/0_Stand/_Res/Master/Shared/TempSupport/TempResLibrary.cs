using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace ES
{
    /// <summary>
    /// 临时资源库数据结构,构建时和加载时 使用。
    /// </summary>
    
    [Serializable]
    public class TempLibrary
    {
        public string LibNameDisPlay = " ";
        public string LibFolderName = "DefaultLibFolderName";
        public bool ContainsBuild = true;
        public bool IsNet = true;

       
        public ESResJsonData_AssetsKeys ESResData_AssetKeys = new ESResJsonData_AssetsKeys();
        public ESResJsonData_ABMetadata ESResData_ABMetadata = new ESResJsonData_ABMetadata();
    }
}
