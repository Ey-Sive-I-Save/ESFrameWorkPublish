using UnityEngine;

namespace ES
{
    #region 标准接口

    /// <summary>
    /// Operation 运行服务接口。
    /// 它不是技能逻辑本体，而是 Operation 执行时访问 ContextPool、CacherPool 和运行期存储的服务入口。
    /// </summary>
    public interface IOperationRuntimeServices : IOpStoreDictionary
        <IOperation, DeleAndCount, OutputOperationDelegateFlag>,
        IOpStoreKeyGroup
        <OutputOperationBufferFloat_TargetAndDirectInput
        <ESRuntimeTargetPack, IOperationRuntimeServices, ESRuntimeOpSupport_ValueEntryFloatOperation>,
        BufferOperationFloat, OutputOperationBufferFlag>
    {
        public ContextPool Context { get { return Provider.contextPool; } }

        public CacherPool Cacher { get { return Provider.cacherPool; } }

        SafeDictionary<IOperation, DeleAndCount> IOpStoreDictionary
            <IOperation, DeleAndCount, OutputOperationDelegateFlag>.GetFromOpStore(OutputOperationDelegateFlag flag)
        {
            return Provider.storeForDelegate;
        }

        SafeKeyGroup<OutputOperationBufferFloat_TargetAndDirectInput<ESRuntimeTargetPack, IOperationRuntimeServices, ESRuntimeOpSupport_ValueEntryFloatOperation>, BufferOperationFloat>
            IOpStoreKeyGroup
            <OutputOperationBufferFloat_TargetAndDirectInput
            <ESRuntimeTargetPack, IOperationRuntimeServices, ESRuntimeOpSupport_ValueEntryFloatOperation>,
            BufferOperationFloat, OutputOperationBufferFlag>.GetFromOpStore(OutputOperationBufferFlag flag)
        {
            return Provider.storeForBuffer;
        }

        public OpSupportProvider Provider { get; }
    }

    /// <summary>
    /// 历史兼容名。新代码优先使用 IOperationRuntimeServices。
    /// </summary>
    public interface IOpSupporter : IOperationRuntimeServices
    {
    }

    #endregion

    #region 示例实现

    public class OpSupport_Examples : IOperationRuntimeServices
    {
        public OpSupportProvider Provider => provider;

        private readonly OpSupportProvider provider = new OpSupportProvider();
    }

    public class SampleSkillOpSupporter : OpSupport_Examples
    {
    }

    public class SampleBuffOpSupporter : OpSupport_Examples
    {
    }

    public class SampleProjectileOpSupporter : OpSupport_Examples
    {
    }

    public class SampleOpSupporter : OpSupport_Examples
    {
    }

    #endregion

    #region 标准 Provider

    /// <summary>
    /// 默认 Operation 运行服务 Provider，直接持有上下文池、缓存池、委托存储和缓冲存储。
    /// </summary>
    public class OpSupportProvider : IOperationRuntimeServices
    {
        public OpSupportProvider()
        {
            contextPool = new ContextPool();
            cacherPool = new CacherPool();
        }

        public ContextPool contextPool;

        public CacherPool cacherPool;

        public SafeDictionary<IOperation, DeleAndCount> storeForDelegate = new SafeDictionary<IOperation, DeleAndCount>();

        public SafeKeyGroup<OutputOperationBufferFloat_TargetAndDirectInput
            <ESRuntimeTargetPack, IOperationRuntimeServices, ESRuntimeOpSupport_ValueEntryFloatOperation>, BufferOperationFloat> storeForBuffer = new();

        public SafeDictionary<IOperation, DeleAndCount> GetFromOpStore(OutputOperationDelegateFlag flag = OutputOperationDelegateFlag.Default)
        {
            return storeForDelegate;
        }

        public SafeKeyGroup<OutputOperationBufferFloat_TargetAndDirectInput<ESRuntimeTargetPack, IOperationRuntimeServices, ESRuntimeOpSupport_ValueEntryFloatOperation>, BufferOperationFloat> GetFromOpStore(OutputOperationBufferFlag flag = OutputOperationBufferFlag.Default)
        {
            return storeForBuffer;
        }

        public ContextPool Context => contextPool;

        public CacherPool Cacher => cacherPool;

        public OpSupportProvider Provider => this;

        public EntityState_Skill CurrentSkillState { get; private set; }

        public Entity CurrentEntity => CurrentSkillState != null ? CurrentSkillState.HostEntity : null;

        public void SetCurrentSkillState(EntityState_Skill state)
        {
            CurrentSkillState = state;
        }
    }

    #endregion

    #region 重载标识

    public enum OutputOperationDelegateFlag
    {
        Default
    }

    public enum OutputOperationBufferFlag
    {
        Default
    }

    #endregion

    #region 基础类型

    public class DeleAndCount
    {
        public System.Action dele;
        public int count;
    }

    public abstract class BufferOperationAbstract
    {
        public abstract void TryAutoPushedToPool();
    }

    public abstract class BufferDataSource<T>
    {
        public T value;
    }

    public class BufferOperationFloat : BufferOperationAbstract
    {
        public override void TryAutoPushedToPool()
        {
        }
    }

    public class BufferDataSourceFloat : BufferDataSource<float>
    {
    }

    public abstract class OutputOperationBufferFloat_TargetAndDirectInput<Target, Logic, ValueEntryOp> :
        OutputOperationBuffer<Target, Logic, float, BufferOperationFloat, BufferDataSourceFloat,
        OutputOperationBufferFloat_TargetAndDirectInput<Target, Logic, ValueEntryOp>>
        where Logic : IOpStoreKeyGroup<OutputOperationBufferFloat_TargetAndDirectInput<Target, Logic, ValueEntryOp>, BufferOperationFloat, OutputOperationBufferFlag>
        where ValueEntryOp : IHandleValueOperation<Target, Logic, float, FloatValueEntryType>
    {
    }

    public interface ESRuntimeOpSupport_ValueEntryFloatOperation : IHandleValueOperation<ESRuntimeTargetPack, IOperationRuntimeServices, float, FloatValueEntryType>
    {
    }

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
