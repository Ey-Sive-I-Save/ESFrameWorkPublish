using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ES {
    /// <summary>
    /// 双键字典：支持使用两种不同的string键中的任意一个查询同一个值
    /// 用于资源加载系统中，可以通过资源名或资源ID等不同方式查询同一资源
    /// </summary>
    /// <typeparam name="TValue">存储的值类型</typeparam>
    public class TwoKeyDictionary<TValue>
    {
        // 主存储：值 -> 两个键的元组
        private Dictionary<TValue, (string key1, string key2)> valueToKeys = new Dictionary<TValue, (string, string)>();
        
        // 键1到值的映射
        private Dictionary<string, TValue> key1ToValue = new Dictionary<string, TValue>();
        
        // 键2到值的映射
        private Dictionary<string, TValue> key2ToValue = new Dictionary<string, TValue>();

        /// <summary>
        /// 添加一个条目，为同一个值关联两个不同的键
        /// </summary>
        /// <param name="key1">第一个键</param>
        /// <param name="key2">第二个键</param>
        /// <param name="value">值</param>
        public void Add(string key1, string key2, TValue value)
        {
            if (string.IsNullOrEmpty(key1))
                throw new ArgumentException("Key1 cannot be null or empty", nameof(key1));
            
            if (string.IsNullOrEmpty(key2))
                throw new ArgumentException("Key2 cannot be null or empty", nameof(key2));

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
        /// 通过任意一个键获取值
        /// </summary>
        /// <param name="key">键（可以是key1或key2）</param>
        /// <returns>对应的值</returns>
        public TValue Get(string key)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Key cannot be null or empty", nameof(key));

            // 先尝试key1
            if (key1ToValue.TryGetValue(key, out TValue value))
                return value;
            
            // 再尝试key2
            if (key2ToValue.TryGetValue(key, out value))
                return value;
            
            throw new KeyNotFoundException($"Key '{key}' not found");
        }

        /// <summary>
        /// 尝试通过第一个键获取值
        /// </summary>
        /// <param name="key1">第一个键</param>
        /// <param name="value">输出的值</param>
        /// <returns>是否找到</returns>
        public bool TryGetByKey1(string key1, out TValue value)
        {
            if (string.IsNullOrEmpty(key1))
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
        public bool TryGetByKey2(string key2, out TValue value)
        {
            if (string.IsNullOrEmpty(key2))
            {
                value = default;
                return false;
            }

            return key2ToValue.TryGetValue(key2, out value);
        }

        /// <summary>
        /// 检查是否包含指定的键
        /// </summary>
        /// <param name="key">键（可以是key1或key2）</param>
        /// <returns>是否包含</returns>
        public bool ContainsKey(string key)
        {
            return !string.IsNullOrEmpty(key) && 
                   (key1ToValue.ContainsKey(key) || key2ToValue.ContainsKey(key));
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
        /// 通过任意一个键移除条目
        /// </summary>
        /// <param name="key">键（可以是key1或key2）</param>
        /// <returns>是否成功移除</returns>
        public bool RemoveByKey(string key)
        {
            if (string.IsNullOrEmpty(key))
                return false;

            TValue value;
            if (key1ToValue.TryGetValue(key, out value) || key2ToValue.TryGetValue(key, out value))
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
        public bool TryGetKeys(TValue value, out string key1, out string key2)
        {
            if (valueToKeys.TryGetValue(value, out var keys))
            {
                key1 = keys.key1;
                key2 = keys.key2;
                return true;
            }

            key1 = null;
            key2 = null;
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
        public int Count => valueToKeys.Count;

        /// <summary>
        /// 获取所有值的集合
        /// </summary>
        public Dictionary<TValue, (string key1, string key2)>.KeyCollection Values => valueToKeys.Keys;

        /// <summary>
        /// 获取所有key1的集合
        /// </summary>
        public Dictionary<string, TValue>.KeyCollection Key1s => key1ToValue.Keys;

        /// <summary>
        /// 获取所有key2的集合
        /// </summary>
        public Dictionary<string, TValue>.KeyCollection Key2s => key2ToValue.Keys;
    }
}
