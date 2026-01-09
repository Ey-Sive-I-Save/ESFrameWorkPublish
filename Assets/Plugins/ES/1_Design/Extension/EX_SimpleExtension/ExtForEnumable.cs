using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.CompilerServices;

namespace ES
{

    public static class ExtForEnumable
    {

        #region 快捷功能
        /// <summary>
        /// 随机元素
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="array"></param>
        /// <param name="ifNullOrEmpty">如果为空</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T _RandomItem<T>(this T[] array, T ifNullOrEmpty = default)
        {
            if (array == null || array.Length == 0) return ifNullOrEmpty;
            if(array.Length==1) return array[0];
            return array[UnityEngine.Random.Range(0, array.Length)];
        }
        
        /// <summary>
        /// 使用 <see cref="System.Random"/> 随机选择数组元素（便于测试与基准）。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T _RandomItem<T>(this T[] array, System.Random rng, T ifNullOrEmpty = default)
        {
            if (array == null || array.Length == 0) return ifNullOrEmpty;
            int idx = rng.Next(0, array.Length);
            return array[idx];
        }
        /// <summary>
        /// 随机元素
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list"></param>
        /// <param name="ifNullOrEmpty">如果为空</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T _RandomItem<T>(this List<T> list, T ifNullOrEmpty = default)
        {
            if (list == null || list.Count == 0) return ifNullOrEmpty;
            if (list.Count == 1) return list[0];
            return list[UnityEngine.Random.Range(0, list.Count)];
        }
        
        /// <summary>
        /// 使用 <see cref="System.Random"/> 随机选择列表元素（便于测试与基准）。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T _RandomItem<T>(this List<T> list, System.Random rng, T ifNullOrEmpty = default)
        {
            if (list == null || list.Count == 0) return ifNullOrEmpty;
            int idx = rng.Next(0, list.Count);
            return list[idx];
        }
        /// <summary>
        /// 打乱列表顺序（原地修改）
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void _RandomShuffle<T>(this List<T> list)
        {
            // 边界条件检查：列表为空或只有一个元素时无需操作
            if (list == null || list.Count <= 1)
            {
                return;
            }

            // 经典 Fisher-Yates：从末尾向前交换，减少随机次数并保证均匀性
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1); // inclusive
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
        /// <summary>
        /// 打乱数组顺序（原地修改）
        /// </summary>
        /// <typeparam name="T">数组元素类型</typeparam>
        /// <param name="array">要打乱的数组</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void _RandomShuffle<T>(this T[] array)
        {
            // 边界条件检查：数组为空或只有一个元素时无需操作
            if (array == null || array.Length <= 1)
            {
                return;
            }

            // Fisher-Yates 洗牌算法：从末尾向前迭代
            for (int i = array.Length - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1); // inclusive
                (array[i], array[j]) = (array[j], array[i]);
            }
        }
        /// <summary>
        /// 是空或者无元素的数组？
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="array"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool _IsNullOrEmpty<T>(this List<T> list)
        {
            return list == null || list.Count == 0;
        }

        /// <summary>
        /// 尝试从数组随机取一个元素，若集合为空返回 false。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool _TryRandomItem<T>(this T[] array, out T item)
        {
            if (array == null || array.Length == 0) { item = default; return false; }
            item = array[UnityEngine.Random.Range(0, array.Length)];
            return true;
        }

        /// <summary>
        /// 尝试从列表随机取一个元素，若集合为空返回 false。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool _TryRandomItem<T>(this List<T> list, out T item)
        {
            if (list == null || list.Count == 0) { item = default; return false; }
            item = list[UnityEngine.Random.Range(0, list.Count)];
            return true;
        }

        /// <summary>
        /// 从序列中随机抽取 N 个元素（无放回）并返回新列表。若 N 大于源长度则返回洗牌后的全部元素。
        /// </summary>
        public static List<T> _RandomSample<T>(this IList<T> source, int n)
        {
            if (source == null) return new List<T>();
            int count = source.Count;
            if (n <= 0) return new List<T>();
            if (n >= count)
            {
                var all = new List<T>(source);
                all._RandomShuffle();
                return all;
            }
            // 使用 Fisher-Yates 部分洗牌
            var buffer = new List<T>(source);
            for (int i = 0; i < n; i++)
            {
                int j = UnityEngine.Random.Range(i, buffer.Count);
                T tmp = buffer[i]; buffer[i] = buffer[j]; buffer[j] = tmp;
            }
            return buffer.GetRange(0, n);
        }

        /// <summary>
        /// 从序列中随机抽取 N 个元素，支持有放回或无放回模式。
        /// </summary>
        public static List<T> _RandomSample<T>(this IList<T> source, int n, bool withReplacement)
        {
            if (source == null) return new List<T>();
            int count = source.Count;
            if (n <= 0) return new List<T>();
            var result = new List<T>(Math.Min(n, count));
            if (withReplacement)
            {
                for (int i = 0; i < n; i++) result.Add(source[UnityEngine.Random.Range(0, count)]);
                return result;
            }
            return _RandomSample(source, n);
        }

        /// <summary>
        /// 返回一个新的被打乱的序列，不修改源集合。
        /// </summary>
        public static List<T> _ShuffleImmutable<T>(this IEnumerable<T> source)
        {
            if (source == null) return new List<T>();
            var list = new List<T>(source);
            list._RandomShuffle();
            return list;
        }

        /// <summary>
        /// 按权重从数组中选择一个索引（权重为 int）。返回 -1 表示失败或总权重为0。
        /// </summary>
        public static int _WeightedRandomIndex(this int[] weights)
        {
            if (weights == null || weights.Length == 0) return -1;
            long total = 0;
            foreach (var w in weights) { if (w > 0) total += w; }
            if (total <= 0) return -1;
            long r = UnityEngine.Random.Range(0, (int)total);
            long sum = 0;
            for (int i = 0; i < weights.Length; i++)
            {
                if (weights[i] <= 0) continue;
                sum += weights[i];
                if (r < sum) return i;
            }
            return -1;
        }

        /// <summary>
        /// 按权重从数组中选择一个索引（权重为 float）。返回 -1 表示失败或总权重为0。
        /// </summary>
        public static int _WeightedRandomIndex(this float[] weights)
        {
            if (weights == null || weights.Length == 0) return -1;
            float total = 0f;
            foreach (var w in weights) { if (w > 0f) total += w; }
            if (!(total > 0f)) return -1;
            float r = UnityEngine.Random.value * total;
            float sum = 0f;
            for (int i = 0; i < weights.Length; i++)
            {
                if (weights[i] <= 0f) continue;
                sum += weights[i];
                if (r < sum) return i;
            }
            return -1;
        }
   /// <summary>
        /// 按权重从数组中选择一个索引（权重为 int）。返回 -1 表示失败或总权重为0。
        /// </summary>
        public static int _WeightedRandomIndex(this List<int> weights)
        {
            if (weights == null || weights.Count == 0) return -1;
            long total = 0;
            foreach (var w in weights) { if (w > 0) total += w; }
            if (total <= 0) return -1;
            long r = UnityEngine.Random.Range(0, (int)total);
            long sum = 0;
            for (int i = 0; i < weights.Count; i++)
            {
                if (weights[i] <= 0) continue;
                sum += weights[i];
                if (r < sum) return i;
            }
            return -1;
        }

        /// <summary>
        /// 按权重从数组中选择一个索引（权重为 float）。返回 -1 表示失败或总权重为0。
        /// </summary>
        public static int _WeightedRandomIndex(this List<float> weights)
        {
            if (weights == null || weights.Count == 0) return -1;
            float total = 0f;
            foreach (var w in weights) { if (w > 0f) total += w; }
            if (!(total > 0f)) return -1;
            float r = UnityEngine.Random.value * total;
            float sum = 0f;
            for (int i = 0; i < weights.Count; i++)
            {
                if (weights[i] <= 0f) continue;
                sum += weights[i];
                if (r < sum) return i;
            }
            return -1;
        }

        /// <summary>
        /// 从枚举（可能是流）中在线抽取 k 个样本（Reservoir Sampling）。
        /// </summary>
        public static List<T> _ReservoirSample<T>(this IEnumerable<T> source, int k)
        {
            var reservoir = new List<T>(k);
            if (source == null || k <= 0) return reservoir;
            int i = 0;
            var rng = new System.Random();
            foreach (var item in source)
            {
                i++;
                if (i <= k) reservoir.Add(item);
                else
                {
                    int j = rng.Next(0, i);
                    if (j < k) reservoir[j] = item;
                }
            }
            return reservoir;
        }

        /// <summary>
        /// 生成随机索引序列（可选择无放回或有放回）。
        /// </summary>
        public static IEnumerable<int> _GetRandomIndices(int n, int maxExclusive, bool withReplacement = false)
        {
            if (n <= 0 || maxExclusive <= 0) yield break;
            if (withReplacement)
            {
                for (int i = 0; i < n; i++) yield return UnityEngine.Random.Range(0, maxExclusive);
                yield break;
            }
            // 无放回：生成 [0,maxExclusive) 的随机排列并取前 n
            var indices = new List<int>(maxExclusive);
            for (int i = 0; i < maxExclusive; i++) indices.Add(i);
            indices._RandomShuffle();
            for (int i = 0; i < Math.Min(n, maxExclusive); i++) yield return indices[i];
        }

       

        #endregion


      
    }
}

