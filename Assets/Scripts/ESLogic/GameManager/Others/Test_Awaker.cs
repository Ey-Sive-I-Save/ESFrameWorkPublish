using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ES {
    public class EventTest : GlobalLinker<Link_GameCenterAwakeBefoe>
    {
        public override void OnLink(Link_GameCenterAwakeBefoe link)
        {
            Debug.Log("应该在Awake之初执行");
        }
    }
}
