using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace ES
{
    #region 标准接口
    /// <summary>
    /// ES框架 - 操作支持者接口(Operation Supporter)
    /// 
    /// 【核心概念】
    /// 可装载完整运行时逻辑的对象，为Operation提供运行环境和数据存储支持
    /// 
    /// 【典型实现对象】
    /// • 技能系统 - 为技能Operation提供执行环境
    /// • 高级Buff - 为效果Operation提供持续运行支持  
    /// • 飞行物 - 为弹道Operation提供轨迹计算环境
    /// 
    /// 【提供的核心服务】
    /// • 委托任务存储 - 管理Operation的委托执行
    /// • 缓冲任务存储 - 管理Operation的缓冲处理
    /// • 上下文环境 - 提供运行时上下文数据
    /// • 缓存服务 - 提供高效的数据缓存机制
    /// </summary>
    public interface IOpSupporter : IOpStoreDictionary // 满足委托任务存储需求
    <IOperation, DeleAndCount, OutputOpeationDelegateFlag>,
       IOpStoreKeyGroup // 满足缓冲任务存储需求
       <ES.OutputOperationBufferFloat_TargetAndDirectInput
       <ES.ESRuntimeTarget, ES.IOpSupporter, ES.ESRuntimeOpSupport_ValueEntryFloatOperation>,
        ES.BufferOperationFloat, ES.OutputOpeationBufferFlag>
    {
        /// <summary>上下文池 - 提供运行时上下文环境</summary>
        public ContextPool Context { get { return Provider.contextPool; } }
        
        /// <summary>缓存池 - 提供运行中的数据缓存服务</summary>
        public CacherPool Cacher { get { return Provider.cacherPool; } }

        /// <summary>
        /// 获取委托任务存储字典
        /// </summary>
        /// <param name="flag">委托操作标志位</param>
        /// <returns>委托任务的安全字典存储</returns>
        SafeDictionary<IOperation, DeleAndCount> IOpStoreDictionary // 满足委托任务存储
        <IOperation, DeleAndCount, OutputOpeationDelegateFlag>.GetFromOpStore(OutputOpeationDelegateFlag flag)
        {
            return Provider.storeFordele;
        }

        /// <summary>
        /// 获取缓冲任务存储键组
        /// </summary>
        /// <param name="flag">缓冲操作标志位</param>
        /// <returns>缓冲任务的安全键组存储</returns>
        SafeKeyGroup<OutputOperationBufferFloat_TargetAndDirectInput<ESRuntimeTarget, IOpSupporter, ESRuntimeOpSupport_ValueEntryFloatOperation>, BufferOperationFloat>
        IOpStoreKeyGroup // 满足缓冲任务存储
        <ES.OutputOperationBufferFloat_TargetAndDirectInput
        <ES.ESRuntimeTarget, ES.IOpSupporter, ES.ESRuntimeOpSupport_ValueEntryFloatOperation>,
        ES.BufferOperationFloat, ES.OutputOpeationBufferFlag>.GetFromOpStore(OutputOpeationBufferFlag flag)
        {
            return Provider.storeForbufer;
        }

        /// <summary>操作支持提供者 - 实际提供各种服务的核心对象</summary>
        public OpSupportProvider Provider { get; }

    }

    #endregion

    #region 示例实现
    /// <summary>
    /// 操作支持者的简单示例实现
    /// 
    /// 【用途】
    /// • 演示IOpSupporter接口的基本实现方式
    /// • 提供快速创建OpSupporter的模板
    /// • 适用于简单场景的Operation支持
    /// 
    /// 【特点】
    /// • 使用默认的OpSupportProvider
    /// • 无自定义逻辑，直接委托给Provider处理
    /// • 适合快速原型和测试场景
    /// </summary>
    public class OpSupport_Examples : IOpSupporter
    {
        /// <summary>获取操作支持提供者实例</summary>
        public OpSupportProvider Provider => provider;

        /// <summary>内部的操作支持提供者实例</summary>
        private readonly OpSupportProvider provider = new OpSupportProvider();
    }
    #endregion

    #region 标准Provider
    /// <summary>
    /// 操作支持标准提供者
    /// 
    /// 【设计理念】
    /// 通用提供者 - 不带选择的全盘接受所有Operation请求
    /// 为所有类型的Operation提供统一的运行环境和存储服务
    /// 
    /// 【核心功能】
    /// • 上下文池管理 - 维护运行时上下文环境
    /// • 缓存池管理 - 提供高效的数据缓存服务
    /// • 委托存储管理 - 处理Operation的委托任务
    /// • 缓冲存储管理 - 处理Operation的缓冲任务
    /// 
    /// 【适用场景】
    /// • 通用Operation支持环境
    /// • 不需要特殊定制的标准场景
    /// • 快速搭建Operation运行环境
    /// </summary>
    public class OpSupportProvider : IOpSupporter
    {
        #region 核心数据池
        /// <summary>上下文池 - 提供运行时上下文环境支持</summary>
        public ContextPool contextPool;
        
        /// <summary>缓存池 - 提供运行时数据缓存支持</summary>
        public CacherPool cacherPool;
        
        /// <summary>委托任务存储 - 管理Operation的委托执行</summary>
        public SafeDictionary<IOperation, DeleAndCount> storeFordele = new SafeDictionary<IOperation, DeleAndCount>();
        
        /// <summary>缓冲任务存储 - 管理Operation的缓冲处理</summary>
        public SafeKeyGroup<OutputOperationBufferFloat_TargetAndDirectInput
        <ESRuntimeTarget, IOpSupporter, ESRuntimeOpSupport_ValueEntryFloatOperation>
        , BufferOperationFloat> storeForbufer = new();
        #endregion

        #region IOpStore接口实现
        /// <summary>
        /// 获取委托任务存储字典
        /// </summary>
        /// <param name="flag">委托操作标志位（当前实现忽略标志位）</param>
        /// <returns>委托任务的安全字典存储</returns>
        public SafeDictionary<IOperation, DeleAndCount> GetFromOpStore(OutputOpeationDelegateFlag flag = null)
        {
            return storeFordele;
        }

        /// <summary>
        /// 获取缓冲任务存储键组
        /// </summary>
        /// <param name="flag">缓冲操作标志位（当前实现忽略标志位）</param>
        /// <returns>缓冲任务的安全键组存储</returns>
        public SafeKeyGroup<OutputOperationBufferFloat_TargetAndDirectInput<ESRuntimeTarget, IOpSupporter, ESRuntimeOpSupport_ValueEntryFloatOperation>, BufferOperationFloat> GetFromOpStore(OutputOpeationBufferFlag flag = null)
        {
            return storeForbufer;
        }

        #endregion

        #region 属性访问器
        /// <summary>获取上下文池实例</summary>
        public ContextPool Context => contextPool;
        
        /// <summary>获取缓存池实例</summary>
        public CacherPool Cacher => cacherPool;

        /// <summary>获取操作支持提供者（返回自身）</summary>
        public OpSupportProvider Provider => this;
        #endregion
    }
    #endregion

    #region 重写标志位定义
    // TODO: 在此区域定义Operation相关的标志位枚举
    // 例如：委托操作标志位、缓冲操作标志位等
    #endregion
}
