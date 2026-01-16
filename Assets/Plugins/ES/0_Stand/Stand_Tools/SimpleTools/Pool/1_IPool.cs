using System;

namespace ES
{
    /// <summary>
    /// 对象池接口
    /// </summary>
    /// <typeparam name="T">池化对象类型</typeparam>
    public interface IPool<T> where T : IPoolable
    {
        /// <summary>
        /// 从池中获取对象
        /// </summary>
        T GetInPool();

        /// <summary>
        /// 将对象放回池中
        /// </summary>
        bool PushToPool(T obj);

        /// <summary>
        /// 清空对象池
        /// </summary>
        void Clear(Action<T> onClearItem = null);

        /// <summary>
        /// 当前池中对象数量
        /// </summary>
        int CurCount { get; }

        /// <summary>
        /// 获取对象池统计信息
        /// </summary>
        PoolStatistics GetStatistics();
    }
}