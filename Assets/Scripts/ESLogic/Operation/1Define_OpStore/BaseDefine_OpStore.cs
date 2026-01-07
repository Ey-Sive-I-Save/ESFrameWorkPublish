using System.Collections.Generic;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// ES框架 - 操作存储系统(Operation Store)定义
    /// 
    /// 【核心概念】
    /// IOpStore非常简单，它要求具有生命周期的OpSupporter能够支持操作类型缓存一些数据
    /// 由于单一职责原则，通常是存储键值对即可，直接以已经序列化的共享操作单元当做键即可
    /// </summary>
    public interface IOpStore
    {
        // 基础接口，用于标记OpStore系列接口
    }

    /// <summary>
    /// 操作存储接口 - 字典方式
    /// 提供基于安全字典的操作数据存储能力
    /// </summary>
    /// <typeparam name="TOperation">操作类型，必须实现IOperation接口</typeparam>
    /// <typeparam name="TValue">存储值类型</typeparam>
    /// <typeparam name="TFlag">标志位类型，用于分组存储</typeparam>
    public interface IOpStoreDictionary<TOperation, TValue, TFlag> : IOpStore
        where TOperation : IOperation
    {
        /// <summary>
        /// 从操作存储中获取指定标志位的安全字典
        /// </summary>
        /// <param name="flag">标志位，用于区分不同的存储分组</param>
        /// <returns>操作与值的安全字典映射</returns>
        SafeDictionary<TOperation, TValue> GetFromOpStore(TFlag flag = default);
    }

    /// <summary>
    /// 操作存储接口 - 安全键组方式
    /// 提供基于安全键组的操作数据存储能力
    /// </summary>
    /// <typeparam name="TOperation">操作类型，必须实现IOperation接口</typeparam>
    /// <typeparam name="TValue">存储值类型</typeparam>
    /// <typeparam name="TFlag">标志位类型，用于分组存储</typeparam>
    public interface IOpStoreKeyGroup<TOperation, TValue, TFlag> : IOpStore
        where TOperation : IOperation
    {
        /// <summary>
        /// 从操作存储中获取指定标志位的安全键组
        /// </summary>
        /// <param name="flag">标志位，用于区分不同的存储分组</param>
        /// <returns>操作与值的安全键组映射</returns>
        SafeKeyGroup<TOperation, TValue> GetFromOpStore(TFlag flag = default);
    }
}
