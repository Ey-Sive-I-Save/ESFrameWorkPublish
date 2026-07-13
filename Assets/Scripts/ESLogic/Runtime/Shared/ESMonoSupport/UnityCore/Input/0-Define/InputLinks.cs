using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ES {
    public enum Channel_InputBaseEvent
    {
        [InspectorName("基本-移动")]Move,
        [InspectorName("基本-取消")] Cancel,
        [InspectorName("基本-选择")] Select,
        [InspectorName("基本-提交")] Submit,
        [InspectorName("基本-选择中(帧更新)")] UpdateSelected,
        [InspectorName("轴-移动")] Axis_Move
    }
    public struct Link_InputBaseEvent 
    {
        public BaseEventData eventData;
    }
    public enum Channel_InputPointerEvent
    {
        [InspectorName("光标-点击")] PointerClick,
        [InspectorName("光标-落下")] PointerDown,
        [InspectorName("光标-抬起")] PointerUp,
        [InspectorName("光标-进入")] PointerEnter,
        [InspectorName("光标-退出")] PointerExit,
        [InspectorName("光标-开始拖动")] BeginDrag,
        [InspectorName("光标-拖动中")] Drag,
        [InspectorName("光标-落放到这")] Drop,
        [InspectorName("光标-停止拖动")] EndDrag,
        [InspectorName("光标-最初始化拖动")] InitalizedPotentialDrag,
       
    }
    public struct Link_InputPointerEvent
    {
        public PointerEventData eventData;
    }
    public struct Link_InputAxisEvent
    {
        public PointerEventData eventData;
    }
}
