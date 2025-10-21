using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ES {
    [CreateNodeRunnerSoMenu(NodeEnvironment.None, "筛选", "2选1")]
    public class NodeRunnerSelector_2for1 : NodeRunnerSelector_ConfirmNodes
    {
        public override string GetOptionName()
        {
            return "2选1";
        }
    }
}
