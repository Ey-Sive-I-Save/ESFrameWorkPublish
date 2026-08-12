using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ES {
    [AddComponentMenu("【ES】/UI/输入事件/鼠标按下-可接收列表")]
    public class EMS_PointerUp_LinkList : EMS_InputPointerEvent_LinkList_Abstract, IPointerUpHandler
    {
        public void OnPointerUp(PointerEventData eventData)
        {
            SendLink(Channel_InputPointerEvent.PointerUp,new Link_InputPointerEvent() { eventData = eventData });
        }
    }
}
