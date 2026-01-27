using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace ES
{
    /// <summary>
    /// 构建时资源库数据结构，用于编辑器构建过程。
    /// </summary>
    
    [Serializable]
    public class ESBuildTempResLibrary
    {
        public string LibNameDisPlay = " ";
        public string LibFolderName = "DefaultLibFolderName";
        public bool ContainsBuild = true;
        public bool IsNet = true;

       
        public ESResJsonData_AssetsKeys ESResData_AssetKeys = new ESResJsonData_AssetsKeys();
        public ESResJsonData_ABMetadata ESResData_ABMetadata = new ESResJsonData_ABMetadata();
    }
}
