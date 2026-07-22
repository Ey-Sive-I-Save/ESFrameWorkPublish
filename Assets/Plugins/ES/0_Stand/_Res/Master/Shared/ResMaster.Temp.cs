using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ES
{
    public partial class ESResMaster
    {


#if UNITY_EDITOR
        /// <summary>
        /// 临时资源库数据结构，用于在构建时和加载资源时存储和管理资源信息，用后就销毁。
        /// </summary>
        [NonSerialized]
        public static SafeDictionary<string, ESBuildTempAssetLibrary>
        TempAssetLibraries = new(() => new ESBuildTempAssetLibrary() { });

        [Obsolete("Use TempAssetLibraries.")]
        public static SafeDictionary<string, ESBuildTempAssetLibrary> TempResLibrarys => TempAssetLibraries;
#endif

    }
}

