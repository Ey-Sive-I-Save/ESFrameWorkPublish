
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UIElements;

namespace ES
{
    public static partial class ExtensionNormal
    {   
        /// <summary>
        /// 产生一个只有自己的列表
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static List<T> _AsListOnlySelf<T>(this T obj)
        {
            return new List<T> { obj };
        }
        /// <summary>
        /// 产生一个只有自己的数组
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T[] _AsArrayOnlySelf<T>(this T obj)
        {
            return new T[] { obj };
        }
        /// <summary>
        /// 交换两个变量值
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="a"></param>
        /// <param name="b"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void _Swap<T>(ref this T a, ref T b) where T : struct
        {
            (a, b) = (b, a);
        }
    }
}
