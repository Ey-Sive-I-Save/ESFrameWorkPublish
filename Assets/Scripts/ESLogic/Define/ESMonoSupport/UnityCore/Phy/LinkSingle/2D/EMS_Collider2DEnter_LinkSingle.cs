using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ES {
    [AddComponentMenu("<ES>ESM支持/物理/2D碰撞入-指定接收目标")]
    public class EMS_Collider2DEnter_LinkSingle : EMS_ColEvent_2D_LinkSingle_Abstract
    {
        private void OnCollisionEnter2D(Collision2D collision)
        {
            Link_?.OnLink(Channel_ColEvent.Enter, new Link_ColEvent_2D() { collider = collision.collider, posAT = collision.contacts[0].point });
        }
    }
}
