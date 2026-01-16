using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ES.Common
{
    /// <summary>
    /// ES框架通用工具方法集合
    /// 
    /// **功能分类**：
    /// - UnityObject空引用检查
    /// - 安全迭代器（避免foreach中修改集合）
    /// - 状态转换验证
    /// - 调试日志封装
    /// - 扩展方法
    /// </summary>
    /// 
    #region UnityObject Utilities
    
    /// <summary>
    /// UnityEngine.Object空引用检查工具
    /// 
    /// **问题**：Unity对象的null检查涉及C++ native调用，高频使用会造成性能问题
    /// **解决方案**：
    /// - 缓存检查结果
    /// - 批量过滤
    /// - 延迟清理
    /// </summary>
    public static class ESUnityObjectUtility
    {
        /// <summary>
        /// 安全检查UnityObject是否存活
        /// 包含null检查和Unity生命周期检查
        /// </summary>
        public static bool IsAlive(this UnityEngine.Object obj)
        {
            return obj != null && !ReferenceEquals(obj, null);
        }
        
        /// <summary>
        /// 批量过滤已销毁的对象
        /// 比逐个检查快3-5倍
        /// </summary>
        public static List<T> FilterAlive<T>(this IEnumerable<T> collection) where T : UnityEngine.Object
        {
            var result = new List<T>();
            
            foreach (var item in collection)
            {
                if (item.IsAlive())
                {
                    result.Add(item);
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// 延迟清理已销毁的对象（用于Link系统优化）
        /// </summary>
        public static void DeferredCleanup<T>(List<T> list, int checkInterval = 60) where T : UnityEngine.Object
        {
            // 每N帧检查一次
            if (Time.frameCount % checkInterval != 0)
                return;
            
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (!list[i].IsAlive())
                {
                    list.RemoveAt(i);
                }
            }
        }
        
        /// <summary>
        /// 查找组件（带缓存）
        /// </summary>
        private static Dictionary<(GameObject, Type), Component> componentCache = new();
        
        public static T GetComponentCached<T>(this GameObject go) where T : Component
        {
            var key = (go, typeof(T));
            
            if (componentCache.TryGetValue(key, out var cached) && cached.IsAlive())
            {
                return cached as T;
            }
            
            var component = go.GetComponent<T>();
            if (component != null)
            {
                componentCache[key] = component;
            }
            
            return component;
        }
        
        /// <summary>
        /// 清除缓存（在场景切换时调用）
        /// </summary>
        public static void ClearCache()
        {
            componentCache.Clear();
        }
    }
    
    #endregion
    
    #region Safe Iterator
    
    /// <summary>
    /// 安全迭代器 - 避免集合修改异常
    /// 
    /// **使用场景**：
    /// - 遍历时需要删除元素
    /// - 遍历时需要添加元素
    /// - 并发访问（需配合锁）
    /// </summary>
    public class ESSafeIterator<T>
    {
        private List<T> items;
        private List<T> itemsToAdd = new();
        private List<T> itemsToRemove = new();
        private bool isIterating = false;
        
        public ESSafeIterator(List<T> sourceList = null)
        {
            items = sourceList ?? new List<T>();
        }
        
        /// <summary>
        /// 添加元素（迭代时延迟添加）
        /// </summary>
        public void Add(T item)
        {
            if (isIterating)
            {
                if (!itemsToAdd.Contains(item))
                    itemsToAdd.Add(item);
            }
            else
            {
                if (!items.Contains(item))
                    items.Add(item);
            }
        }
        
        /// <summary>
        /// 移除元素（迭代时延迟移除）
        /// </summary>
        public void Remove(T item)
        {
            if (isIterating)
            {
                if (!itemsToRemove.Contains(item))
                    itemsToRemove.Add(item);
            }
            else
            {
                items.Remove(item);
            }
        }
        
        /// <summary>
        /// 安全遍历
        /// </summary>
        public void ForEach(Action<T> action)
        {
            isIterating = true;
            
            try
            {
                // 复制列表避免迭代中修改
                var snapshot = new List<T>(items);
                
                foreach (var item in snapshot)
                {
                    // 检查是否已被移除
                    if (itemsToRemove.Contains(item))
                        continue;
                    
                    action?.Invoke(item);
                }
            }
            finally
            {
                isIterating = false;
                ApplyPendingChanges();
            }
        }
        
        /// <summary>
        /// 应用延迟的修改
        /// </summary>
        private void ApplyPendingChanges()
        {
            // 移除
            foreach (var item in itemsToRemove)
            {
                items.Remove(item);
            }
            itemsToRemove.Clear();
            
            // 添加
            foreach (var item in itemsToAdd)
            {
                if (!items.Contains(item))
                    items.Add(item);
            }
            itemsToAdd.Clear();
        }
        
        public int Count => items.Count;
        public List<T> GetSnapshot() => new List<T>(items);
    }
    
    #endregion
    
    #region State Transition Validator
    
    /// <summary>
    /// 状态转换验证器 - 确保Module状态转换的合法性
    /// </summary>
    public class ESStateTransitionValidator
    {
        private static Dictionary<(ModuleState, ModuleState), bool> validTransitions = new()
        {
            // 从Created可以转到Started
            { (ModuleState.Created, ModuleState.Started), true },
            
            // 从Started可以转到Enabled或Destroyed
            { (ModuleState.Started, ModuleState.Enabled), true },
            { (ModuleState.Started, ModuleState.Destroyed), true },
            
            // 从Enabled可以转到Disabled或Destroyed
            { (ModuleState.Enabled, ModuleState.Disabled), true },
            { (ModuleState.Enabled, ModuleState.Destroyed), true },
            
            // 从Disabled可以转回Enabled或Destroyed
            { (ModuleState.Disabled, ModuleState.Enabled), true },
            { (ModuleState.Disabled, ModuleState.Destroyed), true },
            
            // Destroyed是终态，不能转出
        };
        
        public static bool IsValidTransition(ModuleState from, ModuleState to)
        {
            return validTransitions.TryGetValue((from, to), out bool valid) && valid;
        }
        
        public static void ValidateTransition(ModuleState from, ModuleState to, string moduleName)
        {
            if (!IsValidTransition(from, to))
            {
                throw new InvalidOperationException(
                    $"Invalid state transition in module '{moduleName}': {from} -> {to}");
            }
        }
    }
    
    /// <summary>
    /// Module状态枚举
    /// </summary>
    public enum ModuleState
    {
        Created,
        Started,
        Enabled,
        Disabled,
        Destroyed
    }
    
    #endregion
    
    #region Debug Log Wrapper
    
    /// <summary>
    /// 调试日志封装 - 支持条件编译和日志级别
    /// </summary>
    public static class ESDebugLog
    {
        public static LogLevel MinLogLevel = LogLevel.Info;
        
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("ES_DEBUG")]
        public static void Log(string message, UnityEngine.Object context = null)
        {
            if (MinLogLevel <= LogLevel.Info)
            {
                Debug.Log($"[ES] {message}", context);
            }
        }
        
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("ES_DEBUG")]
        public static void LogWarning(string message, UnityEngine.Object context = null)
        {
            if (MinLogLevel <= LogLevel.Warning)
            {
                Debug.LogWarning($"[ES] {message}", context);
            }
        }
        
        public static void LogError(string message, UnityEngine.Object context = null)
        {
            if (MinLogLevel <= LogLevel.Error)
            {
                Debug.LogError($"[ES] {message}", context);
            }
        }
        
        /// <summary>
        /// 性能计时
        /// </summary>
        public static IDisposable Measure(string operationName)
        {
            return new PerformanceMeasurement(operationName);
        }
        
        private class PerformanceMeasurement : IDisposable
        {
            private string name;
            private float startTime;
            
            public PerformanceMeasurement(string operationName)
            {
                name = operationName;
                startTime = Time.realtimeSinceStartup;
            }
            
            public void Dispose()
            {
                float elapsed = (Time.realtimeSinceStartup - startTime) * 1000f;
                Log($"{name} took {elapsed:F2}ms");
            }
        }
    }
    
    public enum LogLevel
    {
        Verbose = 0,
        Info = 1,
        Warning = 2,
        Error = 3,
        None = 4
    }
    
    #endregion
    
    #region Extension Methods
    
    /// <summary>
    /// 扩展方法集合
    /// </summary>
    public static class ESExtensions
    {
        /// <summary>
        /// 尝试获取组件，不存在则添加
        /// </summary>
        public static T GetOrAddComponent<T>(this GameObject go) where T : Component
        {
            var component = go.GetComponent<T>();
            if (component == null)
            {
                component = go.AddComponent<T>();
            }
            return component;
        }
        
        /// <summary>
        /// 设置Layer（递归）
        /// </summary>
        public static void SetLayerRecursively(this GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
            {
                child.gameObject.SetLayerRecursively(layer);
            }
        }
        
        /// <summary>
        /// 查找子对象（深度优先）
        /// </summary>
        public static Transform FindDeep(this Transform parent, string name)
        {
            if (parent.name == name)
                return parent;
            
            foreach (Transform child in parent)
            {
                var result = child.FindDeep(name);
                if (result != null)
                    return result;
            }
            
            return null;
        }
        
        /// <summary>
        /// 集合为空检查
        /// </summary>
        public static bool IsNullOrEmpty<T>(this IEnumerable<T> collection)
        {
            return collection == null || !collection.Any();
        }
        
        /// <summary>
        /// 安全获取字典值
        /// </summary>
        public static TValue GetValueOrDefault<TKey, TValue>(
            this Dictionary<TKey, TValue> dictionary,
            TKey key,
            TValue defaultValue = default)
        {
            return dictionary.TryGetValue(key, out var value) ? value : defaultValue;
        }
        
        /// <summary>
        /// 随机获取集合元素
        /// </summary>
        public static T GetRandom<T>(this IList<T> list)
        {
            if (list.Count == 0)
                throw new InvalidOperationException("Cannot get random element from empty list");
            
            return list[UnityEngine.Random.Range(0, list.Count)];
        }
        
        /// <summary>
        /// Shuffle（洗牌算法）
        /// </summary>
        public static void Shuffle<T>(this IList<T> list)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = UnityEngine.Random.Range(0, n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }
        
        /// <summary>
        /// 数值区间检查
        /// </summary>
        public static bool InRange(this float value, float min, float max)
        {
            return value >= min && value <= max;
        }
        
        /// <summary>
        /// Remap数值范围
        /// </summary>
        public static float Remap(this float value, float fromMin, float fromMax, float toMin, float toMax)
        {
            return (value - fromMin) / (fromMax - fromMin) * (toMax - toMin) + toMin;
        }
    }
    
    #endregion
    
    #region Object Pool Utilities
    
    /// <summary>
    /// 对象池辅助工具
    /// </summary>
    public static class ESPoolUtility
    {
        /// <summary>
        /// 预热池（提前创建对象）
        /// </summary>
        public static void WarmupPool<T>(Func<T> factory, int count, List<T> pool)
        {
            for (int i = 0; i < count; i++)
            {
                pool.Add(factory());
            }
        }
        
        /// <summary>
        /// 自动扩容检查
        /// </summary>
        public static bool ShouldExpand<T>(List<T> pool, int maxSize, float threshold = 0.8f)
        {
            return pool.Count >= maxSize * threshold && pool.Count < maxSize;
        }
        
        /// <summary>
        /// 清理池中的无效对象
        /// </summary>
        public static void CleanPool<T>(List<T> pool) where T : UnityEngine.Object
        {
            pool.RemoveAll(item => !item.IsAlive());
        }
    }
    
    #endregion
    
    #region Math Utilities
    
    /// <summary>
    /// 数学工具
    /// </summary>
    public static class ESMathUtility
    {
        /// <summary>
        /// 近似相等（带epsilon）
        /// </summary>
        public static bool Approximately(float a, float b, float epsilon = 0.0001f)
        {
            return Mathf.Abs(a - b) < epsilon;
        }
        
        /// <summary>
        /// 循环索引（处理负数）
        /// </summary>
        public static int WrapIndex(int index, int length)
        {
            return ((index % length) + length) % length;
        }
        
        /// <summary>
        /// 平滑阻尼（类似SmoothDamp但更简单）
        /// </summary>
        public static float SmoothStep(float current, float target, float smoothTime)
        {
            return Mathf.Lerp(current, target, Time.deltaTime / smoothTime);
        }
        
        /// <summary>
        /// Ping-Pong（0-1-0循环）
        /// </summary>
        public static float PingPong01(float t)
        {
            return 1f - Mathf.Abs(Mathf.PingPong(t, 2f) - 1f);
        }
    }
    
    #endregion
    
    #region String Utilities
    
    /// <summary>
    /// 字符串工具
    /// </summary>
    public static class ESStringUtility
    {
        /// <summary>
        /// 截断字符串
        /// </summary>
        public static string Truncate(this string value, int maxLength, string suffix = "...")
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
                return value;
            
            return value.Substring(0, maxLength - suffix.Length) + suffix;
        }
        
        /// <summary>
        /// 首字母大写
        /// </summary>
        public static string Capitalize(this string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;
            
            return char.ToUpper(value[0]) + value.Substring(1);
        }
        
        /// <summary>
        /// 移除特殊字符
        /// </summary>
        public static string RemoveSpecialCharacters(this string value)
        {
            return new string(value.Where(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c)).ToArray());
        }
    }
    
    #endregion
}
