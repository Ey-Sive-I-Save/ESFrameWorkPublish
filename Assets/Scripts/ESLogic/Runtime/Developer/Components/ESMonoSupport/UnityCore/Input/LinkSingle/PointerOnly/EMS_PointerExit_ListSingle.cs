using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ES {
    [AddComponentMenu("【ES】/UI/输入事件/光标退出-指定接收目标")]
    public class EMS_PointerExit_LinkSingle : EMS_InputPointerEvent_LinkSingle_Abstract, IPointerExitHandler
    {
        public void OnPointerExit(PointerEventData eventData)
        {
            OnLink( Channel_InputPointerEvent.PointerExit,new Link_InputPointerEvent() { eventData = eventData });
        }
    }
}
