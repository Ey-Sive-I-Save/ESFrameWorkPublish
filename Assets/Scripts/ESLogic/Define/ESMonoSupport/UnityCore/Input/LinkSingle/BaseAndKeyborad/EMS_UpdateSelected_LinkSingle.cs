using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ES {
    [AddComponentMenu("<ES>ESM支持/输入/选择中-指定接收目标")]
    public class EMS_UpdateSelected_LinkSingle : EMS_InputBaseEvent_LinkSingle_Abstarct, IUpdateSelectedHandler
    {
   
        public void OnUpdateSelected(BaseEventData eventData)
        {
            Link_?.OnLink( Channel_InputBaseEvent.UpdateSelected ,new Link_InputBaseEvent() { eventData = eventData });
        }
    }
}
