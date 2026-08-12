using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ES
{
    [AddComponentMenu("【ES】/基础设施/物理事件/3D触发中-可接收列表")]
    public class EMS_Trigger3DStay_LinkList : EMS_ColEvent_3D_LinkList_Abstract
    {
        private void OnTriggerStay3D(Collider collider)
        {
            Links.SendLink(Channel_ColEvent.Stay, new Link_ColEvent_3D() { collider = collider, posAT = collider.ClosestPoint(transform.position) });
        }
    }
}
