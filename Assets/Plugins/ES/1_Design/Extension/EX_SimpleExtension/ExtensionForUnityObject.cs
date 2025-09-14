using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{

    public static class ExtensionForUnityObject
    {
        public static T _IsNotNullAndUse<T>(this T ob) where T : UnityEngine.Object
        {
            if (ob == null) return null;
            return ob;
            /*可以配合?.使用
              someObject.NotNull()?.XXXX();
              确保Miss判定
             */
        }
    }
}

