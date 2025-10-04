using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ES {
    [AddComponentMenu("<ES>ESM支持/物理/2D碰撞中-可接收列表")]
    public class EMS_Collider2DStay_LinkList : EMS_ColEvent_2D_LinkList_Abstract
    {
        private void OnCollisionStay2D(Collision2D collision)
        {
            Links.SendLink(Channel_ColEvent.Stay, new Link_ColEvent_2D() { collider = collision.collider, posAT = collision.contacts[0].point });
        }
    }
}
