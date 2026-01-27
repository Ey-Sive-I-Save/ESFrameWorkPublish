using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.Serialization;

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
        [NonSerialized]
        public static Dictionary<string, DownloadedLibraryData> DownloadedLibraries = new Dictionary<string, DownloadedLibraryData>();

        [NonSerialized]
        public static Dictionary<string, DownloadedConsumerData> DownloadedConsumers = new Dictionary<string, DownloadedConsumerData>();

        #region 全局资源缓存
        /// <summary>
        /// 全局AssetKeys缓存：支持通过GUID或资源路径查询Asset对应的ESResKey
        /// 用于快速定位资源在哪个AB包中
        /// </summary>
        [NonSerialized]
        public static TwoKeyDictionary<ESResKey> GlobalAssetKeys = new TwoKeyDictionary<ESResKey>();

        /// <summary>
        /// 全局ABKeys缓存：AB包名 -> ESResKey
        /// 用于快速查找AB包的完整信息
        /// </summary>
        [NonSerialized]
        public static Dictionary<string, ESResKey> GlobalABKeys = new Dictionary<string, ESResKey>();

        /// <summary>
        /// 全局资源索引表：以统一方式维护资源源与引用计数
        /// </summary>
        [NonSerialized]
        public static ESResTable ResTable = new ESResTable();

        /// <summary>
        /// 全局AB包预处理名到哈希的映射：PreName -> HashName
        /// 用于将AB包的预处理名转换为带哈希的实际文件名
        /// </summary>
        [NonSerialized]
        public static Dictionary<string, string> GlobalABPreToHashes = new Dictionary<string, string>();

        /// <summary>
        /// 全局AB包哈希到预处理名的映射：HashName -> PreName
        /// 用于将带哈希的AB包文件名反查原始预处理名
        /// </summary>
        [NonSerialized]
        public static Dictionary<string, string> GlobalABHashToPres = new Dictionary<string, string>();

        /// <summary>
        /// 全局AB包依赖关系：AB包名 -> 依赖的AB包名数组
        /// 用于加载AB包时自动加载其依赖项
        /// </summary>
        [NonSerialized]
        public static Dictionary<string, string[]> GlobalDependences = new Dictionary<string, string[]>();
        #endregion

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

    /// <summary>
    /// 已下载库的数据结构，持久存储整个生命周期，支持rebuild。
    /// </summary>
    [Serializable]
    public class DownloadedLibraryData
    {
        public string LibraryName; // 库显示名
        public string LibFolderName; // 库文件夹名
        public string LocalPath; // 预计算本地路径
        public string RemotePath; // 预计算远程路径
        public bool IsRemote; // 是否远程
        public string Version; // 版本号
        public string Description; // 描述
        public long TotalSize; // 库总大小（字节）
        public int ChangeCount; // 用于rebuild检查
    }

    /// <summary>
    /// 已下载消费者（扩展包）的数据结构，引用多个库。
    /// </summary>
    [Serializable]
    public class DownloadedConsumerData
    {
        public string ConsumerName; // 消费者显示名
        public string Version; // 版本号
        public string Description; // 描述
        public List<string> ReferencedLibFolderNames; // 引用的库文件夹名列表
    }


}

