using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{

    public static partial class ESDesignUtility
    {
        //遍历/递归器
        public static class Foreach
        {
            public static Transform ForeachFindTransform(Transform pa, string name)
            {
                if (pa == null || pa.childCount == 0) return default;
                Transform find = pa.Find(name);
                if (find != null) return find;
                int all = pa.childCount;
                for (int i = 0; i < all; i++)
                {
                    find = ForeachFindTransform(pa.GetChild(i), name);
                    if (find != null)
                    {
                        return find;
                    }
                }
                return default;
            }
        }
    }
}

