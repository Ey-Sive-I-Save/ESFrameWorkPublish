using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ES {
    [AddComponentMenu("【ES】/UI/输入事件/拖动中-可接收列表")]
    public class EMS_Drag_LinkList : EMS_InputPointerEvent_LinkList_Abstract, IDragHandler
    {
        public void OnDrag(PointerEventData eventData)
        {
            SendLink( Channel_InputPointerEvent.Drag,new Link_InputPointerEvent() { eventData = eventData });
        }
    }
}
