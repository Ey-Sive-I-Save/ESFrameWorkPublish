using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ES {
    [AddComponentMenu("<ES>ESM支持/物理/2D触发入-指定接收目标")]
    public class EMS_Trigger2DEnter_LinkSingle : EMS_ColEvent_2D_LinkSingle_Abstract
    {
        private void OnTriggerEnter2D(Collider2D collider2D)
        {
            Link_?.OnLink(Channel_ColEvent.Enter, new Link_ColEvent_2D() { collider = collider2D, posAT = collider2D.ClosestPoint(transform.position) }); ;
        }
    }
}
