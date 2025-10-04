using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ES {
    [AddComponentMenu("<ES>ESM支持/输入/轴输入移动-指定接收目标")]
    public class EMS_Move_LinkSingle : EMS_InputBaseEvent_LinkSingle_Abstarct, IMoveHandler
    {
        public void OnMove(AxisEventData eventData)
        {
            Link_?.OnLink(Channel_InputBaseEvent.Move, new Link_InputBaseEvent() { eventData = eventData });
        }
    }
}
