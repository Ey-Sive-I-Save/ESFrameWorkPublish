using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ES {
    [AddComponentMenu("【ES】/UI/输入事件/鼠标按下-指定接收目标")]
    public class EMS_PointerDown_LinkSingle : EMS_InputPointerEvent_LinkSingle_Abstract, IPointerDownHandler
    {
        public void OnPointerDown(PointerEventData eventData)
        {
            OnLink( Channel_InputPointerEvent.PointerDown,new Link_InputPointerEvent() { eventData = eventData });
        }
    }
}
