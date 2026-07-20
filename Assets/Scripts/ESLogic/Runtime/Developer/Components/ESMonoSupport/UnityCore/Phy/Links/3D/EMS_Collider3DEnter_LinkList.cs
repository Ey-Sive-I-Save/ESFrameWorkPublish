using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ES {
    [AddComponentMenu("<ES>ESM支持/物理/3D碰撞入-可接收列表")]
    public class EMS_Collider3DEnter_LinkList : EMS_ColEvent_3D_LinkList_Abstract
    {
        private void OnCollisionEnter3D(Collision collision)
        {
            Links.SendLink(Channel_ColEvent.Enter, new Link_ColEvent_3D() { collider = collision.collider, posAT = collision.contacts[0].point });
        }
    }
}
