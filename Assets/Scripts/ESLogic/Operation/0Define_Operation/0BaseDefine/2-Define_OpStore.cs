using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ES 
{
    /// <summary>
    /// ES框架 - 操作存储系统(Operation Store)定义
    /// 
    /// 【核心概念】
    /// OpStore非常简单，它是要求具有生命周期的RuntimeLogic能够支持操作类型缓存一些数据
    /// 由于单一职责原则，通常是存储键值对即可，直接以已经序列化的共享逻辑单元当做键即可
    /// </summary>

    /// <summary>
    /// 普通键值对字典存储接口
    /// </summary>
    /// <typeparam name="OP">操作类型</typeparam>
    /// <typeparam name="Value">存储值类型</typeparam>
    /// <typeparam name="Flag">标志位类型</typeparam>
    public interface IOpStoreDictionary<OP, Value, Flag> 
        where OP : IOperation
    {
        /// <summary>
        /// 从操作存储中获取指定标志位的安全字典
        /// </summary>
        /// <param name="flag">标志位</param>
        /// <returns>操作与值的安全字典映射</returns>
        SafeDictionary<OP, Value> GetFromOpStore(Flag flag = default);
    }

    /// <summary>
    /// 安全键组存储接口
    /// </summary>
    /// <typeparam name="OP">操作类型</typeparam>
    /// <typeparam name="Value">存储值类型</typeparam>
    /// <typeparam name="Flag">标志位类型</typeparam>
    public interface IOpStoreKeyGroup<OP, Value, Flag> 
        where OP : IOperation
    {
        /// <summary>
        /// 从操作存储中获取指定标志位的安全键组
        /// </summary>
        /// <param name="flag">标志位</param>
        /// <returns>操作与值的安全键组映射</returns>
        SafeKeyGroup<OP, Value> GetFromOpStore(Flag flag = default);
    }
}
