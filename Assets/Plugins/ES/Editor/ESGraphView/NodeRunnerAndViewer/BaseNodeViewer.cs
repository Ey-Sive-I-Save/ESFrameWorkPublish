using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace ES
{
    //原始抽象版本
    public abstract class BaseNodeViewer : UnityEditor.Experimental.GraphView.Node
    {
        /// <summary>
        /// 点击该节点时被调用的事件，比如转发该节点信息到Inspector中显示
        /// </summary>
        public Action<BaseNodeViewer> OnNodeSelected;

        public TextField textField;
        public string GUID;

        public BaseNodeViewer() : base()
        {
            textField = new TextField();
            GUID = Guid.NewGuid().ToString();

        }
        // 为节点n创建input port或者output port
        // Direction: 是一个简单的枚举，分为Input和Output两种
        public Port GetPortForNode(BaseNodeViewer viewer, Direction portDir, Port.Capacity capacity = Port.Capacity.Single)
        {
            return viewer.InstantiatePort(Orientation.Horizontal, portDir, capacity, typeof(bool));
        }

        //告诉Inspector去绘制该节点
        public override void OnSelected()
        {
            base.OnSelected();
            Debug.Log($"{this.name}节点被点击");
            OnNodeSelected?.Invoke(this);
        }

        public abstract INodeRunner Runner { get; set; }

    }
    //泛型继承
    public class BaseNodeViewer<TRunner> : BaseNodeViewer where TRunner : INodeRunner
    {
        /// <summary>
        /// 关联的State
        /// </summary>
        private TRunner _runner;
        private List<NodePort> inputs;
        public List<NodePort> outputs;
        public override INodeRunner Runner
        {
            get
            {
                return _runner;
            }
            set
            {
                if (_runner != null)
                {
                    /* _runner.node = null;*/
                }

                _runner = (TRunner)value;
            }
        }
        bool hasNodeViewInit = false;
        public void Init()
        {
            if (hasNodeViewInit) return;
            OnCreateGUI();
        }
        protected virtual void OnCreateGUI()
        {
            if (Runner != null)
            {
                //入
                var input = Runner.GetInputNode();
                if (input != null)
                {
                    Port part = GetPortForNode(this, Direction.Input, input.IsMutiConnect ? Port.Capacity.Multi : Port.Capacity.Single);
                    part.portName = input.Name;
                    this.contentContainer.Add(part);
                }
                /*   inputs = Runner.GetInputNodes();
                   if (inputs != null) {
                       foreach (var input in inputs)
                       {
                           Port part = GetPortForNode(this, Direction.Input, input.IsMutiConnect ? Port.Capacity.Multi: Port.Capacity.Single);
                           part.portName = input.Name;
                           this.contentContainer.Add(part);
                       }
                   }*/

                //出
                Debug.Log("OUT");
                outputs = Runner.GetOutputNodes();
                if (outputs != null)
                {
                    foreach (var output in outputs)
                    {
                        Debug.Log("OUT2");
                        Port part = GetPortForNode(this, Direction.Output, output.IsMutiConnect ? Port.Capacity.Multi : Port.Capacity.Single);
                        part.portName = output.Name;
                        this.contentContainer.Add(part);
                    }
                }

                this.title = Runner.GetTitle();
            }
        }

        void aTest()
        {

        }
    }
}
