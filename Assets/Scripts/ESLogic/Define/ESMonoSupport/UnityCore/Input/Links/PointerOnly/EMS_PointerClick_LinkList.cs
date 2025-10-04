using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ES {
    [AddComponentMenu("<ES>ESM支持/输入/光标点击-可接收列表")]
    public class EMS_PointerClick_LinkList : EMS_InputPointerEvent_LinkList_Abstarct, IPointerClickHandler
    {
        
        public void OnPointerClick(PointerEventData eventData)
        {
            SendLink(Channel_InputPointerEvent.PointerClick,new Link_InputPointerEvent() { eventData=eventData });
        }
    }
}
