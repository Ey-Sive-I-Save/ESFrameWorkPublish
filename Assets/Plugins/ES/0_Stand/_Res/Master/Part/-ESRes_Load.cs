using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// ESRes 总工具
    /// </summary>
    public partial class ESResMaster
    {
        #region 加载部分

        public static ESResLoader MainLoader = new ESResLoader();

        public static AssetBundle HasLoadedAB(string name)
        {
            foreach (var i in AssetBundle.GetAllLoadedAssetBundles())
            {
                if (i.name == name) return i;
            }
            return null;
        }


        #endregion

        #region 卸载部分
        public static void UnloadRes(UnityEngine.Object asset)
        {
            if (asset is GameObject)
            {
            }
            else
            {
                //磁盘类资源，对游戏对象无效
                Resources.UnloadAsset(asset);
            }
        }


        #endregion


    }

    public enum ResSourceState
    {
        [InspectorName("等待中(未使用)")] Waiting = 0,
        [InspectorName("加载中")] Loading = 1,
        [InspectorName("完毕")] Ready = 2,
    }

}

