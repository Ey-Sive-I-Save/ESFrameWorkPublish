using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ES {
    [AddComponentMenu("【ES】/UI/输入事件/轴输入移动-指定接收目标")]
    public class EMS_Move_LinkSingle : EMS_InputBaseEvent_LinkSingle_Abstract, IMoveHandler
    {
        public void OnMove(AxisEventData eventData)
        {
            Link_?.OnLink(Channel_InputBaseEvent.Move, new Link_InputBaseEvent() { eventData = eventData });
        }
    }
}
