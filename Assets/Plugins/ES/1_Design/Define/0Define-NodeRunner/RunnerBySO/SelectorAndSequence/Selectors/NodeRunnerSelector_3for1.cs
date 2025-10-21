using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ES {
    [CreateNodeRunnerSoMenu(NodeEnvironment.None,"筛选", "3选1")]
    public class NodeRunnerSelector_3for1 : NodeRunnerSelector_ConfirmNodes
    {
        public override int PortNum => 3;
        public override string GetOptionName()
        {
            return "3选1";
        }
    }
}
