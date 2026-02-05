using System;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// 实体/状态支持标记（位标记）。
    /// 由StateMachine统一维护，KCC与状态系统共同读取：Grounded/Swimming/Flying。
    /// </summary>
    [Flags]
    public enum StateSupportFlags : byte
    {
        [InspectorName("站立/地面(默认)")]
        Grounded = 1 << 0,

        [InspectorName("无")]
        None = 0,

        [InspectorName("下蹲")]
        Crouched = 1 << 1,

        [InspectorName("趴伏")]
        Prone = 1 << 2,

        [InspectorName("游泳")]
        Swimming = 1 << 3,

        [InspectorName("飞行")]
        Flying = 1 << 4,

        [InspectorName("骑乘")]
        Mounted = 1 << 5,

        [InspectorName("死亡")]
        Dead = 1 << 6,

        [InspectorName("过场")]
        Transition = 1 << 7
    }
}
