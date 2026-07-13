using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// 持续效果类 Operation 的缓冲基类。
    /// 负责创建、保存、更新和停止缓冲对象。
    /// </summary>
    public abstract class OutputOperationBuffer<Target, Logic, ValueType, Buffer, BufferSource, This>
        where Logic : IOpStoreKeyGroup<This, Buffer, OutputOperationBufferFlag>
        where Buffer : BufferOperationAbstract, new()
        where BufferSource : BufferDataSource<ValueType>
        where This : OutputOperationBuffer<Target, Logic, ValueType, Buffer, BufferSource, This>
    {
        /// <summary>开始执行缓冲 Operation。</summary>
        public abstract void StartOperation(Target target, Logic logic);

        /// <summary>停止缓冲 Operation。</summary>
        public abstract void StopOperation(Target target, Logic logic);

        /// <summary>启用时创建缓冲并注册到 Logic 的存储中。</summary>
        public Buffer GetBufferOnEnableExpand(Target target, Logic logic)
        {
            var buffer = MakeTheOperation(target, logic);
            logic.GetFromOpStore(OutputOperationBufferFlag.Default).Add(this as This, buffer);
            return buffer;
        }

        /// <summary>禁用时取出缓冲，触发回收并从存储中移除。</summary>
        public Buffer GetBufferOnDisableExpand(Target target, Logic logic)
        {
            var cacher = logic.GetFromOpStore(OutputOperationBufferFlag.Default);
            if (cacher.Groups.TryGetValue(this as This, out var buffer))
            {
                cacher.Groups.Remove(this as This);
                foreach (var item in buffer)
                {
                    item.TryAutoPushedToPool();
                }
                return buffer as Buffer;
            }
            return default;
        }

        /// <summary>创建具体缓冲对象。</summary>
        protected abstract Buffer MakeTheOperation(Target target, Logic logic);

        /// <summary>更新已有缓冲对象。</summary>
        public abstract void TryUpdateTheBuffer(Target target, Logic logic, Buffer buffer);

        /// <summary>停止已有缓冲对象。</summary>
        public abstract void TryStopTheBuffer(Target target, Logic logic, Buffer buffer);
    }
}
