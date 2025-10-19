using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ES
{
    [CreateNodeRunnerSoMenu(NodeEnvironment.Test,"组","测试2")]
    public class NodeTest12 : NodeRunnerSO
    {
        public override NodePort GetInputNode()
        {
            NodePort nodePort = new NodePort();
            nodePort.Name = "test2";
            return nodePort;
        }

        public override List<NodePort> GetOutputNodes()
        {
            List<NodePort> nodePorts = new List<NodePort>();
            nodePorts.Add(new NodePort() { Name = "testOU2" });
            return nodePorts;
        }

        public override string GetTitle()
        {
            return "测试1";
        }
    }
}
