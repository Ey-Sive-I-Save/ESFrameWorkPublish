using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ES {
    [CreateNodeRunnerSoMenu(NodeEnvironment.None,"队列", "3单元队列")]
    public class NodeRunnerSequence_3Sequence : NodeSequnence_ConfirmNodes
    {
        public override int PortNum => 3;
        public override string GetOptionName()
        {
            return "3单元队列";
        }
    }
}
