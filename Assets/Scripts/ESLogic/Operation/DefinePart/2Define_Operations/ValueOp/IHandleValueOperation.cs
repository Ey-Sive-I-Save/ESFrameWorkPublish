using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// ES框架 - 值入口操作接口 (数值处理核心)
    /// 【数值处理的通用抽象】
    ///
    /// 【核心概念】
    /// 值通道导向操作，目的是把一个效果指定到特定的无解析值上
    /// 值一般有数值和引用两种，经常与其他内容配合使用
    ///
    /// 【设计优势】
    /// • 类型安全: 通过泛型约束保证编译时类型检查
    /// • 操作丰富: 支持加减乘除、百分比、限制等多种数值操作
    /// • 可撤销: 支持操作的取消和回滚
    /// • 组合友好: 易于与其他操作组合使用
    ///
    /// 【适用场景】
    /// • 属性修改: 生命值、魔法值、攻击力等数值调整
    /// • 状态计算: Buff效果、技能加成等数值运算
    /// • 游戏逻辑: 经验值、等级、分数等数值处理
    /// </summary>
    /// <typeparam name="Target">操作目标类型</typeparam>
    /// <typeparam name="Logic">逻辑上下文类型</typeparam>
    /// <typeparam name="ValueType">数值类型(目标数值和操作数值类型合一)</typeparam>
    /// <typeparam name="OperationOptions">操作选项类型</typeparam>
    public interface IHandleValueOperation<Target, Logic, ValueType, OperationOptions> : IOperation
    {
        /// <summary>
        /// 处理值入口操作
        /// </summary>
        /// <param name="target">操作目标对象</param>
        /// <param name="logic">逻辑上下文对象</param>
        /// <param name="operationValue">操作数值</param>
        /// <param name="selectType">操作选项</param>
        void HandleValueEntryOperation(Target target, Logic logic, ValueType operationValue, OperationOptions selectType);

        /// <summary>
        /// 取消值入口操作
        /// </summary>
        /// <param name="target">操作目标对象</param>
        /// <param name="logic">逻辑上下文对象</param>
        /// <param name="operationValue">操作数值</param>
        /// <param name="selectType">操作选项</param>
        void HandleValueEntryCancel(Target target, Logic logic, ValueType operationValue, OperationOptions selectType);
    }
}