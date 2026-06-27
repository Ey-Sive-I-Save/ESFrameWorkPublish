using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ES {
    [AddComponentMenu("<ES>ESM支持/输入/光标退出-可接收列表")]
    public class EMS_PointerExit_LinkList : EMS_InputPointerEvent_LinkList_Abstarct, IPointerExitHandler
    {
        public void OnPointerExit(PointerEventData eventData)
        {
           SendLink(Channel_InputPointerEvent.PointerEnter,new Link_InputPointerEvent() { eventData = eventData });
        }
    }
}
