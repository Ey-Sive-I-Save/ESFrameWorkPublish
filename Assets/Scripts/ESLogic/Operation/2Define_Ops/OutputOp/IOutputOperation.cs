using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// ES框架 - 输出操作接口(Output Operation)
    /// 
    /// 【核心概念】
    /// 定义输出型Operation的基本行为和接口
    /// 支持技能效果输出、高级Buff效果输出、飞行物轨迹输出等
    /// 
    /// 【主要职责】
    /// • 执行输出逻辑
    /// • 管理输出状态
    /// • 提供输出数据访问
    /// 
    /// 【实现要求】
    /// 实现类需要继承MonoBehaviour并实现此接口
    /// </summary>
    public interface IOutputOperation : IOperation
    {
        /// <summary>
        /// 执行输出操作
        /// </summary>
        /// <param name="supporter">操作支持者，提供运行环境</param>
        void ExecuteOutput(IOpSupporter supporter);

        /// <summary>
        /// 获取输出状态
        /// </summary>
        /// <returns>当前输出状态</returns>
        OutputState GetOutputState();

        /// <summary>
        /// 停止输出操作
        /// </summary>
        void StopOutput();
    }

    /// <summary>
    /// 输出操作状态枚举
    /// </summary>
    public enum OutputState
    {
        Idle,       // 空闲状态
        Executing,  // 执行中
        Completed,  // 已完成
        Failed      // 失败
    }
}
