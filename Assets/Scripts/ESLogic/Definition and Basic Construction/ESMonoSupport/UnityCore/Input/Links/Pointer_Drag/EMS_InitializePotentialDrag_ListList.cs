using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ES {
    [AddComponentMenu("<ES>ESM支持/输入/初始化拖动-可接收列表")]
    public class EMS_InitializePotentialDrag
        : EMS_InputPointerEvent_LinkList_Abstarct, IInitializePotentialDragHandler
    {
        public void OnInitializePotentialDrag(PointerEventData eventData)
        {
            SendLink( Channel_InputPointerEvent.InitalizedPotentialDrag,new Link_InputPointerEvent() { eventData = eventData });
        }
    }
}
