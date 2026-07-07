using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{
    #region 标准接口

    /// <summary>
    /// Operation 的运行环境接口。
    /// 负责提供上下文、缓存池，以及 Operation 运行期间需要的委托/缓冲存储。
    /// </summary>
    public interface IOpSupporter : IOpStoreDictionary
    <IOperation, DeleAndCount, OutputOperationDelegateFlag>,
       IOpStoreKeyGroup
       <ES.OutputOperationBufferFloat_TargetAndDirectInput
       <ES.ESRuntimeTarget, ES.IOpSupporter, ES.ESRuntimeOpSupport_ValueEntryFloatOperation>,
        ES.BufferOperationFloat, ES.OutputOperationBufferFlag>
    {
        /// <summary>运行上下文池。</summary>
        public ContextPool Context { get { return Provider.contextPool; } }

        /// <summary>运行缓存池。</summary>
        public CacherPool Cacher { get { return Provider.cacherPool; } }

        SafeDictionary<IOperation, DeleAndCount> IOpStoreDictionary
        <IOperation, DeleAndCount, OutputOperationDelegateFlag>.GetFromOpStore(OutputOperationDelegateFlag flag)
        {
            return Provider.storeForDelegate;
        }

        SafeKeyGroup<OutputOperationBufferFloat_TargetAndDirectInput<ESRuntimeTarget, IOpSupporter, ESRuntimeOpSupport_ValueEntryFloatOperation>, BufferOperationFloat>
        IOpStoreKeyGroup
        <ES.OutputOperationBufferFloat_TargetAndDirectInput
        <ES.ESRuntimeTarget, ES.IOpSupporter, ES.ESRuntimeOpSupport_ValueEntryFloatOperation>,
        ES.BufferOperationFloat, ES.OutputOperationBufferFlag>.GetFromOpStore(OutputOperationBufferFlag flag)
        {
            return Provider.storeForBuffer;
        }

        /// <summary>实际提供各类运行服务的对象。</summary>
        public OpSupportProvider Provider { get; }
    }

    #endregion

    #region 示例实现

    /// <summary>
    /// 最小可用的 Operation 支持者示例。
    /// 通过内部 Provider 获得上下文、缓存和存储能力。
    /// </summary>
    public class OpSupport_Examples : IOpSupporter
    {
        public OpSupportProvider Provider => provider;

        private readonly OpSupportProvider provider = new OpSupportProvider();
    }

    /// <summary>技能场景的 Operation 支持者示例。</summary>
    public class SampleSkillOpSupporter : OpSupport_Examples
    {
        // 可在子类中添加技能相关上下文或配置。
    }

    /// <summary>Buff 场景的 Operation 支持者示例。</summary>
    public class SampleBuffOpSupporter : OpSupport_Examples
    {
        // 可在子类中添加 Buff 相关上下文或配置。
    }

    /// <summary>飞行物场景的 Operation 支持者示例。</summary>
    public class SampleProjectileOpSupporter : OpSupport_Examples
    {
        // 可在子类中添加飞行物相关上下文或配置。
    }

    /// <summary>通用测试用 Operation 支持者示例。</summary>
    public class SampleOpSupporter : OpSupport_Examples
    {
        // 可在子类中添加测试或演示逻辑。
    }

    #endregion

    #region 标准 Provider

    /// <summary>
    /// 默认 Operation 支持服务实现。
    /// 直接持有上下文池、缓存池、委托存储和缓冲存储。
    /// </summary>
    public class OpSupportProvider : IOpSupporter
    {
        #region 构造

        public OpSupportProvider()
        {
            contextPool = new ContextPool();
            cacherPool = new CacherPool();
        }

        #endregion

        #region 核心数据

        /// <summary>运行上下文池。</summary>
        public ContextPool contextPool;

        /// <summary>运行缓存池。</summary>
        public CacherPool cacherPool;

        /// <summary>委托类 Operation 的运行存储。</summary>
        public SafeDictionary<IOperation, DeleAndCount> storeForDelegate = new SafeDictionary<IOperation, DeleAndCount>();

        /// <summary>缓冲类 Operation 的运行存储。</summary>
        public SafeKeyGroup<OutputOperationBufferFloat_TargetAndDirectInput
        <ESRuntimeTarget, IOpSupporter, ESRuntimeOpSupport_ValueEntryFloatOperation>
        , BufferOperationFloat> storeForBuffer = new();

        #endregion

        #region IOpStore 实现

        public SafeDictionary<IOperation, DeleAndCount> GetFromOpStore(OutputOperationDelegateFlag flag = OutputOperationDelegateFlag.Default)
        {
            return storeForDelegate;
        }

        public SafeKeyGroup<OutputOperationBufferFloat_TargetAndDirectInput<ESRuntimeTarget, IOpSupporter, ESRuntimeOpSupport_ValueEntryFloatOperation>, BufferOperationFloat> GetFromOpStore(OutputOperationBufferFlag flag = OutputOperationBufferFlag.Default)
        {
            return storeForBuffer;
        }

        #endregion

        #region 属性

        public ContextPool Context => contextPool;

        public CacherPool Cacher => cacherPool;

        public OpSupportProvider Provider => this;

        #endregion
    }

    #endregion

    #region 重载标识

    /// <summary>用于区分委托存储重载的标识。</summary>
    public enum OutputOperationDelegateFlag
    {
        Default
    }

    /// <summary>用于区分缓冲存储重载的标识。</summary>
    public enum OutputOperationBufferFlag
    {
        Default
    }

    #endregion

    #region 基础类型

    /// <summary>委托与引用计数容器。</summary>
    public class DeleAndCount
    {
        public System.Action dele;
        public int count;
    }

    /// <summary>缓冲对象基类。</summary>
    public abstract class BufferOperationAbstract
    {
        public abstract void TryAutoPushedToPool();
    }

    /// <summary>缓冲数据源基类。</summary>
    public abstract class BufferDataSource<T>
    {
        public T value;
    }

    /// <summary>Float 缓冲对象。</summary>
    public class BufferOperationFloat : BufferOperationAbstract
    {
        public override void TryAutoPushedToPool()
        {
            // 预留对象池回收入口。
        }
    }

    /// <summary>Float 缓冲数据源。</summary>
    public class BufferDataSourceFloat : BufferDataSource<float>
    {
    }

    /// <summary>
    /// 面向运行目标和直接数值输入的 Float 缓冲 Operation。
    /// </summary>
    public abstract class OutputOperationBufferFloat_TargetAndDirectInput<Target, Logic, ValueEntryOp> :
        OutputOperationBuffer<Target, Logic, float, BufferOperationFloat, BufferDataSourceFloat,
        OutputOperationBufferFloat_TargetAndDirectInput<Target, Logic, ValueEntryOp>>
        where Logic : IOpStoreKeyGroup<OutputOperationBufferFloat_TargetAndDirectInput<Target, Logic, ValueEntryOp>, BufferOperationFloat, OutputOperationBufferFlag>
        where ValueEntryOp : IHandleValueOperation<Target, Logic, float, FloatValueEntryType>
    {
    }

    /// <summary>运行时 Float 值入口 Operation。</summary>
    public interface ESRuntimeOpSupport_ValueEntryFloatOperation : IHandleValueOperation<ESRuntimeTarget, IOpSupporter, float, FloatValueEntryType>
    {
    }

    /// <summary>Float 值写入方式。</summary>
    public enum FloatValueEntryType
    {
        Set,
        Add,
        Subtract,
        Multiply,
        Divide,
        AddPercent,
        SubtractPercent
    }

    #endregion
}
