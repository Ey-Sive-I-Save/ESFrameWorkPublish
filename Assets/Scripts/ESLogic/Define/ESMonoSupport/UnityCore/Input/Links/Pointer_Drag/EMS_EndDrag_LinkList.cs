using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ES {
    [AddComponentMenu("<ES>ESM支持/输入/结束拖动-可接收列表")]
    public class EMS_EndDrag
        : EMS_InputPointerEvent_LinkList_Abstarct, IEndDragHandler
    {
        public void OnEndDrag(PointerEventData eventData)
        {
            SendLink( Channel_InputPointerEvent.EndDrag,new Link_InputPointerEvent() { eventData = eventData });
        }
    }
}
