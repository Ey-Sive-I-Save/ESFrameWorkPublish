using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ES {
    /// <summary>
    /// ESResTable
    /// 
    /// 资源句柄索引表：
    /// - 通过 int 索引缓存 ESResSource，避免到处用字符串查找；
    /// - AssetsSources：按资源索引管理具体资源；
    /// - ABSources：按 AB 索引管理 AssetBundle 级别的资源；
    /// 
    /// 自身不负责加载 / 卸载，只作为“运行时快速索引层”。
    /// </summary>
    public class ESResTable 
    {
        public Dictionary<int, ESResSource> AssetsSources = new Dictionary<int, ESResSource>();
        public Dictionary<int, ESResSource> ABSources = new Dictionary<int, ESResSource>();

        public ESResSource GetAssetResByIndex(int index)
        {
            if (index < 0) { return null; }
            if (AssetsSources.TryGetValue(index, out var res)) {
                return res;
            }
            return null;
        }

        public ESResSource GetABResByIndex(int index)
        {
            if (index < 0) { return null; }
            if (ABSources.TryGetValue(index, out var res))
            {
                return res;
            }
            return null;
        }

    }
}
