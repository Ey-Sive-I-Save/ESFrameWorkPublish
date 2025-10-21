using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ES {
    //相比于Target,不具备自动初始化功能，有输入端口，但是都可以应用Op上
    public abstract class NodeRunnerSupport_TargetSolver<On, Target,Contain> : NodeRunnerSupport_Target<On, Target,Contain> where Contain:NodeContainerSO
    {
        public override NodePort GetInputNode()
        {
            return new NodePort() { Name = "处理目标", IsMutiConnect = false };
        }

    }
}
