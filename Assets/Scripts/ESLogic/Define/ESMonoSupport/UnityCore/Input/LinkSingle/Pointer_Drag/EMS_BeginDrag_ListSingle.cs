using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ES {
    [AddComponentMenu("<ES>ESM支持/输入/开始拖动-可接收列表")]
    public class EMS_BeginDrag_LinkSingle : EMS_InputPointerEvent_LinkSingle_Abstarct, IBeginDragHandler
    {
        
        public void OnBeginDrag(PointerEventData eventData)
        {
            Link_?.OnLink( Channel_InputPointerEvent.BeginDrag, new Link_InputPointerEvent() { eventData = eventData });
        }
    }
}
