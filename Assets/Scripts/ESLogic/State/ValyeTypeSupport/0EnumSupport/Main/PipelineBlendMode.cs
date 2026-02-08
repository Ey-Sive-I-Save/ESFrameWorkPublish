using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// 层级混合模式 - 定义Main线和Basic线如何混合
    /// </summary>
    public enum PipelineBlendMode
    {
        /// <summary>
        /// 叠加模式（默认）- Main和Basic按权重叠加，权重总和可能>1.0
        /// 适用场景：需要Basic和Main同时展示的情况
        /// 表现：Basic(1.0) + Main(0.7) → 总权重1.7（可能导致动画强度过高）
        /// </summary>
        [InspectorName("叠加模式（权重累加）")]
        Additive = 0,
        
        /// <summary>
        /// 覆盖模式（推荐）- Main激活时完全覆盖Basic，Main未激活时100% Basic
        /// 适用场景：技能动画覆盖基础动作（攻击覆盖移动）
        /// 表现：Main激活时 → Basic(0.0) + Main(1.0), Main未激活时 → Basic(1.0) + Main(0.0)
        /// </summary>
        [InspectorName("覆盖模式（Main覆盖Basic）")]
        Override = 1,
        
        /// <summary>
        /// 乘法模式 - Basic权重被Main的激活度调制（淡入淡出效果）
        /// 适用场景：Main逐渐接管控制权的过渡动画
        /// 表现：Main(0.5) → Basic权重 × (1 - 0.5), 实现平滑过渡
        /// </summary>
        [InspectorName("乘法模式（Main调制Basic）")]
        Multiplicative = 2
    }
}
