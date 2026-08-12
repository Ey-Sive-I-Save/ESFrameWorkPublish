using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ES {
    [AddComponentMenu("【ES】/UI/输入事件/可拖动落入此处-可接收列表")]
    public class EMS_Drop_LinkList : EMS_InputPointerEvent_LinkList_Abstract, IDropHandler
    {

        public void OnDrop(PointerEventData eventData)
        {
            SendLink( Channel_InputPointerEvent.Drop,new Link_InputPointerEvent() { eventData = eventData });
        }
    }
}
