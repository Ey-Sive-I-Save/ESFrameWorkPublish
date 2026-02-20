using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ============================================================================
// 文件：StateBaseStatus.cs
// 作用：StateBase 生命周期状态枚举（用于表示状态从未启动/运行中/已退出）。
//
// Public（本文件定义的对外成员；按模块分组，“先功能、后成员”，便于扫读）：
//
// 【生命周期状态】
// - 从未启动：public enum StateBaseStatus.Never
// - 运行中：public enum StateBaseStatus.Running
// - 已退出：public enum StateBaseStatus.Exited
// ============================================================================

namespace ES
{
   public enum StateBaseStatus
    {
        [InspectorName("从未启动")]
        Never = 0,
        [InspectorName("运行中")]
        Running = 1,
        [InspectorName("退出")]
        Exited = 2,
    }

    
}
