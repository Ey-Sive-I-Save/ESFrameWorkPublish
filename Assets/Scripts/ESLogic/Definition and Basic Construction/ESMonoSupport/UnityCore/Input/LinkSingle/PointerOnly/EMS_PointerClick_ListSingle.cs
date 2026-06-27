using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ES {
    [AddComponentMenu("<ES>ESM支持/输入/光标点击-指定接收目标")]
    public class EMS_PointerClick_LinkSingle : EMS_InputPointerEvent_LinkSingle_Abstarct, IPointerClickHandler
    {
        public void OnPointerClick(PointerEventData eventData)
        {
            OnLink( Channel_InputPointerEvent.PointerClick,new Link_InputPointerEvent() { eventData=eventData });
        }
    }
}
