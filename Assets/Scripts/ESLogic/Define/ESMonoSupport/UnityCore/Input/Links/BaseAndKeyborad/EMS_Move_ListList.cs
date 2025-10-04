using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ES {
    [AddComponentMenu("<ES>ESM支持/输入/轴输入移动-可接收列表")] 
    public class EMS_Move_LinkList : EMS_InputBaseEvent_LinkList_Abstarct, IMoveHandler
    {
        
        public void OnMove(AxisEventData eventData)
        {
            SendLink( Channel_InputBaseEvent.Move,new Link_InputBaseEvent() { eventData=eventData });
        }
    }
}
