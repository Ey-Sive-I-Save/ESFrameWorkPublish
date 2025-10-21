using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace ES
{
    public abstract class NodeSequnence : NodeRunnerSO
    {

        public override NodePort GetInputNode()
        {
            return new NodePort() { Name = "序列", IsMutiConnect = false };
        }

        public override string GetTitle()
        {
            return "<序列>" + GetOptionName();
        }
        public abstract string GetOptionName();
    }

    public abstract class NodeSequnence_ConfirmNodes : NodeSequnence
    {
        public virtual int PortNum => 2;
        public override List<NodePort> GetOutputNodes()
        {
            var list = new List<NodePort>(PortNum);
            for (int i = 0; i < PortNum; i++)
            {
                list.Add(new NodePort() { Name = "序号" + i, IsMutiConnect = false });
            }
            return list;
        }
    }
    public abstract class NodeSequnence_MutiLines : NodeSequnence
    {
        public override bool MutiLineOut => true;
        public override List<NodePort> GetOutputNodes()
        {
            var list = new List<NodePort>(1);
            list.Add(new NodePort() { Name = "多通", IsMutiConnect = true });
            return list;
        }
    }

}
