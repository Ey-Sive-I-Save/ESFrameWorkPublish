using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ES {
    [AddComponentMenu("【ES】/UI/输入事件/初始化拖动-指定接收目标")]
    public class EMS_InitializePotentialDrag_LinkSingle : EMS_InputPointerEvent_LinkSingle_Abstract, IInitializePotentialDragHandler
    {
        public void OnInitializePotentialDrag(PointerEventData eventData)
        {
            OnLink( Channel_InputPointerEvent.InitalizedPotentialDrag,new Link_InputPointerEvent() { eventData = eventData });
        }
    }
}
