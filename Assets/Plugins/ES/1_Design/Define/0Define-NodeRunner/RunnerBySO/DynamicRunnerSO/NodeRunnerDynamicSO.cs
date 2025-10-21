using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using UnityEngine;


namespace ES
{
    public interface IDynamicRunner
    {
        public void InitValueByType(Type t);
        public Type DefaultValueType();
    }
    public abstract class NodeRunnerDynamicSO<T> : NodeRunnerSO, IDynamicRunner
    {
        [SerializeReference, LabelText("动态值")]
        public T Value;
        private void OnValidate()
        {
#if UNITY_EDITOR
            if(ESNodeUtility.CacheMapping.TryGetValue(this,out var o))
            {
                ESNodeUtility.CacheMappingRefresh?.Invoke(o,1,null);
            }
#endif
        }

        public override NodePort GetInputNode()
        {
            if (Option.HasFlag(DyncmicRunnerPortsOption.NoInput))
            {
                return null;
            }
            NodePort nodePort = new NodePort();
            nodePort.Name = "输入";
            return nodePort;
        }

        public override List<NodePort> GetOutputNodes()
        {
            List<NodePort> outputNodes = new List<NodePort>();
            if (Option.HasFlag(DyncmicRunnerPortsOption.NoOutput))
            {
                return outputNodes;
            }
            outputNodes.Add(new NodePort() { Name = "输出" });
            return outputNodes;
        }

        public override string GetTitle()
        {
            return "<动态>" + GetNameForT();
        }

        public abstract string GetNameForT();
        public virtual void InitValueByType(Type t)
        {
            Debug.Log("初始化"+t);
            if (t.IsAbstract) return;
            Value = (T)Activator.CreateInstance(t);
        }

        public abstract Type DefaultValueType();
        public virtual DyncmicRunnerPortsOption Option => DyncmicRunnerPortsOption.None;
    }
    [Flags]
    public enum DyncmicRunnerPortsOption
    {
        None = 0,
        NoInput = 1,
        NoOutput = 2,

    }
}
