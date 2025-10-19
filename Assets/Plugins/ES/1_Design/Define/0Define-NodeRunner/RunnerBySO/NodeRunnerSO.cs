using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace ES
{
    /*
    [CreateNodeRunnerSoMenu(NodeEnvironment.None, "组","测试")]

     */

    public abstract class NodeRunnerSO : ScriptableObject, INodeRunner
    {
        [ToggleGroup("inlineData", "内置数据")]
        [SerializeReference]
        public List<NodeRunnerSO> Flows = new List<NodeRunnerSO>();
        [SerializeField]
        [ToggleGroup("inlineData", "内置数据")]
        public Vector2 nodePos;
        [ToggleGroup("inlineData", "内置数据")]
        public bool inlineData;


        public void SetFlow(INodeRunner runner, int index = 0)
        {
            if (runner is NodeRunnerSO no)
            {
                index = Mathf.Max(0, index);
                if (index < Flows.Count)
                {
                    Flows[index] = no;
                }
                else
                {
                    for (int i = Flows.Count; i < index; i++)
                    {
                        Flows.Add(null); // 或用默认值填充
                    }
                    Flows.Insert(index, no);
                }

            }

        }
        public void RemoveFlow(INodeRunner runner, int index = 0)
        {
            index = Mathf.Max(0, index);
            if (runner is NodeRunnerSO no)
            {
                if (index < Flows.Count)
                {
                    if (Flows[index] == no)
                    {
                        Flows[index] = null;
                        return;
                    };
                }
                Flows.Remove(no);
            }
            else
            {
                if (index < Flows.Count)
                {
                    Flows[index] = null;
                }
            }


        }
        public Vector2 Editor_GetPos()
        {
            return nodePos;
        }

        public void Editor_SetPos(Vector2 vector)
        {
            nodePos = vector;
        }

        public void Execute()
        {
            OnEnter();
        }

        public IEnumerable<INodeRunner> GetFlowTo()
        {
            return Flows;
        }

        public abstract NodePort GetInputNode();

        public abstract List<NodePort> GetOutputNodes();

        public abstract string GetTitle();

        public virtual void OnEnter()
        {

        }

        public virtual void OnExit()
        {

        }

        public virtual void OnRunning()
        {

        }




    }


    public class CreateNodeRunnerSoMenuAttribute : Attribute
    {
        public NodeEnvironment environment;
        public string Group;
        public string Name;
        public CreateNodeRunnerSoMenuAttribute(NodeEnvironment environment, string group, string name)
        {
            this.environment = environment;
            this.Group = group;
            Name = name;
        }
    }
}
