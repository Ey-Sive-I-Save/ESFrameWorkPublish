using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{

    public static partial class EnumCollect
    {
        //对 Feel插件的扩展-对MMF_Player操作
        public enum PointerMMFPlayerHandleOptions
        {
            [InspectorName("播放")] PlayFeedbacks,
            [InspectorName("停止")] StopFeedbacks,
            [InspectorName("反转")] Revert,
            [InspectorName("跳过")] SkipToTheEnd,
            [InspectorName("手动初始化")] Inititialization,
            [InspectorName("恢复初值")] RestoreInitialValues,


            [InspectorName("设置方向")] SetDirection,
            [InspectorName("设置强度")] SetIntensity,
            [InspectorName("设置时间缩放类型")] SetTimeScaleMode,
            [InspectorName("设置持续时间乘数")] SetDurationMultipler,
            [InspectorName("设置冷却")] SetCoolDown,
            [InspectorName("设置生效中心和范围")] SetRangeCenterAndDistance,
            [InspectorName("设置可用")] SetCanPlay
        }

        //Dotween支持  ： 回调类型
        public enum CallBackType
        {
            [InspectorName("完成时")] OnComplete,
            [InspectorName("击杀时")] OnKill,
            [InspectorName("更新时")] OnUpdate,
            [InspectorName("开启时")] OnPlay,
            [InspectorName("暂停时")] OnPause,
            [InspectorName("回退时")] OnRewind,
            [InspectorName("单次循环完成时")] OnStepComplete,
            [InspectorName("路径修改时")] OnWayPointChange,
        }


    }
}

