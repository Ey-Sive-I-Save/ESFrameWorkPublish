using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

namespace ES
{
    
    public static class ExtensionForUnityObject
    {
        /// <summary>
        /// 一句话完成安全调用方法 ob._TryUse()?.InvokeXXX();
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="ob"></param>
        /// <returns></returns>
        public static T _TryUse<T>(this T ob) where T : UnityEngine.Object
        {
            if (ob == null) return null;
            return ob;
            /*可以配合?.使用
              someObject.NotNull()?.XXXX();
              确保Miss判定
             */
        }

        /// <summary>
        /// 获得GUID(仅资产有效)
        /// </summary>
        /// <param name="ob"></param>
        /// <returns></returns>
        public static string _GetGUID(this UnityEngine.Object ob)
        {
           return ESDesignUtility.SafeEditor.GetAssetGUID(ob);
        }

    }
}

