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
    <IOperation, DeleAndCount, OutputOperationDelegateFlag>,
       IOpStoreKeyGroup // 满足缓冲任务存储需求
       <ES.OutputOperationBufferFloat_TargetAndDirectInput
       <ES.ESRuntimeTarget, ES.IOpSupporter, ES.ESRuntimeOpSupport_ValueEntryFloatOperation>,
        ES.BufferOperationFloat, ES.OutputOperationBufferFlag>
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
        <IOperation, DeleAndCount, OutputOperationDelegateFlag>.GetFromOpStore(OutputOperationDelegateFlag flag)
        {
            return Provider.storeForDelegate;
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
        ES.BufferOperationFloat, ES.OutputOperationBufferFlag>.GetFromOpStore(OutputOperationBufferFlag flag)
        {
            return Provider.storeForBuffer;
        }

        /// <summary>
        /// 操作支持提供者 - 实际提供各种服务的核心对象
        /// 【设计意图】Provider模式让用户可以快速获得完整实现，避免手动实现接口的繁重任务
        /// 【使用方式】通过Provider属性访问所有核心服务（上下文池、缓存池、存储等）
        /// 【优势】即插即用，零配置开箱可用
        /// </summary>
        public OpSupportProvider Provider { get; }

    }

    #endregion

    #region 实现案例
    /// <summary>
    /// 操作支持者的简单示例实现
    /// 【核心设计】通过Provider模式实现零配置快速搭建
    ///
    /// 【实现原理】
    /// • 内部创建OpSupportProvider实例作为Provider
    /// • 所有接口方法都委托给Provider处理
    /// • 用户无需手动实现任何接口方法
    ///
    /// 【使用场景】
    /// • 快速原型开发
    /// • 标准场景的Operation支持
    /// • 不需要自定义逻辑的简单应用
    ///
    /// 【优势】
    /// • 零配置：创建实例即可使用
    /// • 功能完整：自动获得所有核心服务
    /// • 扩展友好：继承后可添加自定义逻辑
    /// </summary>
    public class OpSupport_Examples : IOpSupporter
    {
        /// <summary>
        /// 获取操作支持提供者实例
        /// 【自动创建】实例化时自动创建完整的Provider
        /// </summary>
        public OpSupportProvider Provider => provider;

        /// <summary>
        /// 内部的操作支持提供者实例
        /// 【完整实现】包含所有核心服务和存储的完整实现
        /// </summary>
        private readonly OpSupportProvider provider = new OpSupportProvider();
    }

    /// <summary>
    /// 专用OpSupporter实现示例
    /// 【设计模式】通过继承OpSupport_Examples获得完整功能，然后添加特定逻辑
    /// 【优势】继承即获得所有基础服务，无需重新实现接口
    /// 【扩展方式】在子类中添加特定于业务领域的逻辑和配置
    /// </summary>
    /// <summary>
    /// 技能操作支持者
    /// 专为技能系统设计的OpSupporter实现
    /// </summary>
    public class SampleSkillOpSupporter : OpSupport_Examples
    {
        // 可以添加技能特定的逻辑
    }

    /// <summary>
    /// Buff操作支持者
    /// 专为高级Buff系统设计的OpSupporter实现
    /// </summary>
    public class SampleBuffOpSupporter : OpSupport_Examples
    {
        // 可以添加Buff特定的逻辑
    }

    /// <summary>
    /// 飞行物操作支持者
    /// 专为飞行物系统设计的OpSupporter实现
    /// </summary>
    public class SampleProjectileOpSupporter : OpSupport_Examples
    {
        // 可以添加飞行物特定的逻辑
    }

    /// <summary>
    /// 示例操作支持者
    /// 用于演示和测试的OpSupporter实现
    /// </summary>
    public class SampleOpSupporter : OpSupport_Examples
    {
        // 可以添加示例特定的逻辑
    }


    #endregion
    
    #region 标准Provider
    /// <summary>
    /// 操作支持标准提供者 - 完整的即插即用实现
    /// 【核心定位】ES框架的"万能提供者"，提供全套Operation支持服务
    ///
    /// 【设计理念】
    /// • 通用提供者：不带选择的全盘接受所有Operation请求
    /// • 即插即用：创建实例即可获得完整功能
    /// • 零配置：自动初始化所有必要的组件和服务
    ///
    /// 【核心功能】
    /// • 上下文池管理：维护运行时上下文环境
    /// • 缓存池管理：提供高效的数据缓存服务
    /// • 委托存储管理：处理Operation的委托任务
    /// • 缓冲存储管理：处理Operation的缓冲任务
    ///
    /// 【使用方式】
    /// • 直接实例化：new OpSupportProvider()
    /// • 作为Provider属性：通过IOpSupporter.Provider访问
    /// • 继承扩展：在子类中添加特定逻辑
    ///
    /// 【适用场景】
    /// • 通用Operation支持环境
    /// • 快速原型开发
    /// • 标准业务场景
    /// • 测试和验证环境
    /// </summary>
    public class OpSupportProvider : IOpSupporter
    {
        #region 构造函数
        /// <summary>
        /// 默认构造函数，初始化所有数据池
        /// </summary>
        public OpSupportProvider()
        {
            contextPool = new ContextPool();
            cacherPool = new CacherPool();
        }
        #endregion

        #region 核心数据池
        /// <summary>上下文池 - 提供运行时上下文环境支持</summary>
        public ContextPool contextPool;
        
        /// <summary>缓存池 - 提供运行时数据缓存支持</summary>
        public CacherPool cacherPool;
        
        /// <summary>委托任务存储 - 管理Operation的委托执行</summary>
        public SafeDictionary<IOperation, DeleAndCount> storeForDelegate = new SafeDictionary<IOperation, DeleAndCount>();
        
        /// <summary>缓冲任务存储 - 管理Operation的缓冲处理</summary>
        public SafeKeyGroup<OutputOperationBufferFloat_TargetAndDirectInput
        <ESRuntimeTarget, IOpSupporter, ESRuntimeOpSupport_ValueEntryFloatOperation>
        , BufferOperationFloat> storeForBuffer = new();
        #endregion

        #region IOpStore接口实现
        /// <summary>
        /// 获取委托任务存储字典
        /// </summary>
        /// <param name="flag">委托操作标志位（当前实现忽略标志位）</param>
        /// <returns>委托任务的安全字典存储</returns>
        public SafeDictionary<IOperation, DeleAndCount> GetFromOpStore(OutputOperationDelegateFlag flag = OutputOperationDelegateFlag.Default)
        {
            return storeForDelegate;
        }

        /// <summary>
        /// 获取缓冲任务存储键组
        /// </summary>
        /// <param name="flag">缓冲操作标志位（当前实现忽略标志位）</param>
        /// <returns>缓冲任务的安全键组存储</returns>
        public SafeKeyGroup<OutputOperationBufferFloat_TargetAndDirectInput<ESRuntimeTarget, IOpSupporter, ESRuntimeOpSupport_ValueEntryFloatOperation>, BufferOperationFloat> GetFromOpStore(OutputOperationBufferFlag flag = OutputOperationBufferFlag.Default)
        {
            return storeForBuffer;
        }

        #endregion

        #region 属性访问器
        /// <summary>获取上下文池实例</summary>
        public ContextPool Context => contextPool;
        
        /// <summary>获取缓存池实例</summary>
        public CacherPool Cacher => cacherPool;

        /// <summary>
        /// 获取操作支持提供者（返回自身）
        /// 【设计解释】OpSupportProvider本身就是完整的提供者实现
        /// 【使用意义】通过此属性，外部代码可以访问Provider的所有服务
        /// 【实现方式】返回this，因为类本身就实现了所有Provider功能
        /// </summary>
        public OpSupportProvider Provider => this;
        #endregion
    }
    #endregion

    #region 重写标志位定义
    /// <summary>
    /// 委托操作重载标志位枚举
    /// 【注意】此标志位仅用于方法重载区分，无实际业务含义
    /// 用于区分IOpStoreDictionary接口的重载方法
    /// </summary>
    public enum OutputOperationDelegateFlag
    {
        Default     // 默认重载标识
    }

    /// <summary>
    /// 缓冲操作重载标志位枚举
    /// 【注意】此标志位仅用于方法重载区分，无实际业务含义
    /// 用于区分IOpStoreKeyGroup接口的重载方法
    /// </summary>
    public enum OutputOperationBufferFlag
    {
        Default     // 默认重载标识
    }
    #endregion

    #region 基础类型定义
    /// <summary>
    /// 委托和计数容器 - 用于管理操作委托的执行计数
    /// </summary>
    public class DeleAndCount
    {
        /// <summary>委托对象</summary>
        public System.Action dele;
        /// <summary>执行计数</summary>
        public int count;
    }

    /// <summary>
    /// 缓冲区操作抽象基类
    /// </summary>
    public abstract class BufferOperationAbstract
    {
        /// <summary>
        /// 尝试自动推送到对象池
        /// </summary>
        public abstract void TryAutoPushedToPool();
    }

    /// <summary>
    /// 缓冲区数据源抽象基类
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    public abstract class BufferDataSource<T>
    {
        /// <summary>数据值</summary>
        public T value;
    }

    /// <summary>
    /// 缓冲区操作Float实现
    /// </summary>
    public class BufferOperationFloat : BufferOperationAbstract
    {
        /// <summary>
        /// 尝试自动推送到对象池
        /// </summary>
        public override void TryAutoPushedToPool()
        {
            // 实现对象池回收逻辑
        }
    }

    /// <summary>
    /// 缓冲区数据源Float实现
    /// </summary>
    public class BufferDataSourceFloat : BufferDataSource<float>
    {
        
    }

    /// <summary>
    /// 输出操作缓冲区Float - 目标和直接输入
    /// </summary>
    /// <typeparam name="Target">目标类型</typeparam>
    /// <typeparam name="Logic">逻辑类型</typeparam>
    /// <typeparam name="ValueEntryOp">值入口操作类型</typeparam>
    public abstract class OutputOperationBufferFloat_TargetAndDirectInput<Target, Logic, ValueEntryOp> :
        OutputOperationBuffer<Target, Logic, float, BufferOperationFloat, BufferDataSourceFloat,
        OutputOperationBufferFloat_TargetAndDirectInput<Target, Logic, ValueEntryOp>>
        where Logic : IOpStoreKeyGroup<OutputOperationBufferFloat_TargetAndDirectInput<Target, Logic, ValueEntryOp>, BufferOperationFloat, OutputOperationBufferFlag>
        where ValueEntryOp : IHandleValueOperation<Target, Logic, float, FloatValueEntryType>
    {

    }

    /// <summary>
    /// 值入口操作Float运行时支持
    /// </summary>
    public interface ESRuntimeOpSupport_ValueEntryFloatOperation : IHandleValueOperation<ESRuntimeTarget, IOpSupporter, float, FloatValueEntryType>
    {

    }

    /// <summary>
    /// Float值入口类型枚举
    /// </summary>
    public enum FloatValueEntryType
    {
        /// <summary>直接设置</summary>
        Set,
        /// <summary>增加</summary>
        Add,
        /// <summary>减少</summary>
        Subtract,
        /// <summary>乘法</summary>
        Multiply,
        /// <summary>除法</summary>
        Divide,
        /// <summary>百分比增加</summary>
        AddPercent,
        /// <summary>百分比减少</summary>
        SubtractPercent
    }
}

    #endregion
