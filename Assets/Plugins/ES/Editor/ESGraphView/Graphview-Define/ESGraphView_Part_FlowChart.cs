using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using static UnityEditor.Experimental.GraphView.GraphView;
using static UnityEngine.Rendering.DebugUI;

namespace ES
{


    public class ESGraphView_Part_FlowChart : GraphView
    {
        public new class UxmlFactory : UxmlFactory<ESGraphView_Part_FlowChart, GraphView.UxmlTraits> { }

        public ESGraphView_Part_FlowChart()
        {
            var grid = new GridBackground();
            grid.name = grid.GetType().ToString();
            Insert(0, grid);
            
            this.AddManipulator(new ContentZoomer());
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());

            var USS = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Plugins/ES/Editor/ESGraphView/Graphview-Define/ESGraphViewWindow.uss");
            if (USS != null)
            {
                this.styleSheets.Add(USS);
            }

            #region 委托
            graphViewChanged += OnGraphViewChanged;


            #endregion

            //新建搜索菜单
            var menuWindowProvider = ScriptableObject.CreateInstance<ESGraphViewSearchMenu>();
            menuWindowProvider.OnSelectEntryHandler = OnMenuSelectEntry;

            nodeCreationRequest += context =>
            {
                SearchWindow.Open(new SearchWindowContext(context.screenMousePosition), menuWindowProvider);
            };

        }

        private bool OnMenuSelectEntry(SearchTreeEntry searchTreeEntry, SearchWindowContext context)
        {
            Debug.Log("TTTT");
            if(searchTreeEntry.userData is Type t)
            {
                if (!t.IsAbstract)
                {
                    CreateNodeForContainer(t,null,context.screenMousePosition-window.position.position-new Vector2(base.resolvedStyle.left, base.resolvedStyle.top));
                    return true;
                }
            }
            return true;
        }

        private GraphViewChange OnGraphViewChanged(GraphViewChange graphViewChange)
        {
            if (window?.Container == null) return graphViewChange;
            //移除节点或者边的事件
            if (graphViewChange.elementsToRemove != null)
            {
                //对于每个被移除的目标
                graphViewChange.elementsToRemove.ForEach(elem =>
                {
                    if(elem is BaseNodeViewer viewer)
                    {
                        if(viewer.Runner!=null)
                        window.Container.RemoveRunner(viewer.Runner);
                    }else if(elem is Edge edge)
                    {
                        BaseNodeViewer output = edge.output.node as BaseNodeViewer;
                        BaseNodeViewer input = edge.input.node as BaseNodeViewer;

                        var outputPorts = output.outputContainer.Children().OfType<Port>().ToList();
                        int index = outputPorts.IndexOf(edge.output);
                        if (output != null && input != null)
                        {
                            //输出 flows  移除 
                            output.Runner.RemoveFlow(input.Runner,index);
                        }
                    }
                });
            }
            //对于每个被创建的边--节点不需要
            if (graphViewChange.edgesToCreate != null)
            {
                graphViewChange.edgesToCreate.ForEach(edge =>
                {
                    if (edge != null)
                    {
                        BaseNodeViewer output = edge.output.node as BaseNodeViewer;
                        BaseNodeViewer input = edge.input.node as BaseNodeViewer;

                        var outputPorts = output.outputContainer.Children().OfType<Port>().ToList();
                        int index = outputPorts.IndexOf(edge.output);
                        if (output != null && input != null)
                        {
                            output.Runner.SetFlow(input.Runner, index);
                        }

                    }
                });
            }
            //遍历节点，记录位置点
            nodes.ForEach((n) =>
            {
                if(n is BaseNodeViewer viewer)
                {
                    if (viewer.Runner != null)
                    {
                        viewer.Runner.Editor_SetPos(viewer.GetPosition().position);
                    }
                }
            });

            return graphViewChange;
        }

        public ESGraphViewWindow window;
        public Action<BaseNodeViewer> OnSelectNodeViewer;

        public void ResetNodeViewers()
        {
            if (window?.Container == null)
                return;

            var list = window.Container.GetAllNodes();
            foreach (var nodeR in list)
            {
                //一个一个加节点
                CreateBaseNodeView(nodeR);
            }
            //连接节点
            CreateNodeEdge();
        }


        //筛选可用的Port--只是一个初步筛选
        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            return ports.Where(endPort =>
                endPort.direction != startPort.direction &&
                endPort.node != startPort.node).ToList();
        }

        //连接两个点
        private void AddEdgeByPorts(Port _outputPort, Port _inputPort)
        {
            if (_outputPort.node == _inputPort.node)
                return;

            Edge tempEdge = new Edge()
            {
                output = _outputPort,
                input = _inputPort
            };
            tempEdge.input.Connect(tempEdge);
            tempEdge.output.Connect(tempEdge);
            Add(tempEdge);
        }


        //复原节点操作
        private void CreateBaseNodeView(INodeRunner runner)
        {
            if (window?.Container == null||runner==null)
                return;

            var nodeView = CreateNodeForContainer(runner.GetType(),runner);

            nodeView.RefreshExpandedState();
            nodeView.RefreshPorts();

        }

        private BaseNodeViewer CreateNodeForContainer(Type type,INodeRunner runner=null, Vector2 atPos = default)
        {
            if (window?.Container == null)
                return null;
            BaseNodeViewer<INodeRunner> nodeView = new BaseNodeViewer<INodeRunner>();
            if (nodeView == null)
            {
                Debug.LogError("节点未找到对应属性的NodeView");
                return null;
            }

            //创建出来
            var r = runner != null ? runner : window.Container.AddNodeByType(type);
            if (r != null)
            {
                nodeView.Runner = r;
                nodeView.Init();
                //添加和关联节点
                nodeView.OnNodeSelected = OnSelectNodeViewer;
                nodeView.SetPosition(new Rect(runner != null ? runner.Editor_GetPos() : atPos, nodeView.GetPosition().size+new Vector2(50,50)));
                this.AddElement(nodeView);
            }
            return nodeView;
        }

        //复原节点的边
        private void CreateNodeEdge()
        {
            if (window.Container == null)
                return;

            //这里有点像图的邻接表
            Dictionary<INodeRunner, BaseNodeViewer> map = new Dictionary<INodeRunner, BaseNodeViewer>();
            Dictionary<BaseNodeViewer, Port> inputPorts = new Dictionary<BaseNodeViewer,Port>();
            Dictionary<BaseNodeViewer, List<Port>> outputPorts = new Dictionary<BaseNodeViewer, List<Port>>();

            ports.ForEach(x =>
            {
                var y = x.node;
                var node = y as BaseNodeViewer;
                //runner->viewer全映射
                if (!map.ContainsKey(node.Runner))
                {
                    map.Add(node.Runner, node);
                }
                //添加端口集
                if (!inputPorts.ContainsKey(node))
                {
                    inputPorts.Add(node, x);
                }
                if (!outputPorts.ContainsKey(node))
                {
                    outputPorts.Add(node, new List<Port>());
                }
                if (x.direction == Direction.Output)
                    outputPorts[node].Add(x);
            });

            //只负责连接下面的节点
            foreach (var nodeR in map.Keys)
            {
                var tos = nodeR.GetFlowTo();
                if (tos == null || tos.Count() == 0) continue;
                var allPorts = outputPorts[map[nodeR]];
                int index = 0;
                foreach(var portFlow in tos)
                {
                    if (portFlow != null)
                    {
                        if (allPorts.Count > index)
                        {
                            var from = allPorts[index];
                            Port portto = inputPorts[map[portFlow]];
                            AddEdgeByPorts(from, portto);
                        }
                    }
                    index++;
                }
            }
        }
    }
}

