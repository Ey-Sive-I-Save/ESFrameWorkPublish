using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ES {
    public abstract class NodeRunnerSupport_Target<On,Target,Contain> : NodeRunnerDynamicSO<Target> where Contain:NodeContainerSO
    {
        public On on;
        public List<On> onS = new List<On>();
        public override NodePort GetInputNode()
        {
            return null;//没有入端
        }
        

    }
}
