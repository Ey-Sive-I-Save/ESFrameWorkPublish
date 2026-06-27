using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ES
{
    [AddComponentMenu("<ES>ESM支持/输入/取消-指定接收目标")]
    public class EMS_Cancel_ListSingle : EMS_Abstract_ChannelLinkSingle<Channel_InputBaseEvent,Link_InputBaseEvent>, ICancelHandler
    {

        public void OnCancel(BaseEventData eventData)
        {
            Link_?.OnLink( Channel_InputBaseEvent.Cancel,new Link_InputBaseEvent() { eventData = eventData });
        }
    }
}
