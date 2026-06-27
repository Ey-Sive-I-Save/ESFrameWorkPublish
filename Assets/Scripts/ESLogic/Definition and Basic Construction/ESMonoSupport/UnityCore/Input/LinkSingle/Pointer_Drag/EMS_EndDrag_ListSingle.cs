using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ES {
    [AddComponentMenu("<ES>ESM支持/输入/结束拖动-指定接收目标")]
    public class EMS_EndDrag_LinkSingle : EMS_InputPointerEvent_LinkSingle_Abstarct, IEndDragHandler
    {
        public void OnEndDrag(PointerEventData eventData)
        {
            OnLink( Channel_InputPointerEvent.EndDrag,new Link_InputPointerEvent() { eventData = eventData });
        }
    }
}
