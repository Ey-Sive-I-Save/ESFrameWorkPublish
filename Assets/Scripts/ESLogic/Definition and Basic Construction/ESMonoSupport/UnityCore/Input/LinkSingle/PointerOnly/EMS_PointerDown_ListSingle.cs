using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ES {
    [AddComponentMenu("<ES>ESM支持/输入/鼠标按下-指定接收目标")]
    public class EMS_PointerDown_LinkSingle : EMS_InputPointerEvent_LinkSingle_Abstarct, IPointerDownHandler
    {
        public void OnPointerDown(PointerEventData eventData)
        {
            OnLink( Channel_InputPointerEvent.PointerDown,new Link_InputPointerEvent() { eventData = eventData });
        }
    }
}
