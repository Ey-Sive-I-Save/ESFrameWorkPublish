using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// ESRes 总工具（加载与卸载入口）
    /// 
    /// 这一部分只负责：
    /// - 持有全局的 MainLoader（真正干活的是 ESResLoader）
    /// - 提供一些针对 AssetBundle / 资源卸载的静态帮助方法
    /// 不直接参与任何查表或下载逻辑，保证职责单一、易于维护。
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
            // 注意：这里只对“磁盘类资源”调用 Resources.UnloadAsset，
            // GameObject / 场景实例仍然交给 Unity 的销毁流程，避免错误卸载导致引用失效。
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

