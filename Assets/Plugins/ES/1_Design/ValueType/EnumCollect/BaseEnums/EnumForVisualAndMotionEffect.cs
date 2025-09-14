using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{

    public static partial class EnumCollect
    {

        //到目标动效的托管方式
        public enum ToDestionationBaseOn
        {
            [InspectorName("用ES曲线托管移动位移")] ESCurve,
            [InspectorName("用Dotween位移")] DotweenDoMove,
        }
        //到目标的空间运动形式
        public enum ToDestinationPathType
        {
            [InspectorName("直接")] Direct,
            [InspectorName("跳起落地")] JumpAndDown,
            [InspectorName("弧形(不支持)")] Rad,
            [InspectorName("按寻路(不支持)")] AIPath
        }
    }
}

