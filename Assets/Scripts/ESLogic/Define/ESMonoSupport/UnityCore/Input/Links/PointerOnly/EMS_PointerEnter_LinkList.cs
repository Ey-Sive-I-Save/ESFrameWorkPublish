using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ES {
    [AddComponentMenu("<ES>ESM支持/输入/光标进入-可接收列表")]
    public class EMS_PointerEnter_LinkList : EMS_InputPointerEvent_LinkList_Abstarct, IPointerEnterHandler
    {
        public void OnPointerEnter(PointerEventData eventData)
        {
           SendLink(Channel_InputPointerEvent.PointerEnter,new Link_InputPointerEvent() { eventData = eventData });
        }
    }
}
