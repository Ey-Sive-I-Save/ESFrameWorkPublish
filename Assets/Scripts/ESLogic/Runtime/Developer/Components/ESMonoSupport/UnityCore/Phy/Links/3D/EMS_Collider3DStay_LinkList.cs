using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ES {
    [AddComponentMenu("【ES】/基础设施/物理事件/3D碰撞中-可接收列表")]
    public class EMS_Collider3DStay_LinkList : EMS_ColEvent_3D_LinkList_Abstract
    {
        private void OnCollisionStay3D(Collision collision)
        {
            Links.SendLink(Channel_ColEvent.Stay, new Link_ColEvent_3D() { collider = collision.collider, posAT = collision.contacts[0].point });
        }
    }
}
