using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace ES
{
    /// <summary>
    /// 状态系统的 CostPart 通道位掩码定义。
    /// - 使用 Flags 标记表示可组合的通道掩码。
    /// - 指定底层类型为 <c>uint</c> 以避免位溢出并保留扩展空间。
    /// - 建议在运行时将每个位映射到对应的 AvatarMask / 骨骼集合，以便自动把曲线和 IK 绑定到通道。
    /// </summary>
    [Flags]
    public enum StateChannelMask : uint
    {
        /// <summary>无占用。</summary>
        [InspectorName("无")]
        None = 0u,

        /// <summary>右手（手部/手臂）</summary>
        [InspectorName("右手")]
        RightHand = 1u << 0,
        /// <summary>左手（手部/手臂）</summary>
        [InspectorName("左手")]
        LeftHand = 1u << 1,
        /// <summary>双手（右手 | 左手）</summary>
        [InspectorName("双手")]
        DoubleHand = RightHand | LeftHand,

        /// <summary>右腿（腿部/髋/膝）</summary>
        [InspectorName("右腿")]
        RightLeg = 1u << 2,
        /// <summary>左腿（腿部/髋/膝）</summary>
        [InspectorName("左腿")]
        LeftLeg = 1u << 3,
        /// <summary>双腿</summary>
        [InspectorName("双腿")]
        DoubleLeg = RightLeg | LeftLeg,

        /// <summary>四肢（双手 + 双腿）</summary>
        [InspectorName("四肢")]
        FourLimbs = DoubleHand | DoubleLeg,

        /// <summary>头部</summary>
        [InspectorName("头部")]
        Head = 1u << 4,
        /// <summary>身体脊柱/躯干</summary>
        [InspectorName("躯干")]
        BodySpine = 1u << 5,
        /// <summary>全身占据活动（四肢 + 头 + 躯干）</summary>
        [InspectorName("全身占据活动")]
        AllBodyActive = FourLimbs | Head | BodySpine,

        /// <summary>心灵/思考（意愿类通道）</summary>
        [InspectorName("心灵:思考")]
        Heart = 1u << 6,
        /// <summary>眼睛（注视/视线）</summary>
        [InspectorName("眼睛")]
        Eye = 1u << 7,
        /// <summary>耳朵（听觉/注意力）</summary>
        [InspectorName("耳朵")]
        Ear = 1u << 8,
        /// <summary>全身 + 心智 + 感官</summary>
        [InspectorName("全身心")]
        AllBodyAndHeartAndMore = AllBodyActive | Heart | Eye | Ear,

        /// <summary>目标相关（比如指向目标、拾取目标等）</summary>
        [InspectorName("目标")]
        Target = 1u << 9,

        // 预留位：从 10 开始保留若干位以便未来扩展或自定义通道
        Reserved10 = 1u << 10,
        Reserved11 = 1u << 11,
        Reserved12 = 1u << 12,

        /// <summary>
        /// 所有已定义的位（不含预留位）。如果需要包含预留位，请手动扩展此字段或使用 ~0u。
        /// </summary>
        All = RightHand | LeftHand | RightLeg | LeftLeg | Head | BodySpine | Heart | Eye | Ear | Target | FourLimbs
    }
}
