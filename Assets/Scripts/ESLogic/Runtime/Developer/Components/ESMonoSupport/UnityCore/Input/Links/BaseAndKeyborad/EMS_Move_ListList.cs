using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ES {
    [AddComponentMenu("【ES】/UI/输入事件/轴输入移动-可接收列表")] 
    public class EMS_Move_LinkList : EMS_InputBaseEvent_LinkList_Abstract, IMoveHandler
    {
        
        public void OnMove(AxisEventData eventData)
        {
            SendLink( Channel_InputBaseEvent.Move,new Link_InputBaseEvent() { eventData=eventData });
        }
    }
}
