using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{

    public abstract class NodeRunnerSelector : NodeRunnerSO
    {
        public override NodePort GetInputNode()
        {
            return new NodePort() { Name = "筛选", IsMultiConnect = false };
        }

        public override string GetTitle()
        {
            return "<筛选>" + GetOptionName();
        }
        public abstract string GetOptionName();
    }

    public abstract class NodeRunnerSelector_ConfirmNodes : NodeRunnerSelector
    {
        public virtual int PortNum => 2;
        public override List<NodePort> GetOutputNodes()
        {
            var list = new List<NodePort>(PortNum);
            for (int i = 0; i < PortNum; i++)
            {
                list.Add(new NodePort() { Name = "通" + i, IsMultiConnect = false });
            }
            return list;
        }
    }
    public abstract class NodeRunnerSelector_MultiLines : NodeRunnerSelector
    {
        public override bool MultiLineOut => true;
        public virtual int SelectNum => 2;
        public override List<NodePort> GetOutputNodes()
        {
            var list = new List<NodePort>(1);
            list.Add(new NodePort() { Name = "多通", IsMultiConnect = true });
            return list;
        }
    }
}

