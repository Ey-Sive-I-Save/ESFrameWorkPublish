using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ES {
    [AddComponentMenu("<ES>ESM支持/输入/鼠标按下-可接收列表")]
    public class EMS_PointerUp_LinkList : EMS_InputPointerEvent_LinkList_Abstarct, IPointerUpHandler
    {
        public void OnPointerUp(PointerEventData eventData)
        {
            SendLink(Channel_InputPointerEvent.PointerUp,new Link_InputPointerEvent() { eventData = eventData });
        }
    }
}
