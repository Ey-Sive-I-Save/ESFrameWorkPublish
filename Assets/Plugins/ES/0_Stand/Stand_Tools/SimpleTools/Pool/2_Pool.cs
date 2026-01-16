using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// 对象池基类（商业级实现）
    /// 主线程优先：在主线程时尽量避免加锁；若从其他线程访问则用锁保护
    /// </summary>
    /// <typeparam name="T">池化对象类型</typeparam>
    public abstract class Pool<T> : IPool<T> where T : IPoolable
    {
        /// <summary>
        /// 当前池中对象数量
        /// </summary>
        public int CurCount
        {
            get { return mObjectStack.Count; }
        }

        protected IFactory<T> mFactory;

        // ...已移除线程安全锁...

        // 统计信息（仅在编辑器下启用）
#if UNITY_EDITOR
        protected readonly PoolStatistics mStatistics = new PoolStatistics();
#endif

        /// <summary>
        /// 直接设置工厂
        /// </summary>
        public void SetFactoryDirectly(IFactory<T> factory)
        {
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));
            mFactory = factory;
        }

        /// <summary>
        /// 通过函数设置工厂
        /// </summary>
        public void SetFactoryByFunc(Func<T> factoryMethod)
        {
            if (factoryMethod == null)
                throw new ArgumentNullException(nameof(factoryMethod));
            mFactory = new ESFactory_Custom<T>(factoryMethod);
        }

        /// <summary>
        /// 存储相关数据的栈
        /// </summary>
        protected readonly Stack<T> mObjectStack = new Stack<T>();

        // 追踪所有创建的对象（用于防止重复回收）
        protected readonly HashSet<T> mCreatedObjects = new HashSet<T>();

        /// <summary>
        /// 清空对象池
        /// </summary>
        public void Clear(Action<T> onClearItem = null)
        {
            if (onClearItem != null)
            {
                foreach (var poolObject in mObjectStack)
                {
                    onClearItem(poolObject);
                }
            }
            mObjectStack.Clear();
            mCreatedObjects.Clear();
#if UNITY_EDITOR
            // 重置统计信息（保留总创建数，其他清零，0 GC）
            int totalCreated = mStatistics.TotalCreated;
            mStatistics.ResetExceptCreated();
            mStatistics.TotalCreated = totalCreated;
#endif
        }

        /// <summary>
        /// 对象池最大容量（超出此容量的对象将被丢弃）
        /// </summary>
        protected int mMaxCount = 128;

        /// <summary>
        /// 是否启用容量限制
        /// </summary>
        protected bool mEnableMaxLimit = true;

        /// <summary>
        /// 设置最大容量
        /// </summary>
        public void SetMaxCount(int maxCount, bool enableLimit = true)
        {
            if (maxCount <= 0)
                throw new ArgumentException("Max count must be greater than 0", nameof(maxCount));
            mMaxCount = maxCount;
            mEnableMaxLimit = enableLimit;
            // 如果当前数量超过新的最大值，清理多余对象
            while (mEnableMaxLimit && mObjectStack.Count > mMaxCount)
            {
                var obj = mObjectStack.Pop();
                mCreatedObjects.Remove(obj);
                OnObjectDisposed(obj);
            }
        }

        /// <summary>
        /// 从池中获取对象
        /// </summary>
        public virtual T GetInPool()
        {
            T use;
            if (mObjectStack.Count == 0)
            {
                use = mFactory.Create();
                mCreatedObjects.Add(use);
#if UNITY_EDITOR
                mStatistics.TotalCreated++;
#endif
            }
            else
            {
                use = mObjectStack.Pop();
            }
#if UNITY_EDITOR
            mStatistics.TotalGets++;
            mStatistics.CurrentActive = mStatistics.TotalGets - mStatistics.TotalReturns;
            mStatistics.CurrentPooled = mObjectStack.Count;
            if (mStatistics.CurrentActive > mStatistics.PeakActive)
                mStatistics.PeakActive = mStatistics.CurrentActive;
#endif
            use.IsRecycled = false;
            return use;
        }

        /// <summary>
        /// 将对象放回池中
        /// </summary>
        public abstract bool PushToPool(T obj);

        /// <summary>
        /// 获取对象池统计信息
        /// </summary>
        public PoolStatistics GetStatistics()
        {
#if UNITY_EDITOR
            mStatistics.CurrentPooled = mObjectStack.Count;
            mStatistics.CurrentActive = mStatistics.TotalGets - mStatistics.TotalReturns;
            return mStatistics;
#else
            return null;
#endif
        }

        
          
        /// <summary>
        /// 预热对象池（提前创建指定数量的对象）
        /// </summary>
        public void Prewarm(int count)
        {
            if (count <= 0)
                return;
            for (int i = 0; i < count; i++)
            {
                if (mEnableMaxLimit && mObjectStack.Count >= mMaxCount)
                    break;
                var obj = mFactory.Create();
                obj.OnResetAsPoolable();
                obj.IsRecycled = true;
                mObjectStack.Push(obj);
                mCreatedObjects.Add(obj);
#if UNITY_EDITOR
                mStatistics.TotalCreated++;
#endif
            }
        }

        /// <summary>
        /// 当对象被销毁时调用（用于清理资源）
        /// </summary>
        protected virtual void OnObjectDisposed(T obj)
        {
            // 子类可以重写此方法来处理对象销毁逻辑
            if (obj is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        /// <summary>
        /// 设置池的分组名称（仅在编辑器下有效）
        /// </summary>
        /// <param name="groupName">新的分组名称</param>
        public void SetGroupName(string groupName)
        {
#if UNITY_EDITOR
            if (mStatistics != null && !string.IsNullOrEmpty(groupName))
            {
                // 使用 LoadOrJump 功能自动处理分组跳转
                PoolStatistics.GlobalStatisticsGroup.LoadOrJump(mStatistics, groupName);
                // 更新本地 GroupName
                mStatistics.GroupName = groupName;
            } 
#endif
        }

        /// <summary>
        /// 设置池的显示名称（仅在编辑器下有效）
        /// </summary>
        /// <param name="displayName">新的显示名称</param>
        public void SetDisplayName(string displayName)
        {
#if UNITY_EDITOR
            if (mStatistics != null)
            {
                mStatistics.PoolDisplayName = displayName ?? typeof(T)._GetTypeDisplayName();
            }
#endif
        }

        /// <summary>
        /// 设置池的可见性（仅在编辑器下有效）
        /// </summary>
        /// <param name="isVisible">是否可见</param>
        public void SetVisibility(bool isVisible)
        {
#if UNITY_EDITOR
            if (mStatistics != null)
            {
                mStatistics.IsValid = isVisible;
            }
#endif
        }
    }
}