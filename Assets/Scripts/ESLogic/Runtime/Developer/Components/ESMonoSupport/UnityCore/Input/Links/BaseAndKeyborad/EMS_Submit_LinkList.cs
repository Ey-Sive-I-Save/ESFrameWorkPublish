using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ES {
    [AddComponentMenu("【ES】/UI/输入事件/确定提交-可接收列表")]
    public class EMS_Submit_LinkList : EMS_InputBaseEvent_LinkList_Abstract, ISubmitHandler
    {
        public void OnSubmit(BaseEventData eventData)
        {
            Links.SendLink(  Channel_InputBaseEvent.Submit ,new Link_InputBaseEvent() { eventData = eventData });
        }
    }
}
