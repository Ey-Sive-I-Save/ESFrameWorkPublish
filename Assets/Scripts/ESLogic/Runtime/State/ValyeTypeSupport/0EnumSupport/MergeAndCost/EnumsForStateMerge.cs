using System;
using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
namespace ES
{
    /// <summary>
    /// 状态所在逻辑层级(上下控制关系)
    /// </summary>
    [Flags]
    public enum StateStayLevel
    {
        [InspectorName("低等级")] Low = 1,
        [InspectorName("垃圾层()")] Rubbish = 0,
        [InspectorName("中等级")] Middle = 2,
        [InspectorName("高等级")] High = 4,
        [InspectorName("超等级")] Super = 8,
    }
    //层级相关打断机制
    public enum StateHitByLayerOption
    {
        [InspectorName("同级别测试")] SameLevelTest,
        [InspectorName("只允许层级碾压,跳过同级别")] OnlyLayerCrush,
        [InspectorName("永远不发生")] Never,
    }
    public enum StateMergeResult
    {
        HitAndReplace,    // 打断并替换（左被右打断）
        MergeComplete,    // 合并成功（左右共存）
        MergeFail,        // 合并失败（右无法加入）
        TryWeakInterrupt     // 尝试弱打断（新增：保留左状态但降级）
    }

}
