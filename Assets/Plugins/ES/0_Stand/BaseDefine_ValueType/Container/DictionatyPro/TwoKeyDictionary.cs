using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;


namespace ES {
    /// <summary>
    /// 多键字典：支持使用两种不同的键中的任意一个查询同一个值
    /// 用于资源加载系统中，可以通过资源名或资源ID等不同方式查询同一资源
    /// </summary>
    /// <typeparam name="TKey1">第一个键类型</typeparam>
    /// <typeparam name="TKey2">第二个键类型</typeparam>
    /// <typeparam name="TValue">存储的值类型</typeparam>
    public class MultiKeyDictionary<TKey1, TKey2, TValue>
    {
        // 主存储：值 -> 两个键的元组
        private Dictionary<TValue, (TKey1 key1, TKey2 key2)> valueToKeys = new Dictionary<TValue, (TKey1, TKey2)>();
        
        // 键1到值的映射
        [ShowInInspector]
        private Dictionary<TKey1, TValue> key1ToValue = new Dictionary<TKey1, TValue>();
        
        // 键2到值的映射
        [ShowInInspector]
        private Dictionary<TKey2, TValue> key2ToValue = new Dictionary<TKey2, TValue>();

        /// <summary>
        /// 添加一个条目，为同一个值关联两个不同的键
        /// </summary>
        /// <param name="key1">第一个键</param>
        /// <param name="key2">第二个键</param>
        /// <param name="value">值</param>
        public void Add(TKey1 key1, TKey2 key2, TValue value)
        {
            if (key1 == null)
                throw new ArgumentException("Key1 cannot be null", nameof(key1));
            
            if (key2 == null)
                throw new ArgumentException("Key2 cannot be null", nameof(key2));

            // 检查键是否已被使用
            if (key1ToValue.ContainsKey(key1))
                throw new ArgumentException($"Key1 '{key1}' already exists", nameof(key1));
            
            if (key2ToValue.ContainsKey(key2))
                throw new ArgumentException($"Key2 '{key2}' already exists", nameof(key2));

            // 检查值是否已存在
            if (valueToKeys.ContainsKey(value))
                throw new ArgumentException("Value already exists in dictionary", nameof(value));

            // 添加映射
            valueToKeys[value] = (key1, key2);
            key1ToValue[key1] = value;
            key2ToValue[key2] = value;
        }

        /// <summary>
        /// 通过第一个键获取值
        /// </summary>
        /// <param name="key1">第一个键</param>
        /// <returns>对应的值</returns>
        public TValue GetByKey1(TKey1 key1)
        {
            if (key1 == null)
                throw new ArgumentException("Key1 cannot be null", nameof(key1));

            if (key1ToValue.TryGetValue(key1, out TValue value))
                return value;
            
            throw new KeyNotFoundException($"Key1 '{key1}' not found");
        }

        /// <summary>
        /// 通过第二个键获取值
        /// </summary>
        /// <param name="key2">第二个键</param>
        /// <returns>对应的值</returns>
        public TValue GetByKey2(TKey2 key2)
        {
            if (key2 == null)
                throw new ArgumentException("Key2 cannot be null", nameof(key2));

            if (key2ToValue.TryGetValue(key2, out TValue value))
                return value;
            
            throw new KeyNotFoundException($"Key2 '{key2}' not found");
        }

        /// <summary>
        /// 尝试通过第一个键获取值
        /// </summary>
        /// <param name="key1">第一个键</param>
        /// <param name="value">输出的值</param>
        /// <returns>是否找到</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetByKey1(TKey1 key1, out TValue value)
        {
            if (key1 == null)
            {
                value = default;
                return false;
            }

            return key1ToValue.TryGetValue(key1, out value);
        }

        /// <summary>
        /// 尝试通过第二个键获取值
        /// </summary>
        /// <param name="key2">第二个键</param>
        /// <param name="value">输出的值</param>
        /// <returns>是否找到</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetByKey2(TKey2 key2, out TValue value)
        {
            if (key2 == null)
            {
                value = default;
                return false;
            }

            return key2ToValue.TryGetValue(key2, out value);
        }

        /// <summary>
        /// 检查是否包含指定的第一个键
        /// </summary>
        /// <param name="key1">第一个键</param>
        /// <returns>是否包含</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ContainsKey1(TKey1 key1)
        {
            return key1 != null && key1ToValue.ContainsKey(key1);
        }

        /// <summary>
        /// 检查是否包含指定的第二个键
        /// </summary>
        /// <param name="key2">第二个键</param>
        /// <returns>是否包含</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ContainsKey2(TKey2 key2)
        {
            return key2 != null && key2ToValue.ContainsKey(key2);
        }

        /// <summary>
        /// 通过值移除条目
        /// </summary>
        /// <param name="value">要移除的值</param>
        /// <returns>是否成功移除</returns>
        public bool Remove(TValue value)
        {
            if (!valueToKeys.TryGetValue(value, out var keys))
                return false;

            // 移除所有映射
            key1ToValue.Remove(keys.key1);
            key2ToValue.Remove(keys.key2);
            valueToKeys.Remove(value);

            return true;
        }

        /// <summary>
        /// 通过第一个键移除条目
        /// </summary>
        /// <param name="key1">第一个键</param>
        /// <returns>是否成功移除</returns>
        public bool RemoveByKey1(TKey1 key1)
        {
            if (key1 == null)
                return false;

            if (key1ToValue.TryGetValue(key1, out TValue value))
            {
                return Remove(value);
            }

            return false;
        }

        /// <summary>
        /// 通过第二个键移除条目
        /// </summary>
        /// <param name="key2">第二个键</param>
        /// <returns>是否成功移除</returns>
        public bool RemoveByKey2(TKey2 key2)
        {
            if (key2 == null)
                return false;

            if (key2ToValue.TryGetValue(key2, out TValue value))
            {
                return Remove(value);
            }

            return false;
        }

        /// <summary>
        /// 获取指定值对应的两个键
        /// </summary>
        /// <param name="value">值</param>
        /// <param name="key1">输出的key1</param>
        /// <param name="key2">输出的key2</param>
        /// <returns>是否找到</returns>
        public bool TryGetKeys(TValue value, out TKey1 key1, out TKey2 key2)
        {
            if (valueToKeys.TryGetValue(value, out var keys))
            {
                key1 = keys.key1;
                key2 = keys.key2;
                return true;
            }

            key1 = default;
            key2 = default;
            return false;
        }

        /// <summary>
        /// 清空所有条目
        /// </summary>
        public void Clear()
        {
            valueToKeys.Clear();
            key1ToValue.Clear();
            key2ToValue.Clear();
        }

        /// <summary>
        /// 获取条目数量
        /// </summary>
        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => valueToKeys.Count;
        }

        /// <summary>
        /// 获取所有值的集合
        /// </summary>
        public Dictionary<TValue, (TKey1 key1, TKey2 key2)>.KeyCollection Values => valueToKeys.Keys;

        /// <summary>
        /// 获取所有key1的集合
        /// </summary>
        public Dictionary<TKey1, TValue>.KeyCollection Key1s => key1ToValue.Keys;

        /// <summary>
        /// 获取所有key2的集合
        /// </summary>
        public Dictionary<TKey2, TValue>.KeyCollection Key2s => key2ToValue.Keys;
    }

    /// <summary>
    /// 双键字典：支持使用两种不同的string键中的任意一个查询同一个值
    /// 用于资源加载系统中，可以通过资源名或资源ID等不同方式查询同一资源
    /// </summary>
    /// <typeparam name="TValue">存储的值类型</typeparam>
    public class TwoStringKeyDictionary<TValue> : MultiKeyDictionary<string, string, TValue>
    {
        /// <summary>
        /// 通过任意一个键获取值
        /// </summary>
        /// <param name="key">键（可以是key1或key2）</param>
        /// <returns>对应的值</returns>
        public TValue Get(string key)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Key cannot be null or empty", nameof(key));

            // 先尝试key1
            if (TryGetByKey1(key, out TValue value))
                return value;
            
            // 再尝试key2
            if (TryGetByKey2(key, out value))
                return value;
            
            throw new KeyNotFoundException($"Key '{key}' not found");
        }

        /// <summary>
        /// 检查是否包含指定的键
        /// </summary>
        /// <param name="key">键（可以是key1或key2）</param>
        /// <returns>是否包含</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ContainsKey(string key)
        {
            return !string.IsNullOrEmpty(key) && 
                   (ContainsKey1(key) || ContainsKey2(key));
        }

        /// <summary>
        /// 通过任意一个键移除条目
        /// </summary>
        /// <param name="key">键（可以是key1或key2）</param>
        /// <returns>是否成功移除</returns>
        public bool RemoveByKey(string key)
        {
            if (string.IsNullOrEmpty(key))
                return false;

            if (RemoveByKey1(key) || RemoveByKey2(key))
            {
                return true;
            }

            return false;
        }
    }
  }
