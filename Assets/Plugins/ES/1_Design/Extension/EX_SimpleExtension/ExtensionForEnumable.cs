using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{

    public static class ExtensionForEnumable
    {

        #region 快捷功能
        /// <summary>
        /// 随机元素
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="array"></param>
        /// <param name="ifNullOrEmpty">如果为空</param>
        /// <returns></returns>
        public static T _RandomItem<T>(this T[] array, T ifNullOrEmpty = default)
        {
            if (array == null || array.Length == 0) return ifNullOrEmpty;
            return array[UnityEngine.Random.Range(0, array.Length)];
        }
        /// <summary>
        /// 随机元素
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list"></param>
        /// <param name="ifNullOrEmpty">如果为空</param>
        /// <returns></returns>
        public static T _RandomItem<T>(this List<T> list, T ifNullOrEmpty = default)
        {
            if (list == null || list.Count == 0) return ifNullOrEmpty;
            return list[UnityEngine.Random.Range(0, list.Count)];
        }
        /// <summary>
        /// 打乱列表顺序（原地修改）
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list"></param>
        public static void _RandomShuffle<T>(this List<T> list)
        {
            // 边界条件检查：数组为空或只有一个元素时无需操作
            if (list == null || list.Count <= 1)
            {
                return;
            }
            for (int i = 0; i < list.Count; i++)
            {
                int randomIndex = UnityEngine.Random.Range(i, list.Count);
                (list[i], list[randomIndex]) = (list[randomIndex], list[i]);
            }
        }
        /// <summary>
        /// 打乱数组顺序（原地修改）
        /// </summary>
        /// <typeparam name="T">数组元素类型</typeparam>
        /// <param name="array">要打乱的数组</param>
        public static void _RandomShuffle<T>(this T[] array)
        {
            // 边界条件检查：数组为空或只有一个元素时无需操作
            if (array == null || array.Length <= 1)
            {
                return;
            }

            // Fisher-Yates 洗牌算法：从前向后迭代
            for (int i = 0; i < array.Length; i++)
            {
                // 生成一个 [i, array.Length) 范围内的随机索引
                // UnityEngine.Random.Range 对于整数是 [minInclusive, maxExclusive)
                int randomIndex = UnityEngine.Random.Range(i, array.Length);

                // 使用元组交换元素：简洁高效
                (array[i], array[randomIndex]) = (array[randomIndex], array[i]);
            }
        }
        /// <summary>
        /// 是空或者无元素的数组？
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="array"></param>
        /// <returns></returns>
        public static bool _IsNullOrEmpty<T>(this T[] array)
        {
            return array == null || array.Length == 0;
        }
        /// <summary>
        /// 是空或者无元素的列表？
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list"></param>
        /// <returns></returns>
        public static bool _IsNullOrEmpty<T>(this List<T> list)
        {
            return list == null || list.Count == 0;
        }

        #endregion


      
    }
}

