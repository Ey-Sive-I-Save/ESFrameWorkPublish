using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ES {
    [AddComponentMenu("<ES>ESM支持/物理/2D触发出-可接收列表")]
    public class EMS_Trigger2DExit_LinkList : EMS_ColEvent_2D_LinkList_Abstract
    {
        private void OnTriggerExit2D(Collider2D collider)
        {
            Links.SendLink(Channel_ColEvent.Exit, new Link_ColEvent_2D() { collider = collider, posAT = collider.ClosestPoint(transform.position) });
        }
    }
}
