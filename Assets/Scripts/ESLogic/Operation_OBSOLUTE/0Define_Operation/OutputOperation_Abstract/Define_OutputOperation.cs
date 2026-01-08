using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// ES框架 - 输出操作系统(Output Operation System)定义
    /// 
    /// 【核心概念】
    /// OutputOperation是可随时装载和卸载的简单操作单元，专注于向目标对象输出处理结果
    /// 支持操作的动态加载、执行和取消，为复杂的业务逻辑提供灵活的组合能力
    /// 
    /// 【架构演进历程】
    /// • 旧标准(OBSOLUTE): 采用针化操作模式 On/From/With 三参数链式调用
    ///   - 优势: 链式语法直观，参数职责明确
    ///   - 缺陷: 类型参数固定，扩展性受限
    /// 
    /// • 新标准(Current): 采用实时目标+依赖未定上下文模式
    ///   - 优势: 目标类型灵活，上下文可动态注入
    ///   - 特点: 不依赖确定的类型参数，支持运行时类型决策
    /// 
    /// 【设计原则】
    /// • 单一职责: 每个Operation只负责一种特定的输出逻辑
    /// • 可组合性: 支持多个Operation的组合和链式执行
    /// • 可取消性: 提供操作取消机制，支持事务性回滚
    /// • 类型安全: 通过泛型约束保证编译时类型检查
    /// </summary>
    /// <summary>
    #region  旧标准输出操作接口说明
    /// 旧标准输出操作接口 (OBSOLETE - 已废弃)
    /// 
    /// 【设计理念】
    /// 基于针化操作模式的三参数设计:
    /// • On: 操作目标对象，明确指定操作的作用目标
    /// • From: 数据来源对象，提供操作所需的输入数据
    /// • With: 操作参数对象，携带操作执行的额外参数
    /// 
    /// 【典型使用场景】
    /// <code>
    /// // 示例: 伤害操作
    /// damageOperation.TryOperation(
    ///     on: targetEnemy,      // 目标: 敌方单位
    ///     from: skillCaster,    // 来源: 施法者
    ///     with: damageParams    // 参数: 伤害数值和类型
    /// );
    /// </code>
    /// 
    /// 【废弃原因】
    /// • 类型参数过于固定，扩展性不足
    /// • 三参数模式在复杂场景下参数传递冗余
    /// • 缺乏对动态类型和运行时上下文的支持
    /// </summary>
    /// <typeparam name="On">操作目标类型 - 接受操作影响的对象</typeparam>
    /// <typeparam name="From">数据来源类型 - 提供操作数据的对象</typeparam>
    /// <typeparam name="With">操作参数类型 - 携带操作执行参数的对象</typeparam>
    [System.Obsolete("此接口已废弃，请使用IOutputOperation<Target,Logic>新标准接口")]
    public interface IOutputOperation_OBSOLUTE<On, From, With> : IOperation<On, From, With>
    {
        /// <summary>
        /// 尝试执行输出操作
        /// </summary>
        /// <param name="on">操作目标对象</param>
        /// <param name="from">数据来源对象</param>
        /// <param name="with">操作参数对象</param>
        void TryOperation(On on, From from, With with);

        /// <summary>
        /// 尝试取消输出操作
        /// 用于支持操作的回滚和撤销机制
        /// </summary>
        /// <param name="on">操作目标对象</param>
        /// <param name="from">数据来源对象</param> 
        /// <param name="with">操作参数对象</param>
        void TryCancel(On on, From from, With with);
    }
    #endregion
    /// <summary>
    /// 新标准输出操作接口 (Current Standard)
    /// 
    /// 【设计理念】
    /// 基于实时目标+逻辑上下文的双参数设计:
    /// • Target: 操作的实时目标对象，支持运行时类型决策
    /// • Logic: 逻辑上下文对象，提供操作执行所需的依赖和环境
    /// 
    /// 【核心优势】
    /// • 灵活的目标类型: Target可以是任意类型，支持多态和动态绑定
    /// • 丰富的逻辑上下文: Logic承载复杂的执行环境和依赖关系
    /// • 简化的参数模型: 相比三参数模式，减少了参数传递的复杂度
    /// • 更好的扩展性: 易于添加新的目标类型和逻辑上下文
    /// 
    /// 【典型使用场景】
    /// <code>
    /// // 示例: 属性修改操作
    /// var attributeOperation = GetAttributeOperation();
    /// attributeOperation.TryOperation(
    ///     target: playerCharacter,           // 目标: 玩家角色
    ///     logic: attributeModifyContext     // 逻辑: 属性修改上下文
    /// );
    /// 
    /// // 示例: 技能效果操作
    /// skillEffectOperation.TryOperation(
    ///     target: affectedUnits,            // 目标: 受影响单位列表
    ///     logic: skillExecutionContext     // 逻辑: 技能执行上下文
    /// );
    /// </code>
    /// 
    /// 【与旧标准对比】
    /// | 方面 | 旧标准(On/From/With) | 新标准(Target/Logic) |
    /// |------|---------------------|---------------------|
    /// | 参数数量 | 3个固定参数 | 2个灵活参数 |
    /// | 类型灵活性 | 编译时确定 | 运行时决策 |
    /// | 扩展性 | 受限 | 优秀 |
    /// | 学习成本 | 较高 | 较低 |
    /// </summary>
    /// <typeparam name="Target">操作目标类型 - 可以是任意接受操作的对象类型</typeparam>
    /// <typeparam name="Logic">逻辑上下文类型 - 提供操作执行环境和依赖的对象类型</typeparam>
    public interface IOutputOperation<Target, Logic> : IOperation<Logic, Target>
    {
        /// <summary>
        /// 尝试执行输出操作
        /// 
        /// 此方法是Operation的核心执行入口，负责:
        /// • 验证目标对象的有效性
        /// • 从逻辑上下文中获取执行所需的数据
        /// • 执行具体的操作逻辑
        /// • 处理操作执行过程中的异常情况
        /// </summary>
        /// <param name="target">操作的目标对象，将接受操作的影响</param>
        /// <param name="logic">逻辑上下文对象，提供操作执行的环境和依赖</param>
        void TryOperation(Target target, Logic logic);

        /// <summary>
        /// 尝试取消输出操作
        /// 
        /// 提供操作的撤销和回滚能力，用于:
        /// • 撤销已执行的操作效果
        /// • 回滚目标对象的状态变化
        /// • 释放操作占用的资源
        /// • 触发取消操作的相关事件
        /// 
        /// 注意: 并非所有操作都支持取消，具体实现需要根据业务需求决定
        /// </summary>
        /// <param name="target">需要撤销操作影响的目标对象</param>
        /// <param name="logic">提供取消操作所需上下文的逻辑对象</param>
        void TryCancel(Target target, Logic logic);
    }
    /// <summary>
    /// 强制可取消的输出操作抽象基类
    /// 
    /// 【设计目的】
    /// 为那些必须支持取消功能的操作提供统一的基础实现，特别适用于:
    /// • 委托类操作: 需要能够撤销委托的注册和绑定
    /// • 事件订阅操作: 需要能够取消事件的订阅关系
    /// • 资源占用操作: 需要能够释放占用的系统资源
    /// • 异步操作: 需要能够取消正在进行的异步任务
    /// 
    /// 【核心特性】
    /// • 强制取消支持: 所有子类都必须实现取消逻辑
    /// • 取消回调机制: 提供OnCancel委托，允许外部注册取消时的处理逻辑
    /// • 默认取消行为: 提供DefaultAction作为取消操作的默认实现
    /// 
    /// 【使用场景示例】
    /// <code>
    /// // 委托操作示例
    /// public class DelegateOperation : OutputOperation_MustCancel&lt;GameObject, DelegateContext&gt;
    /// {
    ///     public override void TryOperation(GameObject target, DelegateContext logic)
    ///     {
    ///         // 注册委托到目标对象
    ///         target.GetComponent&lt;EventHandler&gt;().AddDelegate(logic.Handler);
    ///     }
    ///     
    ///     public override void TryCancel(GameObject target, DelegateContext logic)
    ///     {
    ///         // 从目标对象移除委托
    ///         target.GetComponent&lt;EventHandler&gt;().RemoveDelegate(logic.Handler);
    ///         OnCancel?.Invoke(target, logic); // 触发取消回调
    ///     }
    /// }
    /// </code>
    /// 
    /// 【最佳实践】
    /// • 在TryOperation中记录操作状态，便于TryCancel时进行准确回滚
    /// • 合理使用OnCancel回调，避免产生循环依赖
    /// • 确保TryCancel的幂等性，多次调用不会产生副作用
    /// </summary>
    /// <typeparam name="Target">操作目标类型</typeparam>
    /// <typeparam name="Logic">逻辑上下文类型</typeparam>
    public abstract class OutputOperation_MustCancel<Target, Logic> : IOutputOperation<Target, Logic>
    {
        /// <summary>
        /// 默认的取消操作实现
        /// 
        /// 这是一个空实现，用作OnCancel委托的默认值
        /// 子类可以根据需要重写此方法或直接设置OnCancel委托
        /// </summary>
        /// <param name="target">操作目标对象</param>
        /// <param name="logic">逻辑上下文对象</param>
        public static void DefaultAction(Target target, Logic logic)
        {
            // 默认空实现
            // 子类可以根据具体需求重写此方法
        }

        /// <summary>
        /// 取消操作的回调委托
        /// 
        /// 【用途】
        /// • 允许外部代码注册取消时的自定义处理逻辑
        /// • 支持取消操作的事件通知机制
        /// • 提供额外的清理和资源释放入口
        /// 
        /// 【注意事项】
        /// • 默认指向DefaultAction，避免空引用异常
        /// • 可以通过赋值或+=操作符来设置或添加回调
        /// • 回调执行顺序与添加顺序一致
        /// </summary>
        public Action<Target, Logic> OnCancel = DefaultAction;

        /// <summary>
        /// 执行输出操作的抽象方法
        /// 
        /// 子类必须实现此方法，定义具体的操作执行逻辑
        /// 建议在实现中记录必要的状态信息，以便TryCancel时进行准确回滚
        /// </summary>
        /// <param name="target">操作目标对象</param>
        /// <param name="logic">逻辑上下文对象</param>
        public abstract void TryOperation(Target target, Logic logic);

        /// <summary>
        /// 取消输出操作的抽象方法
        /// 
        /// 子类必须实现此方法，定义具体的取消和回滚逻辑
        /// 实现时应该:
        /// • 撤销TryOperation中执行的所有操作
        /// • 恢复目标对象的原始状态
        /// • 调用OnCancel委托通知外部代码
        /// • 释放占用的资源
        /// </summary>
        /// <param name="target">需要撤销操作的目标对象</param>
        /// <param name="logic">提供取消操作上下文的逻辑对象</param>
        public abstract void TryCancel(Target target, Logic logic);    }

    #region 架构设计说明
    /*
    ╔═══════════════════════════════════════════════════════════════════════════════╗
    ║                           OutputOperation 架构设计说明                        ║
    ╠═══════════════════════════════════════════════════════════════════════════════╣
    ║                                                                               ║
    ║ 【设计模式】                                                                   ║
    ║ • 策略模式: 不同的Operation实现不同的操作策略                                  ║
    ║ • 命令模式: Operation封装了操作请求，支持撤销和重做                            ║
    ║ • 模板方法: 抽象基类定义操作流程，子类实现具体细节                            ║
    ║                                                                               ║
    ║ 【类型安全】                                                                   ║
    ║ • 泛型约束确保编译时类型检查                                                   ║
    ║ • Target和Logic类型参数提供灵活的类型适配                                     ║
    ║                                                                               ║
    ║ 【扩展指南】                                                                   ║
    ║ 1. 继承IOutputOperation<Target,Logic>实现基本操作                            ║
    ║ 2. 继承OutputOperation_MustCancel实现可取消操作                              ║
    ║ 3. 根据业务需求定义Target和Logic的具体类型                                    ║
    ║ 4. 在TryOperation中实现核心业务逻辑                                           ║
    ║ 5. 在TryCancel中实现回滚和清理逻辑                                            ║
    ║                                                                               ║
    ║ 【性能考虑】                                                                   ║
    ║ • Operation应该是轻量级的，避免在构造函数中进行重量级初始化                    ║
    ║ • 考虑使用对象池管理Operation实例，减少GC压力                                 ║
    ║ • TryOperation和TryCancel应该具备良好的性能特性                              ║
    ║                                                                               ║
    ╚═══════════════════════════════════════════════════════════════════════════════╝
    */
    #endregion
}
