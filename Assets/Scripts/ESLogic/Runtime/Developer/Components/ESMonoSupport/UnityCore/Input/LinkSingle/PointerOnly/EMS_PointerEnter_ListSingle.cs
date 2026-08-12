using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ES {
    [AddComponentMenu("【ES】/UI/输入事件/光标进入-指定接收目标")]
    public class EMS_PointerEnter_LinkSingle : EMS_InputPointerEvent_LinkSingle_Abstract, IPointerEnterHandler
    {
        public void OnPointerEnter(PointerEventData eventData)
        {
            OnLink( Channel_InputPointerEvent.PointerEnter,new Link_InputPointerEvent() { eventData = eventData });
        }
    }
}
