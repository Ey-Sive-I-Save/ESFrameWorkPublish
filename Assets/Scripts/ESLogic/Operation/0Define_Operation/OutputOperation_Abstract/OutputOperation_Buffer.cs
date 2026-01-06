using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;


namespace ES
{
    public class OutputOpeationBufferFlag : OverLoadFlag<OutputOpeationBufferFlag>
    {

    }
    public abstract class OutputOperationBuffer<Target,Logic, ValueType, Buffer, BufferSource, This> : IOutputOperation<Target,Logic>
        where Logic : IOpStoreSafeKeyGroupForOutputOpeation<This, Buffer, OutputOpeationBufferFlag>
        where Buffer : BufferOperationAbstract, new()
        where BufferSource : BufferDataSource<ValueType>
        where This : OutputOperationBuffer<Target,Logic, ValueType, Buffer, BufferSource, This>
    {
        public abstract void TryOperation(Target target,Logic logic);
        public abstract void TryCancel(Target target,Logic logic);
        public Buffer GetBufferOnEnableExpand(Target target,Logic logic)
        {
            var use = MakeTheOpeation(target, logic);
            logic.GetFromOpStore(OutputOpeationBufferFlag.flag).Add(this as This, use);
            return use;
        }
        public Buffer GetBufferOnDisableExpand(Target target,Logic logic)
        {
            var cacher = logic.GetFromOpStore(OutputOpeationBufferFlag.flag);
            if (cacher.Groups.TryGetValue(this as This, out var use))
            {
                cacher.Groups.Remove(this as This);
                foreach(var i in use)
                {
                    i.TryAutoPushedToPool();
                }
                return use as Buffer;
            }
            return default;
        }
        protected abstract Buffer MakeTheOpeation(Target target,Logic logic);
        public abstract void TryUpdateTheBuffer(Target target,Logic logic, Buffer buffer);
        public abstract void TryStopTheBuffer(Target target,Logic logic, Buffer buffer);
    }

    /*演示*/
    #region 演示
    //数值导向+直接输入(这种可以绕过数值传递直接Update)
    public abstract class OutputOperationBuffer_TargetAndDirectInput<Target,Logic, ValueType, Buffer, BufferSource,ValueEntryOp, This> :
        OutputOperationBuffer<Target,Logic, ValueType, Buffer, BufferSource, This>
        where Logic : IOpStoreSafeKeyGroupForOutputOpeation<This, Buffer, OutputOpeationBufferFlag>
        where Buffer : BufferOperation<ValueType, BufferSource, Buffer>, new()
        where BufferSource : BufferDataSource<ValueType>
        where ValueEntryOp : IValueEntryOperation<Target,Logic, ValueType, ValueType, OperationOptionsForFloat>
        where This : OutputOperationBuffer_TargetAndDirectInput<Target,Logic, ValueType, Buffer, BufferSource,ValueEntryOp, This>
    {
        [LabelText("输入缓冲源")]
        public BufferSource bufferSource;
        [LabelText("数值导向"), SerializeReference]
        public ValueEntryOp valueEntryOp;
        protected override Buffer MakeTheOpeation(Target target,Logic logic)
        {
            var buffer = BufferOperation<ValueType, BufferSource, Buffer>.GetOne();
            buffer.timeHasGo = 0;
            // buffer.source = bufferSource;
            return buffer;
        }
        public override void TryUpdateTheBuffer(Target target,Logic logic, Buffer buffer)
        {
            if (valueEntryOp != null)
            {
                valueEntryOp.HandleValueEntryOpeation(target, logic,bufferSource.EvaluateThisFrame(ref buffer.timeHasGo), OperationOptionsForFloat.Add);
                if (buffer.timeHasGo >= bufferSource.allTime)
                {
                    TryStopTheBuffer(target, logic,buffer);//提前退出
                }
            }
        }
        public override void TryStopTheBuffer(Target target,Logic logic, Buffer buffer)
        {
            var cacher = logic.GetFromOpStore(OutputOpeationBufferFlag.flag);
            if (cacher.Groups.TryGetValue(this as This, out var use))
            {
                if (valueEntryOp != null)
                {
                    valueEntryOp.HandleValueEntryOpeation(target, logic, bufferSource.EvaluateToEndFrame(ref buffer.timeHasGo), OperationOptionsForFloat.Add);
                    use.Remove(buffer);
                } 
                foreach(var i in use)
                {
                    i.TryAutoPushedToPool();
                }
            }
           
        }
    }

    //浮点缓冲 直接指向
    public abstract class 
    OutputOperationBufferFloat_TargetAndDirectInput<Target,Logic,ValueEntryOp> : 
    OutputOperationBuffer_TargetAndDirectInput
    <Target,Logic, float, BufferOperationFloat, BufferDataFloatSource,ValueEntryOp
    , OutputOperationBufferFloat_TargetAndDirectInput<Target,Logic,ValueEntryOp>>
          where Logic : IOpStoreSafeKeyGroupForOutputOpeation<OutputOperationBufferFloat_TargetAndDirectInput<Target,Logic,ValueEntryOp>, BufferOperationFloat, OutputOpeationBufferFlag>
         where ValueEntryOp : IValueEntryOperation<Target,Logic, float, float, OperationOptionsForFloat>
    {

    }


    //EEB格式
 /*   [Serializable, TypeRegistryItem("缓冲输出-浮点数-导向-直接输入-EEB")]
    public class OutputOperationBufferrFloatEEB_TargetAndDirectInput : OutputOperationBufferFloat_TargetAndDirectInput<Entity, Entity, EntityState_Buff, ITargetOperationFloatEEB>, IOutputOperationEEB
    {
        public override void TryOperation(Entity on, Entity from, EntityState_Buff with)
        {
            GetBufferOnEnableExpand(target, logic);
        }
        public override void TryCancel(Entity on, Entity from, EntityState_Buff with)
        {
            GetBufferOnDisableExpand(target, logic);
        }

    }
*/
    //EEB指向

    #endregion
}
