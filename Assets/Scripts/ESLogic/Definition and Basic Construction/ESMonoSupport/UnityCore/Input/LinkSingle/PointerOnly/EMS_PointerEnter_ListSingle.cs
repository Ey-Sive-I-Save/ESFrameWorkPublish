using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ES {
    [AddComponentMenu("<ES>ESM支持/输入/光标进入-指定接收目标")]
    public class EMS_PointerEnter_LinkSingle : EMS_InputPointerEvent_LinkSingle_Abstarct, IPointerEnterHandler
    {
        public void OnPointerEnter(PointerEventData eventData)
        {
            OnLink( Channel_InputPointerEvent.PointerEnter,new Link_InputPointerEvent() { eventData = eventData });
        }
    }
}
