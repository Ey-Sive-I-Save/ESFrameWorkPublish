using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ES
{
    [AddComponentMenu("<ES>ESM支持/输入/滚轮滚动-可接收列表")]
    public class EMS_Scroll
        : EMS_Abstract_ChannelLinkList<Channel_InputBaseEvent,Link_InputAxisEvent>, IScrollHandler
    {

        public void OnScroll(PointerEventData eventData)
        {
            SendLink( Channel_InputBaseEvent.Axis_Move, new Link_InputAxisEvent() { eventData = eventData });

        }
    }
}
