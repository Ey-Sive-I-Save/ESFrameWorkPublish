using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// ES框架 - 输出操作基类 (ESOutputOp)
    /// 【ESRuntimeTarget + IOpSupporter 专用实现】
    ///
    /// 【设计意图】
    /// 为基于ESRuntimeTarget和IOpSupporter的操作提供统一的基类实现
    /// 简化具体操作类的开发，提供一致的接口和生命周期管理
    ///
    /// 【核心特性】
    /// • 抽象基类：提供默认实现，子类按需重写
    /// • 序列化支持：支持Unity序列化系统，可在Inspector中配置
    /// • 虚方法设计：StartOperation和StopOperation均为虚方法
    /// • 空实现默认：提供安全的默认行为（空操作）
    ///
    /// 【使用方式】
    /// 继承此类并重写所需的方法：
    /// <code>
    /// public class MyOutputOp : ESOutputOp
    /// {
    ///     public override void StartOperation(ESRuntimeTarget target, IOpSupporter logic)
    ///     {
    ///         // 实现具体的操作逻辑
    ///     }
    /// }
    /// </code>
    ///
    /// 【重要说明】
    /// • 不支持回滚操作，仅提供单纯的启动和停止机制
    /// • StopOperation不会撤销StartOperation产生的效果
    /// • 适合需要明确生命周期管理的操作场景
    /// </summary>
    [Serializable]
    public abstract class ESOutputOp 
    {
        public bool Enabled=true;
        [LabelText("必须触发停止")]
        public bool MustTriggerStop=false;
        /// <summary>
        /// 开始执行输出操作
        /// 【生命周期起点】
        ///
        /// 默认实现为空操作，子类可重写以实现具体逻辑:
        /// • 验证目标和上下文的有效性
        /// • 初始化操作状态
        /// • 执行具体的业务逻辑
        /// • 启动相关的协程或异步操作
        ///
        /// 【重写建议】
        /// 子类重写时应考虑:
        /// - 参数验证：检查target和logic的有效性
        /// - 状态管理：设置操作的初始状态
        /// - 资源分配：申请所需的临时资源
        /// - 异常处理：妥善处理执行过程中的异常
        /// </summary>
        /// <param name="target">操作目标，ESRuntimeTarget实例</param>
        /// <param name="logic">操作上下文，IOpSupporter实例</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void _TryStartOp(ESRuntimeTarget target, IOpSupporter logic)
        {
            if(Enabled){
                StartOperation(target, logic);
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void _TryStopOp(ESRuntimeTarget target, IOpSupporter logic)
        {
            if(Enabled){
                StopOperation(target, logic);
            }
        }
        
        protected abstract void StartOperation(ESRuntimeTarget target, IOpSupporter logic);

        /// <summary>
        /// 停止输出操作
        /// 【单纯退出机制 - 无回滚支持】
        ///
        /// 默认实现为空操作，子类可重写以实现清理逻辑:
        /// • 中断正在进行的操作
        /// • 释放占用的临时资源
        /// • 清理操作状态
        /// • 停止相关的协程或异步操作
        ///
        /// 【重要限制】
        /// 此方法不提供回滚功能，已产生的效果不会被撤销
        /// 仅用于停止正在进行的操作和执行必要的清理工作
        ///
        /// 【重写建议】
        /// 子类重写时应考虑:
        /// - 安全停止：确保操作能安全中断
        /// - 资源清理：释放所有临时资源
        /// - 状态重置：清理操作相关的状态数据
        /// - 通知机制：通知相关组件操作已停止
        /// </summary>
        /// <param name="target">操作目标，ESRuntimeTarget实例</param>
        /// <param name="logic">操作上下文，IOpSupporter实例</param>
        protected virtual void StopOperation(ESRuntimeTarget target, IOpSupporter logic){}
    }
}



