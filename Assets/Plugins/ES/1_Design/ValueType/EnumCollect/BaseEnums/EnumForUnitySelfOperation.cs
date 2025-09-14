using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ES {
    public static partial class EnumCollect
    {
        //给 三种 层级方法调用发射用
        public enum UnitySendMessageType
        {
            [InspectorName("当前对象层")] SendMessage,
            [InspectorName("包含子对象")] BroadCastMessage,
            [InspectorName("包含父对象")] SendMessageUpWards,
        }
        //有关变换的操作类型 - - 位置/旋转/缩放
        public enum TransformHandle_ValueSet
        {
            [InspectorName("直接设置")] Set,
            [InspectorName("加上")] Add,
            [InspectorName("减去")] Sub,
            [InspectorName("逼近")] Muti,
            [InspectorName("远离")] Div,
            [InspectorName("循环")] Model
        }
        //有关变换的操作空间 - - 位置/旋转/缩放
        public enum TransformHandle_ValueReleSpace
        {
            [InspectorName("世界空间")] WorldSpace,
            [InspectorName("局部空间")] LocalSpace,
        }

        //可能带目标的变换操作空间
        public enum TransformHandleSpaceWithTarget
        {
            [InspectorName("按世界偏移")] WorldSpace,
            [InspectorName("按本体坐标偏移")] SelfSpace,
            [InspectorName("从发起者到目标者面向方向偏移")] LerpLookAtSpace,
            [InspectorName("从发起者到目标者单纯值偏移")] LerpValueSpace,
        }
      
    }
}
