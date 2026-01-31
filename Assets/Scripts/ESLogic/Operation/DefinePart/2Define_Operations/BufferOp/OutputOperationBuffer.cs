using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// ES框架 - 缓冲区操作抽象基类 (持续效果核心机制)
    /// 【持续效果的核心机制】
    ///
    /// 【核心概念】
    /// 为需要持续执行的操作提供缓冲区支持，实现效果的生命周期管理
    /// 支持自动创建、更新、停止和回收的完整生命周期
    ///
    /// 【设计优势】
    /// • 生命周期管理: 自动处理缓冲区的创建和销毁
    /// • 内存安全: 支持对象池回收，避免内存泄漏
    /// • 类型安全: 泛型约束保证类型一致性
    /// • 扩展灵活: 抽象方法支持自定义逻辑
    ///
    /// 【适用场景】
    /// • Buff持续效果: 中毒、燃烧、治疗等持续伤害/治疗
    /// • 技能持续状态: 加速、减速、护盾等状态效果
    /// • 飞行物轨迹: 持续移动、旋转等轨迹计算
    /// </summary>
    /// <typeparam name="Target">操作目标类型</typeparam>
    /// <typeparam name="Logic">逻辑上下文类型</typeparam>
    /// <typeparam name="ValueType">数值类型</typeparam>
    /// <typeparam name="Buffer">缓冲区类型</typeparam>
    /// <typeparam name="BufferSource">缓冲区数据源类型</typeparam>
    /// <typeparam name="This">自身类型，用于CRTP模式</typeparam>
    public abstract class OutputOperationBuffer<Target, Logic, ValueType, Buffer, BufferSource, This> 
        where Logic : IOpStoreKeyGroup<This, Buffer, OutputOperationBufferFlag>
        where Buffer : BufferOperationAbstract, new()
        where BufferSource : BufferDataSource<ValueType>
        where This : OutputOperationBuffer<Target, Logic, ValueType, Buffer, BufferSource, This>
    {
        /// <summary>
        /// 尝试执行缓冲区操作
        /// </summary>
        public abstract void StartOperation(Target target, Logic logic);

        /// <summary>
        /// 尝试取消缓冲区操作
        /// </summary>
        public abstract void StopOperation(Target target, Logic logic);

        /// <summary>
        /// 启用时获取缓冲区（扩展方法）
        /// </summary>
        public Buffer GetBufferOnEnableExpand(Target target, Logic logic)
        {
            var buffer = MakeTheOperation(target, logic);
            logic.GetFromOpStore(OutputOperationBufferFlag.Default).Add(this as This, buffer);
            return buffer;
        }

        /// <summary>
        /// 禁用时获取缓冲区（扩展方法）
        /// </summary>
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

        /// <summary>
        /// 创建缓冲区操作（抽象方法，由子类实现）
        /// </summary>
        protected abstract Buffer MakeTheOperation(Target target, Logic logic);

        /// <summary>
        /// 更新缓冲区（抽象方法，由子类实现）
        /// </summary>
        public abstract void TryUpdateTheBuffer(Target target, Logic logic, Buffer buffer);

        /// <summary>
        /// 停止缓冲区（抽象方法，由子类实现）
        /// </summary>
        public abstract void TryStopTheBuffer(Target target, Logic logic, Buffer buffer);
    }
}