using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ES {
    [CreateNodeRunnerSoMenu(NodeEnvironment.None,"筛选", "无序队列")]
    public class NodeRunnerSequence_NoList : NodeSequnence_MutiLines
    {
       
        public override string GetOptionName()
        {
            return "无序队列";
        }
    }
}
