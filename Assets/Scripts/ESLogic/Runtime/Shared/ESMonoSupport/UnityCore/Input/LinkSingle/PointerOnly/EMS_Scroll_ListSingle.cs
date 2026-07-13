using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ES
{
    [AddComponentMenu("<ES>ESM支持/输入/滚轮滚动-指定接收目标")]
    public class EMS_Scroll_LinkList : EMS_Abstract_ChannelLinkSingle<Channel_InputBaseEvent, Link_InputAxisEvent>, IScrollHandler
    {
        public void OnScroll(PointerEventData eventData)
        {
            SendLink( Channel_InputBaseEvent.Axis_Move,new Link_InputAxisEvent() { eventData = eventData });
        }
    }
}
