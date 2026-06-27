using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ES {
    [AddComponentMenu("<ES>ESM支持/输入/可拖动落入此处-指定接收目标")]
    public class EMS_Drop_LinkSingle : EMS_InputPointerEvent_LinkSingle_Abstarct, IDropHandler
    {
        public void OnDrop(PointerEventData eventData)
        {
            /* eventData.pointerDrag*/
            OnLink( Channel_InputPointerEvent.Drop,new Link_InputPointerEvent() { eventData = eventData });
        }
    }
}
