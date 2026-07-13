using System;

namespace ES
{
    /// <summary>
    /// 运行时初始化接口 - 统一管理所有需要运行时初始化的数据结构
    /// 设计原则：
    /// 1. 上级递归向下级调用，确保数据完整初始化
    /// 2. 重复调用被自动拦截（通过内部标记）
    /// 3. 用于享元数据的预备计算和缓存优化
    /// </summary>
    public interface IRuntimeInitializable
    {
        /// <summary>
        /// 运行时初始化 - 执行预计算和缓存准备
        /// 重复调用会被自动拦截
        /// </summary>
        void InitializeRuntime();
        
        /// <summary>
        /// 是否已初始化
        /// </summary>
        bool IsRuntimeInitialized { get; }
    }
}
