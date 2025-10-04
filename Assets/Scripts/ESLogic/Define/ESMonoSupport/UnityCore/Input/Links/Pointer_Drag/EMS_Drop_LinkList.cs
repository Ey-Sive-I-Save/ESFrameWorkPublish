using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ES {
    [AddComponentMenu("<ES>ESM支持/输入/可拖动落入此处-可接收列表")]
    public class EMS_Drop_LinkList : EMS_InputPointerEvent_LinkList_Abstarct, IDropHandler
    {

        public void OnDrop(PointerEventData eventData)
        {
            SendLink( Channel_InputPointerEvent.Drop,new Link_InputPointerEvent() { eventData = eventData });
        }
    }
}
