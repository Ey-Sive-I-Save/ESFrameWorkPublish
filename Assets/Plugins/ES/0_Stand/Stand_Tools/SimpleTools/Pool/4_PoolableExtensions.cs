using System;

namespace ES
{
    /// <summary>
    /// 对象池扩展方法
    /// </summary>
    public static class PoolableExtensions
    {
        /// <summary>
        /// 自动回收到池中（如果实现了IPoolableAuto接口）
        /// </summary>
        public static void TryRecycle<T>(this T obj) where T : IPoolable
        {
            if (obj is IPoolableAuto autoPoolable)
            {
                autoPoolable.TryAutoPushedToPool();
            }
        }
    }
}