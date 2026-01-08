using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Runtime.CompilerServices;
namespace ES
{
    /// <summary>
    /// 安全字典 - 永远不会因为键不存在而抛出异常
    /// 继承自 Dictionary，完全兼容现有代码
    /// </summary>
    [Serializable]
    public class SafeDictionary<TKey, TValue> : Dictionary<TKey, TValue>
    {
        #region 核心字段
        /// <summary>
        /// 默认值工厂 - 用于生成键不存在时的默认值
        /// </summary>
        public Func<TValue> _defaultValueFactory = null;
        /// <summary>
        /// 是否在访问不存在的键时自动加入字典
        /// </summary>
        public bool _autoCreateOnAccess = true;
        #endregion

        #region 构造函数
        public SafeDictionary() : base()
        {
            _defaultValueFactory = () => default;
        }

        public SafeDictionary(IEqualityComparer<TKey> comparer) : base(comparer)
        {
            _defaultValueFactory = () => default;
        }

        public SafeDictionary(Func<TValue> defaultValueFactory) : base()
        {
            _defaultValueFactory = defaultValueFactory ?? (() => default);
        }

        public SafeDictionary(Func<TValue> defaultValueFactory, IEqualityComparer<TKey> comparer)
            : base(comparer)
        {
            _defaultValueFactory = defaultValueFactory ?? (() => default);
        }
        #endregion

        #region 核心API - 永远不会报错
        /// <summary>
        /// 永远不会抛出KeyNotFoundException的索引器
        /// 键不存在时：返回默认值，并根据设置自动创建
        /// </summary>
        public new TValue this[TKey key]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (TryGetValue(key, out var value))
                    return value;

                var defaultValue = _defaultValueFactory != null ? _defaultValueFactory() : default;
                if (_autoCreateOnAccess)
                    base[key] = defaultValue;

                return defaultValue;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => base[key] = value;
        }

        /// <summary>
        /// 安全添加 - 键已存在时返回false，而不是抛异常
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Add(TKey key, TValue value, bool overwriteIfExists = false)
        {
            if (ContainsKey(key))
            {
                if (overwriteIfExists)
                {
                    this[key] = value;
                    return true;
                }
                return false;
            }
            base.Add(key, value);
            return true;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public new void Add(TKey key, TValue value)
        {
            Add(key, value);
        }
        /// <summary>
        /// 安全获取 - 永远不会抛异常
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TValue GetValueSafe(TKey key)
        {
            if (TryGetValue(key, out var value))
                return value;

            var defaultValue = _defaultValueFactory != null ? _defaultValueFactory() : default;
            if (_autoCreateOnAccess)
                base[key] = defaultValue;

            return defaultValue;
        }

        /// <summary>
        /// 安全获取 - 可以指定自定义默认值
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TValue GetValueSafe(TKey key, TValue defaultValue)
        {
            if (TryGetValue(key, out var value))
                return value;
            if (_autoCreateOnAccess)
                base[key] = defaultValue;
            return defaultValue;
        }

        #endregion

        #region  设置行为

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetDefaultValueFactory(Func<TValue> factory)
        {
            _defaultValueFactory = factory ?? (() => default);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetAutoCreateOnAccess(bool autoCreate)
        {
            _autoCreateOnAccess = autoCreate;
        }

        #endregion
    }

}
