using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ES {
    [AddComponentMenu("<ES>ESM支持/输入/初始化拖动-指定接收目标")]
    public class EMS_InitializePotentialDrag_LinkSingle : EMS_InputPointerEvent_LinkSingle_Abstarct, IInitializePotentialDragHandler
    {
        public void OnInitializePotentialDrag(PointerEventData eventData)
        {
            OnLink( Channel_InputPointerEvent.InitalizedPotentialDrag,new Link_InputPointerEvent() { eventData = eventData });
        }
    }
}
