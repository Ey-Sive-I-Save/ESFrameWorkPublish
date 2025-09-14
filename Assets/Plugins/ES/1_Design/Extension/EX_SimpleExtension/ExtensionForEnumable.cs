using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{

    public static class ExtensionForEnumable 
    {
        // 21. 随机获取数组元素
        public static T _RandomItem<T>(this T[] array)
        {
            if (array == null || array.Length == 0) return default;
            return array[UnityEngine.Random.Range(0, array.Length)];
        }

        // 22. 随机获取列表元素
        public static T _RandomItem<T>(this List<T> list)
        {
            if (list == null || list.Count == 0) return default;
            return list[UnityEngine.Random.Range(0, list.Count)];
        }

        // 23. 打乱列表顺序
        public static void _RandomShuffle<T>(this List<T> list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                int randomIndex = UnityEngine.Random.Range(i, list.Count);
                (list[i], list[randomIndex]) = (list[randomIndex], list[i]);
            }
        }

        // 24. 检查数组是否为空或null
        public static bool _IsNullOrEmpty<T>(this T[] array)
        {
            return array == null || array.Length == 0;
        }

        // 25. 检查列表是否为空或null
        public static bool _IsNullOrEmpty<T>(this List<T> list)
        {
            return list == null || list.Count == 0;
        }

        // 26. 遍历数组并执行操作
        public static void _ForEach<T>(this T[] array, Action<T> action)
        {
            if (array == null || action == null) return;
            foreach (var item in array) action(item);
        }

        // 27. 遍历列表并执行操作
        public static void _ForEach<T>(this List<T> list, Action<T> action)
        {
            if (list == null || action == null) return;
            foreach (var item in list) action(item);
        }

        // 28. 查找满足条件的第一个元素的索引
        public static int _FindIndex<T>(this T[] array, Predicate<T> predicate)
        {
            if (array == null || predicate == null) return -1;
            for (int i = 0; i < array.Length; i++)
                if (predicate(array[i])) return i;
            return -1;
        }

        // 29. 查找满足条件的最后一个元素的索引
        public static int _FindLastIndex<T>(this T[] array, Predicate<T> predicate)
        {
            if (array == null || predicate == null) return -1;
            for (int i = array.Length - 1; i >= 0; i--)
                if (predicate(array[i])) return i;
            return -1;
        }

        // 30. 检查数组是否包含满足条件的元素
        public static bool _ContainsFeetCondition<T>(this T[] array, Predicate<T> predicate)
        {
            return array._FindIndex(predicate) != -1;
        }
    }
}

