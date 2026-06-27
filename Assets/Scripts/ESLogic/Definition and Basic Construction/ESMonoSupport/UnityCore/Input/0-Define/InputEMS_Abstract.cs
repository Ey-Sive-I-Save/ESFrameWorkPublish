using Sirenix.OdinInspector;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace ES
{
    [TypeRegistryItem("EMS-输入常规事件-单接收")]
    public class EMS_InputBaseEvent_LinkSingle_Abstarct : EMS_Abstract_ChannelLinkSingle<Channel_InputBaseEvent, Link_InputBaseEvent>
    {

    }
    [TypeRegistryItem("EMS-输入光标事件-单接收")]
    public class EMS_InputPointerEvent_LinkSingle_Abstarct : EMS_Abstract_ChannelLinkSingle<Channel_InputPointerEvent, Link_InputPointerEvent>
    {

    }
    [TypeRegistryItem("EMS-输入常规事件-接收列")]
    public class EMS_InputPointerEvent_LinkList_Abstarct : EMS_Abstract_ChannelLinkList<Channel_InputPointerEvent, Link_InputPointerEvent>
    {

    }
    [TypeRegistryItem("EMS-输入光标事件-接收列")]
    public class EMS_InputBaseEvent_LinkList_Abstarct : EMS_Abstract_ChannelLinkList<Channel_InputBaseEvent, Link_InputBaseEvent>
    {

    }
   

}
