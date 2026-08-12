using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ES
{
    [AddComponentMenu("【ES】/UI/输入事件/选择-可接收列表")]
    public class EMS_Select : EMS_InputBaseEvent_LinkList_Abstract, ISelectHandler
    {


        public void OnSelect(BaseEventData eventData)
        {
            Links.SendLink( Channel_InputBaseEvent.Select,new Link_InputBaseEvent() { eventData = eventData });
        }
    }
}
