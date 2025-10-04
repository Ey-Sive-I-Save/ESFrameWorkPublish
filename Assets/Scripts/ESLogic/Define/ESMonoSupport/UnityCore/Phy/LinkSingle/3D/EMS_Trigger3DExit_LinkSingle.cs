using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ES {
    [AddComponentMenu("<ES>ESM支持/物理/3D触发出-指定接收目标")]
    public class EMS_Trigger3DExit_LinkSingle : EMS_ColEvent_3D_LinkSingle_Abstract
    {
        private void OnTriggerExit3D(Collider collider3D)
        {
            OnLink(Channel_ColEvent.Exit, new Link_ColEvent_3D() { collider = collider3D, posAT = collider3D.ClosestPoint(transform.position) });
        }
    }
}
