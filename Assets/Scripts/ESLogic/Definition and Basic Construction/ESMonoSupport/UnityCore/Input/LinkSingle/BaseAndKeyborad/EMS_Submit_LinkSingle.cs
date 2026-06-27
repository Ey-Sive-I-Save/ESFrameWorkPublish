using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ES {
    [AddComponentMenu("<ES>ESM支持/输入/确定提交-指定接收目标")]
    public class EMS_Submit_LinkSingle : EMS_InputBaseEvent_LinkSingle_Abstarct, ISubmitHandler
    {

        public void OnSubmit(BaseEventData eventData)
        {
            Link_?.OnLink(Channel_InputBaseEvent.Submit, new Link_InputBaseEvent() { eventData = eventData });
        }
    }
}
