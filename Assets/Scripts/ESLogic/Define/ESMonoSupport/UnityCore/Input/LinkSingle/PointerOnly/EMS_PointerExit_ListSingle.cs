using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ES {
    [AddComponentMenu("<ES>ESM支持/输入/光标退出-指定接收目标")]
    public class EMS_PointerExit_LinkSingle : EMS_InputPointerEvent_LinkSingle_Abstarct, IPointerExitHandler
    {
        public void OnPointerExit(PointerEventData eventData)
        {
            OnLink( Channel_InputPointerEvent.PointerExit,new Link_InputPointerEvent() { eventData = eventData });
        }
    }
}
