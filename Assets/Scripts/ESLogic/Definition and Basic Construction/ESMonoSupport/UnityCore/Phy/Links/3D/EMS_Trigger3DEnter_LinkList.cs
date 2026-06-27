using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ES {
    [AddComponentMenu("<ES>ESM支持/物理/3D触发入-可接收列表")]
    public class EMS_Trigger3DEnter_LinkList : EMS_ColEvent_3D_LinkList_Abstract
    {
        private void OnTriggerEnter3D(Collider collider)
        {
            Links.SendLink(Channel_ColEvent.Enter, new Link_ColEvent_3D() { collider = collider, posAT = collider.ClosestPoint(transform.position) });
        }
    }
}
