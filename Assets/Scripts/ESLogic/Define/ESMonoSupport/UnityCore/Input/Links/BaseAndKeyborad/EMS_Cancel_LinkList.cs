using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ES
{
    [AddComponentMenu("<ES>ESM支持/输入/取消-可接收列表")]
    public class EMS_Cancel_LinkList : EMS_Abstract_ChannelLinkList<Channel_InputBaseEvent,Link_InputBaseEvent>, ICancelHandler
    {
        public void OnCancel(BaseEventData eventData)
        {
            Links.SendLink( Channel_InputBaseEvent.Cancel,new Link_InputBaseEvent() { eventData = eventData });
        }
    }
}
