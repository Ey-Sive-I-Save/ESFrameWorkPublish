using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{
    //扩展模板
    public static class ExtensionModel 
    {
        /// <summary>
        /// 使用“ _ ” 开头有助于分辨
        /// </summary>
        /// <param name="useOn"></param>
        /// <returns></returns>
        public static object _MethodName(this object useOn)
        {
            //DO

            return useOn;
        }
    }
}

