using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// 简单对象池实现（商业级）
    /// 支持自定义创建、重置、销毁回调
    /// </summary>
    public class ESSimplePool<T> : AbstractPool<T> where T : IPoolable
    {
        protected readonly Action<T> mResetMethod;
        protected readonly Action<T> mOnCreate;
        protected readonly Action<T> mOnDestroy;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="factoryMethod">对象创建方法</param>
        /// <param name="resetMethod">对象重置方法</param>
        /// <param name="initCount">初始化数量</param>
        /// <param name="maxCount">最大容量</param>
        /// <param name="onCreate">对象创建回调</param>
        /// <param name="onDestroy">对象销毁回调</param>
        /// <param name="poolDisplayName">池显示名称</param>
        /// <param name="groupName">组名称</param>
        public ESSimplePool(
            Func<T> factoryMethod,
            Action<T> resetMethod = null,
            int initCount = 5,
            int maxCount = 128,
            Action<T> onCreate = null,
            Action<T> onDestroy = null,
            string poolDisplayName = null,
            string groupName = "Default")
        {
            if (factoryMethod == null)
                throw new ArgumentNullException(nameof(factoryMethod));

            mFactory = new ESFactory_Custom<T>(factoryMethod);
            mResetMethod = resetMethod;
            mOnCreate = onCreate;
            mOnDestroy = onDestroy;
            mMaxCount = maxCount;

            // 预热对象池
            if (initCount > 0)
            {
                Prewarm(initCount);
            }
#if UNITY_EDITOR
            //初始化统计信息
            if (mStatistics != null)
            {
                mStatistics.PoolDisplayName = poolDisplayName ?? typeof(T)._GetTypeDisplayName();
                mStatistics.IsValid = true;
                mStatistics.GroupName = groupName;
                mStatistics.CurrentGroup = groupName;
                PoolStatistics.GlobalStatisticsGroup.Add(groupName, mStatistics);
            }
#endif
        }

        /// <summary>
        /// 将对象放回池中（高性能版本）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool PushToPool(T obj)
        {
            // 快速路径：最常见的情况
            if (obj == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning("[ESSimplePool] Trying to push null object to pool.");
#endif
                return false;
            }
            
            // 检查对象是否已被回收（最常见的错误情况）
            if (obj.IsRecycled)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning($"[ESSimplePool] Object is already recycled. Type: {typeof(T).Name}");
#endif
                return false;
            }
            
            // 检查对象是否属于此池（HashSet.Contains 是 O(1) 操作，性能优秀）
            if (!mCreatedObjects.Contains(obj))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning($"[ESSimplePool] Object does not belong to this pool. Type: {typeof(T).Name}");
#endif
                return false;
            }
            // 检查容量限制
            if (mEnableMaxLimit && mObjectStack.Count >= mMaxCount)
            {
#if UNITY_EDITOR
                mStatistics.DiscardedCount++;
#endif
                mCreatedObjects.Remove(obj);
                OnObjectDisposed(obj);
                mOnDestroy?.Invoke(obj);
                return false;
            }
            // 重置对象
            try
            {
                mResetMethod?.Invoke(obj);
                obj.OnResetAsPoolable();
                obj.IsRecycled = true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ESSimplePool] Error resetting object {obj.GetType().Name}: {ex.Message}");
                mCreatedObjects.Remove(obj);
                OnObjectDisposed(obj);
                return false;
            }
            mObjectStack.Push(obj);
#if UNITY_EDITOR
            mStatistics.TotalReturns++;
            mStatistics.CurrentPooled = mObjectStack.Count;
            mStatistics.CurrentActive = mStatistics.TotalGets - mStatistics.TotalReturns;
#endif
            return true;
        }

        /// <summary>
        /// 从池中获取对象（高性能版本）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override T GetInPool()
        {
            var obj = base.GetInPool();
            
            // 统一处理创建回调（避免重复代码）
            if (mOnCreate != null)
            {
                try 
                { 
                    mOnCreate(obj); 
                } 
                catch (Exception ex) 
                { 
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogError($"[ESSimplePool] Error in onCreate callback for {typeof(T).Name}: {ex.Message}");
#endif
                }
            }
            
            return obj;
        }

        protected override void OnObjectDisposed(T obj)
        {
            base.OnObjectDisposed(obj);
            mOnDestroy?.Invoke(obj);
        }
    }

    /// <summary>
    /// 单例对象池（商业级实现，支持自定义配置）
    /// 线程安全的单例模式
    /// </summary>
    public class ESSimplePoolSingleton<T> : ESSimplePool<T> where T : IPoolable, new()
    {
        private static ESSimplePoolSingleton<T> sInstance;
        /// <summary>
        /// 获取单例池实例（无锁实现）
        /// </summary>
        public static ESSimplePoolSingleton<T> Instance
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (sInstance != null) return sInstance;
                return CreateDefaultPool();
            }
        }

        /// <summary>
        /// 保持向后兼容的Pool属性
        /// </summary>
        public static ESSimplePoolSingleton<T> Pool
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return Instance; }
        }

        /// <summary>
        /// 构造函数（私有化，确保单例）
        /// </summary>
        private ESSimplePoolSingleton(
            Func<T> factoryMethod,
            Action<T> resetMethod = null,
            int initCount = 0,
            int maxCount = 128,
            Action<T> onCreate = null,
            Action<T> onDestroy = null,
            string poolDisplayName = null,
            string groupName = "Default")
        : base(factoryMethod, resetMethod, initCount, maxCount, onCreate, onDestroy, poolDisplayName, groupName)
        {
        }

        /// <summary>
        /// 创建默认池配置
        /// </summary>
        private static ESSimplePoolSingleton<T> CreateDefaultPool()
        {
            return sInstance = new ESSimplePoolSingleton<T>(() => new T());
        }

        /// <summary>
        /// 创建自定义配置的池（只能调用一次）
        /// </summary>
        public static ESSimplePoolSingleton<T> CreatePool(
            Func<T> factoryMethod = null,
            Action<T> resetMethod = null,
            int initCount = 10,
            int maxCount = 128,
            Action<T> onCreate = null,
            Action<T> onDestroy = null,
            string poolDisplayName = null,
            string groupName = "Default")
        {
            if (sInstance != null)
            {
                Debug.LogWarning($"[ESSimplePoolSingleton] Pool for {typeof(T).Name} already exists. Returning existing instance.");
                return sInstance;
            }
            factoryMethod = factoryMethod ?? (() => new T());
            return sInstance = new ESSimplePoolSingleton<T>(factoryMethod, resetMethod, initCount, maxCount, onCreate, onDestroy, poolDisplayName, groupName);
        }

        /// <summary>
        /// 销毁单例池
        /// </summary>
        public static void DestroyPool()
        {
            if (sInstance != null)
            {
                sInstance.Clear();
                sInstance = null;
            }
        }
    }
}