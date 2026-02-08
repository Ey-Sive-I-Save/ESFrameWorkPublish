using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// ES框架 - 运行时目标类 (ESRuntimeTarget)
    /// 【对象池化目标系统的基础实现】
    ///
    /// 【当前状态】
    /// 实现了IPoolableAuto接口，支持对象池管理
    /// 为后续扩展目标功能提供基础设施
    ///
    /// 【设计目标】
    /// • 提供灵活的运行时目标定义和存储
    /// • 支持多种目标类型（位置、对象、方向等）
    /// • 实现高效的内存管理和对象复用
    /// • 为Operation系统提供目标数据支持
    ///
    /// 【核心特性】
    /// • 对象池支持：全局静态池，容量1000，支持高频使用
    /// • 接口实现：完整实现IPoolableAuto，支持自动回收
    /// • 扩展就绪：预留了目标数据字段的扩展空间
    /// </summary>
    public class ESRuntimeTarget : IPoolableAuto
    {
        #region 对象池基本支持

        /// <summary>
        /// 全局静态对象池 - ESRuntimeTarget专用池
        /// 【配置说明】
        /// • 容量: 1000个对象，支持大规模并发使用
        /// • 预热: 20个初始对象，减少首次分配开销
        /// • 线程安全: ESSimplePool提供完整的线程安全保证
        /// 【使用方式】
        /// var target = ESRuntimeTarget.Pool.GetInPool();
        /// target.TryAutoPushedToPool();
        /// </summary>
        public static readonly ESSimplePool<ESRuntimeTarget> Pool = new ESSimplePool<ESRuntimeTarget>(
            factoryMethod: () => new ESRuntimeTarget(),
            resetMethod: (obj) => obj.OnResetAsPoolable(),
            initCount: 20,
            maxCount: 1000,
            poolDisplayName: "ESRuntimeTarget Pool"
        );

        /// <summary>
        /// 对象回收标记 (IPoolableAuto接口要求)
        /// 【作用】防止对象被重复回收，确保对象池的完整性
        /// 【管理】GetInPool时自动设为false，PushToPool时自动设为true
        /// </summary>
        public bool IsRecycled { get; set; }

        /// <summary>
        /// 重置对象状态，准备回收到池中 (IPoolableAuto接口要求)
        /// 【调用时机】对象被放回对象池时，由池系统自动调用
        /// 【当前实现】空实现，因为类中暂无需要重置的字段
        /// 【扩展说明】当添加目标数据字段时，需要在此重置为默认值
        /// </summary>
        public void OnResetAsPoolable()
        {
            // 当前类无需要重置的字段，预留给未来扩展
        }

        /// <summary>
        /// 尝试自动回收到对象池 (IPoolableAuto接口要求)
        /// 【安全机制】检查IsRecycled状态，防止重复回收
        /// 【执行逻辑】设置回收标记后将对象放回池中
        /// 【性能优化】避免无效的池操作，提升系统性能
        /// </summary>
        public void TryAutoPushedToPool()
        {
            if (!IsRecycled)
            {
                // ★ 不在这里设置 IsRecycled = true
                // PushToPool 内部流程：检查IsRecycled → resetMethod → 设置IsRecycled=true → 入栈
                // 如果提前设置，PushToPool会误判为"已回收"而拒绝入池
                Pool.PushToPool(this);
            }
        }

        #endregion
    }
}