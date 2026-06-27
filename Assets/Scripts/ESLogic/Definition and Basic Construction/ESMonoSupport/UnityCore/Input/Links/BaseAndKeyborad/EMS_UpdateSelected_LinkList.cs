using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ES {
    [AddComponentMenu("<ES>ESM支持/输入/选择中-可接收列表")]
    public class EMS_UpdateSelected_LinkList : EMS_InputBaseEvent_LinkList_Abstarct, IUpdateSelectedHandler
    {
        public void OnUpdateSelected(BaseEventData eventData)
        {
            SendLink(Channel_InputBaseEvent.UpdateSelected,new Link_InputBaseEvent() { eventData = eventData });
        }
    }
}
