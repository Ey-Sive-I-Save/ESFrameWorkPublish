using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ES
{
    [CreateNodeRunnerSoMenu(NodeEnvironment.Test,"组","测试1")]
    public class NodeTest1 : NodeRunnerSO
    {
        public override NodePort GetInputNode()
        {
            NodePort nodePort = new NodePort();
            nodePort.Name = "test";
            return nodePort;
        } 

        public override List<NodePort> GetOutputNodes()
        {
            List<NodePort> nodePorts = new List<NodePort>();
            nodePorts.Add(new NodePort() { Name = "testOU" });
            return nodePorts;
        }

        public override string GetTitle()
        {
            return "测试1";
        }
    }
}
