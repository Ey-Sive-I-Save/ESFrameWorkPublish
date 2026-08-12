using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ES {
    [AddComponentMenu("【ES】/UI/输入事件/鼠标按下-指定接收目标")]
    public class EMS_PointerUp_LinkSingle
        : EMS_InputPointerEvent_LinkSingle_Abstract, IPointerUpHandler
    {
        public void OnPointerUp(PointerEventData eventData)
        {
            SendLink( Channel_InputPointerEvent.PointerEnter,new Link_InputPointerEvent() { eventData = eventData });
        }
    }
}
