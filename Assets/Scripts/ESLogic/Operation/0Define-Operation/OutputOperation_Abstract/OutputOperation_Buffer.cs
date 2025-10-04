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
    public abstract class OutputOperationBuffer<On, From, With, ValueType, Buffer, BufferSource, This> : IOutputOperation<On, From, With>
        where With : ICacheSafeKeyGroupForOutputOpeation<This, Buffer, OutputOpeationBufferFlag>
        where Buffer : BufferOperationAbstract, new()
        where BufferSource : BufferDataSource<ValueType>
        where This : OutputOperationBuffer<On, From, With, ValueType, Buffer, BufferSource, This>
    {
        public abstract void TryOperation(On on, From from, With with);
        public abstract void TryCancel(On on, From from, With with);
        public Buffer GetBufferOnEnableExpand(On on, From from, With with)
        {
            var use = MakeTheOpeation(on, from, with);
            with.GetCache(OutputOpeationBufferFlag.flag).TryAdd(this as This, use);
            return use;
        }
        public Buffer GetBufferOnDisableExpand(On on, From from, With with)
        {
            var cacher = with.GetCache(OutputOpeationBufferFlag.flag);
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
        protected abstract Buffer MakeTheOpeation(On on, From from, With with);
        public abstract void TryUpdateTheBuffer(On on, From from, With with, Buffer buffer);
        public abstract void TryStopTheBuffer(On on, From from, With with, Buffer buffer);
    }

    /*演示*/
    #region 演示
    //数值导向+直接输入(这种可以绕过数值传递直接Update)
    public abstract class OutputOperationBuffer_TargetAndDirectInput<On, From, With, ValueType, Buffer, BufferSource, Target, This> :
        OutputOperationBuffer<On, From, With, ValueType, Buffer, BufferSource, This>
        where With : ICacheSafeKeyGroupForOutputOpeation<This, Buffer, OutputOpeationBufferFlag>
        where Buffer : BufferOperation<ValueType, BufferSource, Buffer>, new()
        where BufferSource : BufferDataSource<ValueType>
        where Target : ITargetOperation<On, From, With, ValueType, ValueType, OperationOptionsForFloat>
        where This : OutputOperationBuffer_TargetAndDirectInput<On, From, With, ValueType, Buffer, BufferSource, Target, This>
    {
        [LabelText("输入缓冲源")]
        public BufferSource bufferSource;
        [LabelText("数值导向"), SerializeReference]
        public Target target;
        protected override Buffer MakeTheOpeation(On on, From from, With with)
        {
            var buffer = BufferOperation<ValueType, BufferSource, Buffer>.GetOne();
            buffer.timeHasGo = 0;
            // buffer.source = bufferSource;
            return buffer;
        }
        public override void TryUpdateTheBuffer(On on, From from, With with, Buffer buffer)
        {
            if (target != null)
            {
                target.TargetOpeation(on, from, with,bufferSource.EvaluateThisFrame(ref buffer.timeHasGo), OperationOptionsForFloat.Add);
                if (buffer.timeHasGo >= bufferSource.allTime)
                {
                    TryStopTheBuffer(on, from, with,buffer);//提前退出
                }
            }
        }
        public override void TryStopTheBuffer(On on, From from, With with, Buffer buffer)
        {
            Debug.Log("BU4");
            var cacher = with.GetCache(OutputOpeationBufferFlag.flag);
            if (cacher.Groups.TryGetValue(this as This, out var use))
            {
                if (target != null)
                {
                    target.TargetOpeation(on, from, with, bufferSource.EvaluateToEndFrame(ref buffer.timeHasGo), OperationOptionsForFloat.Add);
                    use.TryRemove(buffer);
                } 
                foreach(var i in use)
                {
                    i.TryAutoPushedToPool();
                }
            }
           
        }
    }

    //浮点缓冲 直接指向
    public abstract class OutputOperationBufferFloat_TargetAndDirectInput<On, From, With, Target> : OutputOperationBuffer_TargetAndDirectInput<On, From, With, float, BufferOperationFloat, BufferDataFloatSource, Target, OutputOperationBufferFloat_TargetAndDirectInput<On, From, With, Target>>
          where With : ICacheSafeKeyGroupForOutputOpeation<OutputOperationBufferFloat_TargetAndDirectInput<On, From, With, Target>, BufferOperationFloat, OutputOpeationBufferFlag>
         where Target : ITargetOperation<On, From, With, float, float, OperationOptionsForFloat>
    {

    }


    //EEB格式
 /*   [Serializable, TypeRegistryItem("缓冲输出-浮点数-导向-直接输入-EEB")]
    public class OutputOperationBufferrFloatEEB_TargetAndDirectInput : OutputOperationBufferFloat_TargetAndDirectInput<Entity, Entity, EntityState_Buff, ITargetOperationFloatEEB>, IOutputOperationEEB
    {
        public override void TryOperation(Entity on, Entity from, EntityState_Buff with)
        {
            GetBufferOnEnableExpand(on, from, with);
        }
        public override void TryCancel(Entity on, Entity from, EntityState_Buff with)
        {
            GetBufferOnDisableExpand(on, from, with);
        }

    }
*/
    //EEB指向

    #endregion
}
