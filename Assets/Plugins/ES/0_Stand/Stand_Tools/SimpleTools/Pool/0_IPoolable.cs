using System;

namespace ES
{
    /// <summary>
    /// 可池化对象接口
    /// </summary>
    public interface IPoolable
    {
        /// <summary>
        /// 重置对象状态，准备回收到池中
        /// </summary>
        void OnResetAsPoolable();

        /// <summary>
        /// 标记对象是否已被回收
        /// </summary>
        bool IsRecycled { get; set; }
    }

    /// <summary>
    /// 支持自动回池的对象接口
    /// </summary>
    public interface IPoolableAuto : IPoolable
    {
        /// <summary>
        /// 尝试自动回收到对象池
        /// </summary>
        void TryAutoPushedToPool();
    }
}