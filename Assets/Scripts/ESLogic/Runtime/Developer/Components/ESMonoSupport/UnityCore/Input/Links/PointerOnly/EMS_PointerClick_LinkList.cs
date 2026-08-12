using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ES {
    [AddComponentMenu("【ES】/UI/输入事件/光标点击-可接收列表")]
    public class EMS_PointerClick_LinkList : EMS_InputPointerEvent_LinkList_Abstract, IPointerClickHandler
    {
        
        public void OnPointerClick(PointerEventData eventData)
        {
            SendLink(Channel_InputPointerEvent.PointerClick,new Link_InputPointerEvent() { eventData=eventData });
        }
    }
}
