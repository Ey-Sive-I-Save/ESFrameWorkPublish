using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ES {
    [CreateNodeRunnerSoMenu(NodeEnvironment.None,"筛选", "随机多选1")]
    public class NodeRunnerSelector_Nfor1 : NodeRunnerSelector_MutiLines
    {
        public override int SelectNum => 1;
        public override string GetOptionName()
        {
            return "随机多选1";
        }
    }
}
