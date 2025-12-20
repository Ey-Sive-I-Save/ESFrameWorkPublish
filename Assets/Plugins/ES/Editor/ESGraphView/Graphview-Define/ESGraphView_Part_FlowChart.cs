using ES;
using ES.ES;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Schema;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using static UnityEditor.Experimental.GraphView.GraphView;
using static UnityEditor.Rendering.FilterWindow;
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
            this.AddManipulator(new SelectionDragger() { });
            var useRect = new RectangleSelector() { };
            useRect.target = this;

            this.AddManipulator(useRect);

            var USS = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Plugins/ES/Editor/ESGraphView/Graphview-Define/ESGraphViewWindow.uss");
            if (USS != null)
            {
                this.styleSheets.Add(USS);
            }

            #region 委托
            graphViewChanged += OnGraphViewChanged;
            this.RegisterCallback<KeyDownEvent>(SelfDefineCommandEvent, TrickleDown.TrickleDown);
            focusable = true;

            var menuWindowvalueEntry = ScriptableObject.CreateInstance<ESGraphViewSearchMenu>();

            menuWindowvalueEntry.OnSelectEntryHandler = OnMenuSelectEntry;

            nodeCreationRequest += context =>
            {
                SearchWindow.Open(new SearchWindowContext(context.screenMousePosition), menuWindowvalueEntry);
            };

            #endregion
        }

        private bool OnMenuSelectEntry(SearchTreeEntry searchTreeEntry, SearchWindowContext context)
        {
            if (searchTreeEntry.userData is Type t)
            {
                if (!t.IsAbstract)
                {

                    Rect layout = this.parent.layout;
                    Vector2 tran = this.contentContainer.transform.position;
                    var windowRoot = window.rootVisualElement;
                    // var pos = context.screenMousePosition - window.position.position - layout.position- tran; var windowRoot = window.rootVisualElement;
                    var windowMousePosition = windowRoot.ChangeCoordinatesTo(windowRoot.parent, context.screenMousePosition - window.position.position);
                    var graphMousePosition = contentViewContainer.WorldToLocal(windowMousePosition);

                    var pos = graphMousePosition;
                    // Debug.Log(" "++"  " + window.position.position +"  "+ layout.position + "   "+ tran + "  "+pos);
                    if (typeof(INodeRunner).IsAssignableFrom(t))
                    {
                        INodeRunner runner = null;
                        var node = CreateNodeForContainer(t, ref runner, pos);

                        if (runner is IDynamicRunner dr)
                        {
                            dr.InitValueByType(dr.DefaultValueType());
                            node.Refresh();
                        }

                    }



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
                var edgesToRemove = new List<Edge>(2);
                //对于每个被移除的目标
                graphViewChange.elementsToRemove.ForEach(elem =>
                {
                    if (elem is BaseNodeViewer viewer)
                    {
                        if (viewer.Runner != null)
                            window.Container.RemoveRunner(viewer.Runner);
                        foreach (var i in edges)
                        {
                            if (i.input.node == viewer || i.output.node == viewer)
                            {
                                edgesToRemove.Add(i);
                            }
                        }
                    }
                    else if (elem is Edge edge)
                    {
                        BaseNodeViewer output = edge.output.node as BaseNodeViewer;
                        BaseNodeViewer input = edge.input.node as BaseNodeViewer;
                        if (output != null)
                        {
                            var outputPorts = output.outputContainer.Children().OfType<Port>().ToList();
                            int index = outputPorts.IndexOf(edge.output);
                            if (input != null)
                            {
                                //输出 flows  移除 
                                output.Runner.RemoveFlow(input.Runner, index);
                            }
                        }
                    }
                });


                if (edgesToRemove.Count > 0)
                {
                    foreach (var edge in edgesToRemove)
                    {
                        RemoveEdge(edge);
                    }
                }
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
                        Debug.Log("创建边："+outputPorts.Count);
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
                if (n is BaseNodeViewer viewer)
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
            ClearAllSafe();
            ESNodeUtility.CacheMapping.Clear();
            ESNodeUtility.CacheMappingRefresh += (o, level, usedara) =>
            {
                if (o is BaseNodeViewer viewer)
                {
                    if (viewer != null)
                    {
                        if (level < 2) viewer.Refresh();
                        if (level == 5)
                        {
                            
                            //变输出端口行为
                            viewer.RefreshPorts();
                            var opPuts = viewer.outputContainer.Children().OfType<Port>().ToList();
                           
                            if (int.TryParse(usedara.ToString(), out int num))
                            {
                                num = Mathf.Clamp(num, 1, 10);
                                int off = opPuts.Count - num;
                               
                                if (off > 0)//太多了
                                {
                                    for(int i = opPuts.Count - 1; i >= 0&&off>0; i--)
                                    {
                                        var port = opPuts[i];
                                        viewer.outputContainer.Remove(port);
                                        viewer.Runner.ConfirmFlow(num);
                                        off--;
                                    }
                                }else if (off < 0)
                                {
                                    int c = opPuts.Count;
                                    for (int i = 0; i < -off; i++) {
                                        var port= viewer.GetPortForNode(viewer, Direction.Output);
                                        port.portName = "序号" + (c+i);
                                        viewer.outputContainer.Add(port);
                                    }
                                }
                                viewer.RefreshPorts();
                                viewer.Refresh();
                            };

                        }
                    }
                }

            };
            var list = window.Container.GetAllNodes();
            foreach (var nodeR in list)
            {
                //一个一个加节点
                CreateBaseNodeViewer(nodeR);
            }
            //连接节点
            CreateNodeEdge();
        }
        public void ClearAllSafe()
        {
            // 1. 收集所有节点（用于清理 Runtime 组件）
            var nodesToCleanup = nodes.OfType<BaseNodeViewer>().ToList();
            var edgesToRemove = edges.OfType<Edge>().ToList();

            // 2. 先删除边
            foreach (var edge in edgesToRemove)
            {
                RemoveElement(edge);
            }

            // 3. 删除节点
            foreach (var nodeView in nodesToCleanup)
            {

                RemoveElement(nodeView);
            }

            // 4. 清空选择
            ClearSelection();

            Debug.Log($"安全清空完成: {nodesToCleanup.Count} 个节点, {edgesToRemove.Count} 条边");
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
        private void RemoveEdge(Edge edge)
        {
            var outputNode = edge.output.node as BaseNodeViewer;
            var allOutPorts = outputNode.inputContainer.Children().OfType<Port>().ToList();
            var inputNode = edge.input.node as BaseNodeViewer;


            var tos = outputNode.Runner.GetFlowTo();
            if (tos == null || tos.Count() == 0) return;
            int index = 0;
            foreach (var portFlow in tos)
            {
                if (portFlow != null)
                {
                    if (allOutPorts.Count > index)
                    {
                        var outputPort = allOutPorts[index];
                        if (edge.output == outputPort)
                        {
                            outputNode.Runner.SetFlow(null, index);
                        }
                    }
                }
                index++;
            }
            RemoveElement(edge);
        }

        //复原节点操作
        private BaseNodeViewer CreateBaseNodeViewer(INodeRunner runner)
        {
            if (window?.Container == null || runner == null)
                return null;

            var nodeView = CreateNodeForContainer(runner.GetType(), ref runner);

            nodeView.RefreshExpandedState();
            nodeView.RefreshPorts();

            return nodeView;

        }

        private BaseNodeViewer CreateNodeForContainer(Type type, ref INodeRunner runner, Vector2 atPos = default)
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
                nodeView.SetPosition(new Rect(runner != null ? runner.Editor_GetPos() : atPos, nodeView.GetPosition().size + new Vector2(50, 50)));
                runner = nodeView.Runner = r;
                nodeView.Init();
                //添加和关联节点
                nodeView.OnNodeSelected = OnSelectNodeViewer;
                this.AddElement(nodeView);

                ESNodeUtility.CacheMapping[r] = nodeView;

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
            Dictionary<BaseNodeViewer, Port> inputPorts = new Dictionary<BaseNodeViewer, Port>();
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
                var flows = nodeR.GetFlowTo();
                if (flows == null || flows.Count() == 0) continue;
                var allPorts = outputPorts[map[nodeR]];
                int index = 0;
                foreach (var portFlow in flows)
                {
                    if (portFlow is UnityEngine.Object uo && uo == null) continue;
                    if (portFlow != null)
                    {
                        var nodeV = map[portFlow];
                        if (nodeV != null)
                        {
                            int current = nodeR.MutiLineOut ? 0 : index;
                            if (allPorts.Count > current)
                            {
                                var from = allPorts[current];
                                Port portto = inputPorts[map[portFlow]];
                                AddEdgeByPorts(from, portto);
                            }
                        }
                    }
                    index++;
                }
            }
        }

        public void SelfDefineCommandEvent(KeyDownEvent keyDown)
        {
            // 
            Debug.Log(keyDown.keyCode);
            if (keyDown.modifiers == EventModifiers.Control)
            {
                //复制
                if (keyDown.keyCode == KeyCode.C)
                {
                    ESEditorHandle.AddSimpleHanldeTask(() =>
                    {
                        DoAction_NodePaste();
                    }, 10, "NodePaste");
                    return;
                }

            }
            if (keyDown.keyCode == KeyCode.Delete)
            {
                ESEditorHandle.AddSimpleHanldeTask(() =>
                {
                    DoAction_NodeDelete();
                }, 10, "NodeDelete");
            }
        }
        public void OnExecuteValidateCommandEvent(ValidateCommandEvent evt)
        {
            if (evt is ValidateCommandEvent commandEvent)
            {
                Debug.Log("Event:");
                Debug.Log(commandEvent.commandName);
                //限制一下0.2s执行一次  不然短时间会多次执行
                if (commandEvent.commandName.Equals("Paste"))
                {
                    ESEditorHandle.AddSimpleHanldeTask(() =>
                    {
                        DoAction_NodePaste();
                    }, 10, "NodePaste");
                }
            }
        }
        public void OnExecuteCommandEvent(ExecuteCommandEvent evt)
        {
            Debug.Log("Event:");
            Debug.Log(evt.commandName);
            if (evt is ExecuteCommandEvent commandEvent)
            {

            }
        }
        private void DoAction_NodePaste()
        {
            var nodesDict = new Dictionary<BaseNodeViewer, BaseNodeViewer>(); //新旧Node对照

            foreach (var selectable in selection)
            {
                var offset = 1;
                if (selectable is BaseNodeViewer fromViewer)
                {
                    offset++;

                    var runnerC = window.Container.CopyNodeRunner(fromViewer.Runner);
                    var nodeView = CreateBaseNodeViewer(runnerC);

                    //新旧节点映射
                    if (nodeView != null)
                    {
                        nodesDict.Add(fromViewer, nodeView);
                    }

                    //调整一下流向
                    //保持原来的流向算法好难写，还是全部设置成null把
                    //复制出来的节点位置偏移
                    nodeView.SetPosition(new Rect(fromViewer.GetPosition().position + (Vector2.one * 30 * offset), nodeView.GetPosition().size));
                }
            }

            for (int i = selection.Count - 1; i >= 0; i--)
            {
                //取消选择
                this.RemoveFromSelection(selection[i]);
            }

            foreach (var node in nodesDict.Values)
            {
                //选择新生成的节点
                this.AddToSelection(node);
            }
        }
        private void DoAction_NodeDelete()
        {
            foreach (var selectable in selection)
            {
                if (selectable is BaseNodeViewer fromViewer)
                {
                    RemoveElement(fromViewer);

                }
            }
            for (int i = selection.Count - 1; i >= 0; i--)
            {
                //取消选择
                this.RemoveFromSelection(selection[i]);
            }
        }


    }
}
