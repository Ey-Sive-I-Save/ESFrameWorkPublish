using ES;
 
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ES {
    [AddComponentMenu("<ES>ESM支持/物理/3D碰撞入-指定接收目标")]
    public class EMS_Collider3DEnter_LinkSingle : EMS_ColEvent_3D_LinkSingle_Abstract
    {
        private void OnCollisionEnter(Collision collision)
        {
            Debug.Log("Hanppen：" + Channel_ColEvent.Enter + " by " );
            OnLink(Channel_ColEvent.Enter, new Link_ColEvent_3D() { collider = collision.collider, posAT = collision.contacts[0].point });
        }
    }
}
