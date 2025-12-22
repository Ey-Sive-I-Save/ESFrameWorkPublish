using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ES {
    //浮点值操作
    public enum OperationOptionsForFloat
    {
        [InspectorName("加上")] Add,
        [InspectorName("减去")] Sub,
        [InspectorName("百分乘1+X")] PerUp,
        [InspectorName("限制最大")] Max,
        [InspectorName("限制最小")] Min,
        [InspectorName("震荡")] Wave,
    }
    //布尔值操作
    public enum OperationOptionsForBool
    {
        [InspectorName("或者")] Or,
        [InspectorName("并且")] And,
        [InspectorName("开")] ON,
        [InspectorName("关")] OFF,
        [InspectorName("非")] Not,
    }

    //缓冲模式
    public enum OpeationHanldeTypeForBuffer
    {
        [InspectorName("常规曲线缓冲")] NormalCurve,

    }
  
    [Flags]
    public enum SettleSelfTypeForFloat
    {
        [InspectorName("无类型")] None,
        [InspectorName("动态的")] Dynamic = 1,
        [InspectorName("可静默的")] MayListenOff = 2,
    }
}
