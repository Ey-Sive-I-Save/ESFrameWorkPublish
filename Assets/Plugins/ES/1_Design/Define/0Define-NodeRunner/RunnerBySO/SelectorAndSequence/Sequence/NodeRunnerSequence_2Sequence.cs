using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ES {
    [CreateNodeRunnerSoMenu(NodeEnvironment.None, "队列", "2单元队列")]
    public class NodeRunnerSelector_2Sequence : NodeSequnence_ConfirmNodes
    {
        public override string GetOptionName()
        {
            return "2单元队列";
        }
    }
}
