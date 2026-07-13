using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace ES
{
    public abstract class NodeSequence : NodeRunnerSO
    {

        public override NodePort GetInputNode()
        {
            return new NodePort() { Name = "序列", IsMultiConnect = false };
        }

        public override string GetTitle()
        {
            return "<序列>" + GetOptionName();
        }
        public abstract string GetOptionName();
    }

    public abstract class NodeSequence_ConfirmNodes : NodeSequence
    {
        public virtual int PortNum => 2;
        public override List<NodePort> GetOutputNodes()
        {
            var list = new List<NodePort>(PortNum);
            for (int i = 0; i < PortNum; i++)
            {
                list.Add(new NodePort() { Name = "序号" + i, IsMultiConnect = false });
            }
            return list;
        }
    }
    public abstract class NodeSequence_MultiLines : NodeSequence
    {
        public override bool MultiLineOut => true;
        public override List<NodePort> GetOutputNodes()
        {
            var list = new List<NodePort>(1);
            list.Add(new NodePort() { Name = "多通", IsMultiConnect = true });
            return list;
        }
    }

}
