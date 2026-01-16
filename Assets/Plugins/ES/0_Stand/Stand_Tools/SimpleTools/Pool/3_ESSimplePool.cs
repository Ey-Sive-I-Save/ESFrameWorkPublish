using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// 简单对象池实现（商业级）
    /// 支持自定义创建、重置、销毁回调
    /// </summary>
    public class ESSimplePool<T> : Pool<T> where T : IPoolable
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
            int initCount = 0,
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
        /// 将对象放回池中（线程安全）
        /// </summary>
        public override bool PushToPool(T obj)
        {
            if (obj == null)
            {
                Debug.LogWarning("[ESSimplePool] Trying to push null object to pool.");
                return false;
            }
            // 检查对象是否已被回收
            if (obj.IsRecycled)
            {
                Debug.LogWarning($"[ESSimplePool] Object {obj.GetType().Name} is already recycled. Duplicate push detected.");
                return false;
            }
            // 检查对象是否属于此池
            if (!mCreatedObjects.Contains(obj))
            {
                Debug.LogWarning($"[ESSimplePool] Object {obj.GetType().Name} does not belong to this pool.");
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
        /// 从池中获取对象（重写以添加创建回调）
        /// </summary>
        public override T GetInPool()
        {
            // 主线程实现：调用基类（已为主线程实现）
            var obj = base.GetInPool();
#if UNITY_EDITOR
            if (mOnCreate != null && mStatistics.TotalCreated > 0)
            {
                // 如果是刚创建的对象，触发回调（注意：TotalCreated 自增较快，仅供指示）
                try { mOnCreate(obj); } catch (Exception ex) { Debug.LogError($"[ESSimplePool] Error in onCreate callback: {ex.Message}"); }
            }
#else
            if (mOnCreate != null)
            {
                try { mOnCreate(obj); } catch (Exception ex) { Debug.LogError($"[ESSimplePool] Error in onCreate callback: {ex.Message}"); }
            }
#endif
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