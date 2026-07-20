using System;

namespace ES
{
    #region Store Flags

    public enum OutputOperationDelegateFlag
    {
        Default
    }

    public enum OutputOperationBufferFlag
    {
        Default
    }

    #endregion

    #region Buffer Types

    public class DeleAndCount
    {
        public Action dele;
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

    public interface ESRuntimeOpSupport_ValueEntryFloatOperation : IHandleValueOperation<ESRuntimeTargetPack, ESOpSupport, float, FloatValueEntryType>
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
