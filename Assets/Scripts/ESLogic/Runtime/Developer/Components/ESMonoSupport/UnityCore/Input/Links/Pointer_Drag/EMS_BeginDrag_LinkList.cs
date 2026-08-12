using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ES
{
    [AddComponentMenu("【ES】/UI/输入事件/开始拖动-可接收列表")]
    public class EMS_BeginDrag_LinkList : EMS_InputPointerEvent_LinkList_Abstract, IBeginDragHandler
    {
        public void OnBeginDrag(PointerEventData eventData)
        {
            SendLink( Channel_InputPointerEvent.BeginDrag ,new Link_InputPointerEvent() { eventData = eventData });
        }
    }
}
