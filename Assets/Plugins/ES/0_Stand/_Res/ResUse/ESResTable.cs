using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ES {
    
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
