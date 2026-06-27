using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ES {
    [AddComponentMenu("<ES>ESM支持/输入/鼠标按下-指定接收目标")]
    public class EMS_PointerUp_LinkSingle
        : EMS_InputPointerEvent_LinkSingle_Abstarct, IPointerUpHandler
    {
        public void OnPointerUp(PointerEventData eventData)
        {
            SendLink( Channel_InputPointerEvent.PointerEnter,new Link_InputPointerEvent() { eventData = eventData });
        }
    }
}
