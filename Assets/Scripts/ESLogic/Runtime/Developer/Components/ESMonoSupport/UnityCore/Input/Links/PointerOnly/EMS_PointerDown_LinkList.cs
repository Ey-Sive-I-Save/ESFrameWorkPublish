using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ES {
    [AddComponentMenu("【ES】/UI/输入事件/鼠标按下-可接收列表")]
    public class EMS_PointerDown_LinkList : EMS_InputPointerEvent_LinkList_Abstract, IPointerDownHandler
    {
        public void OnPointerDown(PointerEventData eventData)
        {
            SendLink( Channel_InputPointerEvent.PointerDown, new Link_InputPointerEvent() { eventData = eventData });
        }
    }
}
