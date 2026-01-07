using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// ES框架 - 流程处理系统(Process System)定义
    /// 
    /// 【核心概念】
    /// Process是借助输入值，通过一系列操作进行数据流处理的载体
    /// 支持管道化处理和多通道操作分发
    /// 
    /// 【处理流程】
    /// Input → [Operation1] → [Operation2] → ... → Output
    ///           ↓Channel1      ↓Channel2           ↓
    ///         SideEffect1    SideEffect2        Result
    /// </summary>
    /// <typeparam name="TSource">输入数据类型</typeparam>
    /// <typeparam name="TOutput">输出结果类型</typeparam>
    /// <typeparam name="TOperation">执行的具体操作类型</typeparam>
    /// <typeparam name="TChannel">通道标识类型</typeparam>
    public interface IProcess<TSource, TOutput, TOperation, TChannel> 
        where TOperation : IOperation
    {
        /// <summary>输入源数据</summary>
        TSource Source { get; set; }
        
        /// <summary>处理结果输出</summary>
        TOutput Output { get; set; }
        
        
        /// <summary>
        /// 执行流程处理
        /// </summary>
        /// <param name="source">输入数据</param>
        /// <returns>处理是否成功</returns>
        void DoProcess(TSource source);
        
        /// <summary>
        /// 添加操作到指定通道
        /// </summary>
        void AddOperation(TOperation operation, TChannel channel);
        
        /// <summary>
        /// 从指定通道移除操作
        /// </summary>
        void RemoveOperation(TOperation operation, TChannel channel);
        
        /// <summary>
        /// 清空指定通道的所有操作
        /// </summary>
        void ClearChannel(TChannel channel);
        
        /// <summary>
        /// 获取通道中的所有操作
        /// </summary>
        IReadOnlyList<TOperation> GetOperations(TChannel channel);
    }
    
    /// <summary>
    /// 简化版本的Process接口，适用于单通道场景
    /// </summary>
    public interface ISingleProcess<TSource, TOutput> : IProcess<TSource, TOutput, IOperation, string>
    {
        /// <summary>添加操作到默认通道</summary>
        void AddOperation(IOperation operation);
        
        /// <summary>从默认通道移除操作</summary>
        void RemoveOperation(IOperation operation);
    }
}

