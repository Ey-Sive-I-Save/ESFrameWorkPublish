#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ES.EditorInternal;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace ES.Tests
{
    public sealed class ESGraphAssetTests
    {
        public sealed class ESTestGraphAsset : ESGraphAssetBase
        {
            private ESGraphDomainKey testDomain = ESGraphDomainKey.FromKind(ESGraphDomainKind.Generic);

            public override ESGraphDomainKey DomainKey => testDomain;

            public bool InitializeTestDomain(ESGraphDomainKey value, out string error)
            {
                return InitializeTestDomain(value.StableId, out error);
            }

            public bool InitializeTestDomain(string value, out string error)
            {
                value = value?.Trim();
                if (!ESGraphStableIdUtility.IsValid(value))
                {
                    error = "测试 DomainId 不合法。";
                    return false;
                }
                if (Nodes.Count > 0 || Edges.Count > 0)
                {
                    error = "测试图已有内容，不能重新初始化 Domain。";
                    return false;
                }
                testDomain = ESGraphDomainKey.Parse(value);
                error = null;
                return true;
            }
        }

        private static readonly ESGraphPortDefinition[] DefaultPorts =
        {
            new ESGraphPortDefinition("输入", ESGraphBuiltInPortKeys.Input,
                ESGraphPortDirection.Input, ESGraphPortCapacity.Single),
            new ESGraphPortDefinition("输出", ESGraphBuiltInPortKeys.Output,
                ESGraphPortDirection.Output, ESGraphPortCapacity.Multi)
        };

        [Test]
        public void StableGraph_NodeEndpointLookupRequiresOneExactStableKey()
        {
            var node = new ESGraphNodeRecord
            {
                nodeId = ESGraphIdentity.NewId(),
                ports = new List<ESGraphPortRecord>
                {
                    new ESGraphPortRecord { portId = ESGraphIdentity.NewId(), stableKey = "result.success" },
                    new ESGraphPortRecord { portId = ESGraphIdentity.NewId(), stableKey = "result.failure" }
                }
            };

            Assert.That(node.TryGetPort("result.success", out ESGraphPortRecord success), Is.True);
            Assert.That(success.stableKey, Is.EqualTo("result.success"));
            Assert.That(node.TryGetPort("result.missing", out _), Is.False);

            node.ports.Add(new ESGraphPortRecord
            {
                portId = ESGraphIdentity.NewId(),
                stableKey = "result.success"
            });
            Assert.That(node.TryGetPort("result.success", out ESGraphPortRecord duplicate), Is.False);
            Assert.That(duplicate, Is.Null);
        }

        [Test]
        public void StableGraph_ConcreteAssetTypesOwnImmutableDomains()
        {
            ESGraphAssetBase[] assets =
            {
                ScriptableObject.CreateInstance<ESGenericGraphAsset>(),
                ScriptableObject.CreateInstance<ESStoryGraphAsset>(),
                ScriptableObject.CreateInstance<ESBehaviorTreeGraphAsset>(),
                ScriptableObject.CreateInstance<ESAgentAuthoringGraphAsset>()
            };
            string[] expected =
            {
                ESGraphDomainIds.Generic,
                ESGraphDomainIds.Story,
                ESGraphDomainIds.BehaviorTree,
                ESAgentGraphStableIds.DomainId
            };
            try
            {
                for (int i = 0; i < assets.Length; i++)
                {
                    Assert.That(assets[i].DomainId, Is.EqualTo(expected[i]));
                    Assert.That(assets[i].GetType().IsSealed, Is.True);
                }
                Assert.That(typeof(ESGraphAssetBase).GetProperty(nameof(ESGraphAssetBase.DomainKey))
                    ?.CanWrite, Is.False);
            }
            finally
            {
                for (int i = 0; i < assets.Length; i++)
                    Object.DestroyImmediate(assets[i]);
            }
        }

        [Test]
        public void StableGraph_AgentArtifactHandlersUseDistinctStableAttributes()
        {
            var command = (ESAgentArtifactAttribute)Attribute.GetCustomAttribute(
                typeof(ESAgentAICommandOutputPayload), typeof(ESAgentArtifactAttribute));
            var skill = (ESAgentArtifactAttribute)Attribute.GetCustomAttribute(
                typeof(ESAgentSkillOutputPayload), typeof(ESAgentArtifactAttribute));

            Assert.That(command, Is.Not.Null);
            Assert.That(skill, Is.Not.Null);
            Assert.That(command.StableId, Is.EqualTo(ESAgentGraphStableIds.AICommandArtifact));
            Assert.That(skill.StableId, Is.EqualTo(ESAgentGraphStableIds.AISkillArtifact));
            Assert.That(command.StableId, Is.Not.EqualTo(skill.StableId));
        }

        [Test]
        public void StableGraph_AssetDomainAttributesSeparatePlayerAndEditorTypes()
        {
            Type[] runtimeTypes =
            {
                typeof(ESGenericGraphAsset),
                typeof(ESStoryGraphAsset),
                typeof(ESBehaviorTreeGraphAsset)
            };
            foreach (Type type in runtimeTypes)
            {
                var attribute = (ESGraphAssetDomainAttribute)Attribute.GetCustomAttribute(
                    type, typeof(ESGraphAssetDomainAttribute));
                Assert.That(attribute, Is.Not.Null, type.FullName);
                Assert.That(attribute.EditorOnly, Is.False, type.FullName);
            }

            var agentAttribute = (ESGraphAssetDomainAttribute)Attribute.GetCustomAttribute(
                typeof(ESAgentAuthoringGraphAsset), typeof(ESGraphAssetDomainAttribute));
            Assert.That(agentAttribute, Is.Not.Null);
            Assert.That(agentAttribute.EditorOnly, Is.True);
        }

        [Test]
        public void StableGraph_AgentSemanticsUseOpaqueStableIdsAndKeepReservedEnumSlotsEmpty()
        {
            Assert.That(Enum.IsDefined(typeof(ESGraphDomainKind), (byte)3), Is.False);
            for (byte value = 20; value <= 25; value++)
                Assert.That(Enum.IsDefined(typeof(ESGraphBuiltInNodeKind), value), Is.False,
                    "Node enum reserved slot " + value);
            for (byte value = 7; value <= 9; value++)
                Assert.That(Enum.IsDefined(typeof(ESGraphPortValueKind), value), Is.False,
                    "Port enum reserved slot " + value);

            Assert.That(ESAgentGraphStableIds.Domain.Kind, Is.EqualTo(ESGraphDomainKind.Custom));
            Assert.That(ESAgentGraphStableIds.Node(ESAgentGraphStableIds.GoalNode).Kind,
                Is.EqualTo(ESGraphBuiltInNodeKind.Custom));
            Assert.That(ESGraphPortValueCatalog.GetKind(ESAgentGraphStableIds.ContextPort),
                Is.EqualTo(ESGraphPortValueKind.Custom));
        }

        [Test]
        public void StableGraph_BuildGateRejectsAgentGraphInsideResources()
        {
            const string folder = "Assets/ESNormalAssets/Resources";
            ESAgentAuthoringGraphPreset.EnsureAssetFolder(folder);
            string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/EditorOnlyAgentGraph.asset");
            ESAgentAuthoringGraphAsset graph = ScriptableObject.CreateInstance<ESAgentAuthoringGraphAsset>();
            try
            {
                AssetDatabase.CreateAsset(graph, path);
                AssetDatabase.SaveAssets();
                Assert.That(ESAgentGraphBuildGate.CollectViolations().Any(item =>
                    item.Contains(path) && item.Contains("[Resources]")), Is.True);
            }
            finally
            {
                AssetDatabase.DeleteAsset(path);
            }
        }

        [Test]
        public void StableGraph_AgentArtifactRegistrationIsIdempotent()
        {
            var attribute = (ESAgentArtifactAttribute)Attribute.GetCustomAttribute(
                typeof(ESAgentAICommandOutputPayload), typeof(ESAgentArtifactAttribute));
            var register = new ESAgentArtifactAttributeRegister();
            register.Handle(attribute, typeof(ESAgentAICommandOutputPayload));
            register.Handle(attribute, typeof(ESAgentAICommandOutputPayload));

            Assert.That(ESAgentArtifactTypeRegistry.TryGet(attribute.StableId, out Type registered), Is.True);
            Assert.That(registered, Is.EqualTo(typeof(ESAgentAICommandOutputPayload)));
        }

        [Test]
        public void StableGraph_DegreeRuleSupportsOptionalArgumentsAndRejectsInvalidRanges()
        {
            var defaults = new ESGraphDegreeRule();
            var outgoingOnly = new ESGraphDegreeRule(minOutgoing: 1);

            Assert.That(defaults.MinIncoming, Is.Zero);
            Assert.That(defaults.MaxIncoming, Is.EqualTo(ESGraphDegreeRule.Unlimited));
            Assert.That(defaults.AllowIsolated, Is.True);
            Assert.That(outgoingOnly.MinOutgoing, Is.EqualTo(1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ESGraphDegreeRule(minIncoming: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new ESGraphDegreeRule(minIncoming: 2, maxIncoming: 1));
        }

        [TestCase((int)ESStableGraphCreationTemplateKind.GenericFlow, typeof(ESGenericGraphAsset),
            ESGraphDomainIds.Generic)]
        [TestCase((int)ESStableGraphCreationTemplateKind.Story, typeof(ESStoryGraphAsset),
            ESGraphDomainIds.Story)]
        [TestCase((int)ESStableGraphCreationTemplateKind.BehaviorTree, typeof(ESBehaviorTreeGraphAsset),
            ESGraphDomainIds.BehaviorTree)]
        public void StableGraph_BuiltInBusinessTemplatesPassUnifiedValidation(
            int templateValue, Type assetType, string domainId)
        {
            ESGraphAssetBase graph = ScriptableObject.CreateInstance(assetType) as ESGraphAssetBase;
            try
            {
                Assert.That(graph, Is.Not.Null);
                Assert.That(graph.DomainId, Is.EqualTo(domainId));
                ESStableGraphViewWindow.PopulateDomainTemplate(graph,
                    (ESStableGraphCreationTemplateKind)templateValue);

                List<ESGraphValidationIssue> issues = ESGraphAuthoringRegistry.Validate(graph);
                Assert.That(issues.Any(IsError), Is.False, Describe(issues));
                Assert.That(ESGraphAuthoringRegistry.TryBake(graph, out ESBakedGraphSnapshot snapshot,
                    out _, out issues), Is.True, Describe(issues));
                Assert.That(snapshot.DomainId, Is.EqualTo(domainId));
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }

        [TestCase(ESAgentAuthoringPresetKind.Paired)]
        [TestCase(ESAgentAuthoringPresetKind.AICommandOnly)]
        [TestCase(ESAgentAuthoringPresetKind.AgentSkillOnly)]
        [TestCase(ESAgentAuthoringPresetKind.MindMapPaired)]
        public void StableGraph_AgentTemplatesPassTheSameCommercialGate(
            ESAgentAuthoringPresetKind presetKind)
        {
            ESAgentAuthoringGraphAsset graph = ScriptableObject.CreateInstance<ESAgentAuthoringGraphAsset>();
            try
            {
                ESAgentAuthoringGraphPreset.Populate(graph, presetKind);

                List<ESGraphValidationIssue> issues = ESGraphAuthoringRegistry.Validate(graph);
                Assert.That(issues.Any(IsError), Is.False, Describe(issues));
                Assert.That(ESGraphAuthoringRegistry.TryBake(graph, out ESBakedGraphSnapshot snapshot,
                    out IESBakedGraphPlan plan, out issues), Is.True, Describe(issues));
                Assert.That(snapshot.DomainId, Is.EqualTo(ESAgentGraphStableIds.DomainId));
                Assert.That(plan, Is.TypeOf<ESAgentArtifactGenerationSpec>());
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void StableGraph_SharedEditorSurfaceAcceptsEveryConcreteGraphType()
        {
            ESGraphAssetBase[] graphs =
            {
                ScriptableObject.CreateInstance<ESGenericGraphAsset>(),
                ScriptableObject.CreateInstance<ESStoryGraphAsset>(),
                ScriptableObject.CreateInstance<ESBehaviorTreeGraphAsset>(),
                ScriptableObject.CreateInstance<ESAgentAuthoringGraphAsset>()
            };
            var view = new ESStableGraphView(null, null);
            try
            {
                ESStableGraphViewWindow.PopulateDomainTemplate(graphs[0],
                    ESStableGraphCreationTemplateKind.GenericFlow);
                ESStableGraphViewWindow.PopulateDomainTemplate(graphs[1],
                    ESStableGraphCreationTemplateKind.Story);
                ESStableGraphViewWindow.PopulateDomainTemplate(graphs[2],
                    ESStableGraphCreationTemplateKind.BehaviorTree);
                ESAgentAuthoringGraphPreset.Populate(graphs[3], ESAgentAuthoringPresetKind.Paired);

                foreach (ESGraphAssetBase graph in graphs)
                {
                    Assert.That(ESGraphAuthoringRegistry.TryGetProfile(graph.DomainKey, out _), Is.True,
                        graph.GetType().Name);
                    Assert.That(ESGraphAuthoringRegistry.GetNodeDefinitions(graph), Is.Not.Empty,
                        graph.GetType().Name);
                    Assert.DoesNotThrow(() => view.SetAsset(graph), graph.GetType().Name);
                    Assert.That(view.Asset, Is.SameAs(graph), graph.GetType().Name);
                    Assert.That(ESGraphAuthoringRegistry.TryBake(graph, out _, out _,
                        out List<ESGraphValidationIssue> issues), Is.True,
                        graph.GetType().Name + ": " + Describe(issues));
                }

                System.Reflection.MethodInfo inspectorSetAsset = typeof(ESStableGraphInspector)
                    .GetMethod(nameof(ESStableGraphInspector.SetAsset));
                System.Reflection.MethodInfo windowOpenGraph = typeof(ESStableGraphViewWindow)
                    .GetMethod("OpenGraph", System.Reflection.BindingFlags.Instance
                        | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                Assert.That(inspectorSetAsset?.GetParameters().Single().ParameterType,
                    Is.EqualTo(typeof(ESGraphAssetBase)));
                Assert.That(windowOpenGraph?.GetParameters().Single().ParameterType,
                    Is.EqualTo(typeof(ESGraphAssetBase)));
            }
            finally
            {
                view.Dispose();
                foreach (ESGraphAssetBase graph in graphs)
                    Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void StableGraph_TemplateDomainMismatchFailsWithoutMutation()
        {
            ESStoryGraphAsset story = ScriptableObject.CreateInstance<ESStoryGraphAsset>();
            try
            {
                Assert.Throws<InvalidOperationException>(() =>
                    ESStableGraphViewWindow.PopulateDomainTemplate(story,
                        ESStableGraphCreationTemplateKind.BehaviorTree));
                Assert.That(story.Nodes, Is.Empty);
                Assert.That(story.Edges, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(story);
            }
        }

        [TestCase(ESGraphDomainIds.Generic, ESGraphNodeTypeIds.GenericSource)]
        [TestCase(ESGraphDomainIds.Story, ESGraphNodeTypeIds.StoryStart)]
        [TestCase(ESGraphDomainIds.BehaviorTree, ESGraphNodeTypeIds.BehaviorRoot)]
        [TestCase(ESAgentGraphStableIds.DomainId, ESAgentGraphStableIds.GoalNode)]
        public void StableGraph_AllDomainsRejectIsolatedRequiredNodes(
            string domainId, string nodeTypeId)
        {
            ESTestGraphAsset graph = ScriptableObject.CreateInstance<ESTestGraphAsset>();
            try
            {
                Assert.That(graph.InitializeTestDomain(domainId, out string domainError), Is.True, domainError);
                AddDefinedNode(graph, nodeTypeId, Vector2.zero);

                List<ESGraphValidationIssue> issues = ESGraphAuthoringRegistry.Validate(graph);
                Assert.That(issues.Any(issue => issue?.code == "Graph.Isolated"), Is.True,
                    Describe(issues));
                Assert.That(issues.Any(issue => issue?.code == "Graph.Degree.Outgoing.Min"), Is.True,
                    Describe(issues));
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void StableGraph_DegreeValidatorCountsEveryEdgeOnOneMultiConnectionPort()
        {
            ESTestGraphAsset graph = ScriptableObject.CreateInstance<ESTestGraphAsset>();
            ESGraphDomainKey domain = ESGraphDomainKey.FromKind(ESGraphDomainKind.Generic);
            var sourceDefinition = new ESStableGraphNodeTemplate(domain,
                ESGraphNodeTypeKey.Custom("es.test.degree-source"), "Test/Source", "Source", string.Empty,
                ESGraphNodeCategory.Entry, ESGraphNodeTheme.Entry, "Source", string.Empty, 1, 0, default,
                new ESGraphDegreeRule(maxIncoming: 0, minOutgoing: 1, maxOutgoing: 1),
                new ESGraphPortDefinition("输出", "flow.output", ESGraphPortDirection.Output,
                    ESGraphPortCapacity.Multi));
            var sinkDefinition = new ESStableGraphNodeTemplate(domain,
                ESGraphNodeTypeKey.Custom("es.test.degree-sink"), "Test/Sink", "Sink", string.Empty,
                ESGraphNodeCategory.Exit, ESGraphNodeTheme.Exit, "Sink", string.Empty, 1, 0, default,
                new ESGraphDegreeRule(minIncoming: 1, maxOutgoing: 0),
                new ESGraphPortDefinition("输入", "flow.input", ESGraphPortDirection.Input,
                    ESGraphPortCapacity.Single));
            try
            {
                ESGraphNodeRecord source = graph.AddNode(sourceDefinition.NodeType, "Source",
                    Vector2.zero, sourceDefinition.Ports);
                ESGraphNodeRecord firstSink = graph.AddNode(sinkDefinition.NodeType, "Sink A",
                    Vector2.right, sinkDefinition.Ports);
                ESGraphNodeRecord secondSink = graph.AddNode(sinkDefinition.NodeType, "Sink B",
                    Vector2.right * 2f, sinkDefinition.Ports);
                Assert.That(graph.TryAddEdge(Output(source), Input(firstSink), out _, out string firstError),
                    Is.True, firstError);
                Assert.That(graph.TryAddEdge(Output(source), Input(secondSink), out _, out string secondError),
                    Is.True, secondError);

                var issues = new List<ESGraphValidationIssue>();
                ESGraphDegreeValidator.Validate(graph,
                    new IESGraphNodeDefinition[] { sourceDefinition, sinkDefinition }, issues);
                Assert.That(issues.Count(issue => issue?.code == "Graph.Degree.Outgoing.Max"),
                    Is.EqualTo(1), Describe(issues));
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void StableGraph_TopologySeparatesMultipleEndpointsFromOnePortWithMultipleEdges()
        {
            ESTestGraphAsset graph = ScriptableObject.CreateInstance<ESTestGraphAsset>();
            try
            {
                var sourceDefinition = new ESStableGraphNodeTemplate(
                    ESGraphDomainKey.FromKind(ESGraphDomainKind.Generic),
                    ESGraphNodeTypeKey.Custom("es.test.topology-source"), "Test/Source", "Source",
                    string.Empty, ESGraphNodeCategory.Entry, ESGraphNodeTheme.Entry,
                    "Source", string.Empty, 1, 0, default, new ESGraphDegreeRule(),
                    new ESGraphPortDefinition("分发", "route.fan-out",
                        ESGraphPortDirection.Output, ESGraphPortCapacity.Multi,
                        ESGraphPortValueIds.Flow, meaning: "同一出口允许多条连接"));
                var sinkDefinition = new ESStableGraphNodeTemplate(
                    ESGraphDomainKey.FromKind(ESGraphDomainKind.Generic),
                    ESGraphNodeTypeKey.Custom("es.test.topology-sink"), "Test/Sink", "Sink",
                    string.Empty, ESGraphNodeCategory.Exit, ESGraphNodeTheme.Exit,
                    "Sink", string.Empty, 1, 0, default, new ESGraphDegreeRule(),
                    new ESGraphPortDefinition("输入", "route.input",
                        ESGraphPortDirection.Input));
                ESGraphNodeRecord source = graph.AddNode(sourceDefinition.NodeType, "Source",
                    Vector2.zero, sourceDefinition.Ports);
                ESGraphNodeRecord firstSink = graph.AddNode(sinkDefinition.NodeType, "Sink A",
                    Vector2.right, sinkDefinition.Ports);
                ESGraphNodeRecord secondSink = graph.AddNode(sinkDefinition.NodeType, "Sink B",
                    Vector2.right * 2f, sinkDefinition.Ports);
                Assert.That(graph.TryAddEdge(Output(source), Input(firstSink), out _, out string firstError),
                    Is.True, firstError);
                Assert.That(graph.TryAddEdge(Output(source), Input(secondSink), out _, out string secondError),
                    Is.True, secondError);

                ESGraphNodeTopology singleEndpoint =
                    ESGraphTopologyAnalyzer.Analyze(source, graph.Nodes, graph.Edges);
                Assert.That(singleEndpoint.OutputEndpointCount, Is.EqualTo(1));
                Assert.That(singleEndpoint.OutputConnectionCount, Is.EqualTo(2));
                Assert.That(singleEndpoint.IsMultiEndpointNode, Is.False,
                    "同一 Multi 容量端点的两条边仍是单端口多连接，不能判成多端口节点。");
                Assert.That(singleEndpoint.Endpoints.Single().Capacity,
                    Is.EqualTo(ESGraphPortCapacity.Multi));
                Assert.That(singleEndpoint.Endpoints.Single().ConnectionCount, Is.EqualTo(2));

                Assert.That(graph.AddPort(source.nodeId, new ESGraphPortDefinition(
                    "失败", "route.failure", ESGraphPortDirection.Output,
                    ESGraphPortCapacity.Single, ESGraphPortValueIds.Flow,
                    meaning: "独立失败出口"), out string addError), Is.Not.Null, addError);
                ESGraphNodeTopology multipleEndpoints =
                    ESGraphTopologyAnalyzer.Analyze(source, graph.Nodes, graph.Edges);
                Assert.That(multipleEndpoints.OutputEndpointCount, Is.EqualTo(2));
                Assert.That(multipleEndpoints.OutputConnectionCount, Is.EqualTo(2));
                Assert.That(multipleEndpoints.HasMultipleOutputEndpoints, Is.True,
                    "两个不同 PortId、StableKey、方向和用途才构成多输出端点。");

                var ordinaryFlowNode = new ESGraphNodeRecord
                {
                    nodeId = ESGraphIdentity.NewId(),
                    ports = new List<ESGraphPortRecord>
                    {
                        new ESGraphPortRecord
                        {
                            portId = ESGraphIdentity.NewId(), stableKey = "flow.input",
                            meaning = "流程输入", direction = ESGraphPortDirection.Input,
                            capacity = ESGraphPortCapacity.Multi
                        },
                        new ESGraphPortRecord
                        {
                            portId = ESGraphIdentity.NewId(), stableKey = "flow.output",
                            meaning = "流程输出", direction = ESGraphPortDirection.Output,
                            capacity = ESGraphPortCapacity.Multi
                        }
                    }
                };
                ESGraphNodeTopology ordinaryTopology =
                    ESGraphTopologyAnalyzer.Analyze(ordinaryFlowNode,
                        new[] { ordinaryFlowNode }, Array.Empty<ESGraphEdgeRecord>());
                Assert.That(ordinaryTopology.InputEndpointCount, Is.EqualTo(1));
                Assert.That(ordinaryTopology.OutputEndpointCount, Is.EqualTo(1));
                Assert.That(ordinaryTopology.MultiConnectionCapacityEndpointCount, Is.EqualTo(2));
                Assert.That(ordinaryTopology.IsMultiEndpointNode, Is.False,
                    "普通一入一出节点即使两个端点都允许多连接，也不是多端口节点。");

                var foreignNodeWithDuplicatedPortId = new ESGraphNodeRecord
                {
                    nodeId = ESGraphIdentity.NewId(),
                    ports = new List<ESGraphPortRecord>
                    {
                        new ESGraphPortRecord
                        {
                            portId = ordinaryFlowNode.ports[0].portId,
                            stableKey = "foreign.input",
                            meaning = "冲突端点",
                            direction = ESGraphPortDirection.Input
                        }
                    }
                };
                ESGraphNodeTopology crossNodeDuplicateTopology =
                    ESGraphTopologyAnalyzer.Analyze(ordinaryFlowNode,
                        new[] { ordinaryFlowNode, foreignNodeWithDuplicatedPortId },
                        Array.Empty<ESGraphEdgeRecord>());
                Assert.That(crossNodeDuplicateTopology.InputEndpointCount, Is.Zero,
                    "跨节点重复 PortId 的记录不得计为独立稳定端点。");
                Assert.That(crossNodeDuplicateTopology.OutputEndpointCount, Is.EqualTo(1));
                Assert.That(crossNodeDuplicateTopology.InvalidEndpointRecordCount, Is.EqualTo(1));

                ESGraphPortRecord duplicated = ordinaryFlowNode.ports[1];
                ordinaryFlowNode.ports.Add(new ESGraphPortRecord
                {
                    portId = duplicated.portId,
                    stableKey = duplicated.stableKey,
                    meaning = duplicated.meaning,
                    direction = duplicated.direction,
                    capacity = duplicated.capacity
                });
                ESGraphNodeTopology duplicateTopology =
                    ESGraphTopologyAnalyzer.Analyze(ordinaryFlowNode,
                        new[] { ordinaryFlowNode }, Array.Empty<ESGraphEdgeRecord>());
                Assert.That(duplicateTopology.OutputEndpointCount, Is.Zero,
                    "重复身份的两条记录都不是可独立寻址的稳定端点。");
                Assert.That(duplicateTopology.InvalidEndpointRecordCount, Is.EqualTo(2));
                Assert.That(duplicateTopology.IsMultiEndpointNode, Is.False,
                    "重复同一 PortId、StableKey、方向和用途不得伪造第二个稳定端点。");
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void StableGraph_BakedRoutesPreserveOneToManyAndManyToOneEndpoints()
        {
            ESTestGraphAsset graph = ScriptableObject.CreateInstance<ESTestGraphAsset>();
            var sourceDefinition = new ESStableGraphNodeTemplate(
                ESGraphDomainKey.FromKind(ESGraphDomainKind.Generic),
                ESGraphNodeTypeKey.Custom("es.test.route-source"), "Test/RouteSource", "Source",
                string.Empty, ESGraphNodeCategory.Entry, ESGraphNodeTheme.Entry,
                "Source", string.Empty, 1, 0, default,
                new ESGraphDegreeRule(),
                new ESGraphPortDefinition("输出", "route.out", ESGraphPortDirection.Output,
                    ESGraphPortCapacity.Multi));
            var targetDefinition = new ESStableGraphNodeTemplate(
                ESGraphDomainKey.FromKind(ESGraphDomainKind.Generic),
                ESGraphNodeTypeKey.Custom("es.test.route-target"), "Test/RouteTarget", "Target",
                string.Empty, ESGraphNodeCategory.Exit, ESGraphNodeTheme.Exit,
                "Target", string.Empty, 1, 0, default,
                new ESGraphDegreeRule(),
                new ESGraphPortDefinition("输入", "route.in", ESGraphPortDirection.Input,
                    ESGraphPortCapacity.Multi));
            try
            {
                ESGraphNodeRecord sourceA = graph.AddNode(sourceDefinition.NodeType, "Source A",
                    Vector2.zero, sourceDefinition.Ports);
                ESGraphNodeRecord sourceB = graph.AddNode(sourceDefinition.NodeType, "Source B",
                    Vector2.right, sourceDefinition.Ports);
                ESGraphNodeRecord targetA = graph.AddNode(targetDefinition.NodeType, "Target A",
                    Vector2.right * 2f, targetDefinition.Ports);
                ESGraphNodeRecord targetB = graph.AddNode(targetDefinition.NodeType, "Target B",
                    Vector2.right * 3f, targetDefinition.Ports);
                ESGraphNodeRecord targetC = graph.AddNode(targetDefinition.NodeType, "Target C",
                    Vector2.right * 4f, targetDefinition.Ports);

                string sourceAOut = sourceA.ports[0].portId;
                string sourceBOut = sourceB.ports[0].portId;
                string targetAIn = targetA.ports[0].portId;
                string targetBIn = targetB.ports[0].portId;
                string targetCIn = targetC.ports[0].portId;
                Assert.That(graph.TryAddEdge(sourceAOut, targetAIn, out _, out string firstError),
                    Is.True, firstError);
                Assert.That(graph.TryAddEdge(sourceAOut, targetBIn, out _, out string secondError),
                    Is.True, secondError);
                Assert.That(graph.TryAddEdge(sourceBOut, targetCIn, out _, out string thirdError),
                    Is.True, thirdError);
                Assert.That(graph.TryAddEdge(sourceAOut, targetCIn, out _, out string fourthError),
                    Is.True, fourthError);

                Assert.That(ESGraphSnapshotBaker.TryBake(graph,
                    out ESBakedGraphSnapshot snapshot, out List<ESGraphValidationIssue> issues),
                    Is.True, Describe(issues));
                Assert.That(snapshot.GetOutgoingRoutes(sourceA.nodeId, "route.out"),
                    Has.Count.EqualTo(3));
                Assert.That(snapshot.GetIncomingRoutes(targetC.nodeId, "route.in"),
                    Has.Count.EqualTo(2));
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void StableGraph_BakedRouteExposesCompleteEndpointContractAndIndexes()
        {
            ESTestGraphAsset graph = ScriptableObject.CreateInstance<ESTestGraphAsset>();
            var sourceDefinition = new ESStableGraphNodeTemplate(
                ESGraphDomainKey.FromKind(ESGraphDomainKind.Generic),
                ESGraphNodeTypeKey.Custom("es.test.semantic-source"), "Test/SemanticSource", "Source",
                string.Empty, ESGraphNodeCategory.Entry, ESGraphNodeTheme.Entry,
                "Source", string.Empty, 1, 0, default, new ESGraphDegreeRule(),
                new ESGraphPortDefinition("输出", "semantic.out", ESGraphPortDirection.Output,
                    ESGraphPortCapacity.Multi, ESGraphPortValueIds.Text));
            var targetDefinition = new ESStableGraphNodeTemplate(
                ESGraphDomainKey.FromKind(ESGraphDomainKind.Generic),
                ESGraphNodeTypeKey.Custom("es.test.semantic-target"), "Test/SemanticTarget", "Target",
                string.Empty, ESGraphNodeCategory.Exit, ESGraphNodeTheme.Exit,
                "Target", string.Empty, 1, 0, default, new ESGraphDegreeRule(),
                new ESGraphPortDefinition("输入", "semantic.in", ESGraphPortDirection.Input,
                    ESGraphPortCapacity.Multi, ESGraphPortValueIds.Text));
            try
            {
                ESGraphNodeRecord source = graph.AddNode(sourceDefinition.NodeType, "Source",
                    Vector2.zero, sourceDefinition.Ports);
                ESGraphNodeRecord target = graph.AddNode(targetDefinition.NodeType, "Target",
                    Vector2.right, targetDefinition.Ports);
                Assert.That(source.TryGetPort("semantic.out", out ESGraphPortRecord sourcePort),
                    Is.True);
                Assert.That(target.TryGetPort("semantic.in", out ESGraphPortRecord targetPort),
                    Is.True);
                string sourcePortId = sourcePort.portId;
                string targetPortId = targetPort.portId;
                Assert.That(graph.TryAddEdge(sourcePortId, targetPortId, out ESGraphEdgeRecord edge,
                    out string addError), Is.True, addError);
                Assert.That(ESGraphSnapshotBaker.TryBake(graph, out ESBakedGraphSnapshot snapshot,
                    out List<ESGraphValidationIssue> issues), Is.True, Describe(issues));

                ESGraphRouteSnapshot route = snapshot.Routes.Single();
                Assert.That(route.SourceNode, Is.Not.Null);
                Assert.That(route.TargetNode, Is.Not.Null);
                Assert.That(route.SourcePort, Is.Not.Null);
                Assert.That(route.TargetPort, Is.Not.Null);
                Assert.That(route.SourcePortId, Is.EqualTo(sourcePortId));
                Assert.That(route.TargetPortId, Is.EqualTo(targetPortId));
                Assert.That(route.SourceValueTypeId, Is.EqualTo(ESGraphPortValueIds.Text));
                Assert.That(route.TargetValueTypeId, Is.EqualTo(ESGraphPortValueIds.Text));
                Assert.That(route.IsFlow, Is.False);
                Assert.That(snapshot.GetOutgoingRoutes(source.nodeId, "semantic.out").Single().EdgeId,
                    Is.EqualTo(edge.edgeId));
                Assert.That(snapshot.GetIncomingRoutes(target.nodeId, "semantic.in").Single().EdgeId,
                    Is.EqualTo(edge.edgeId));
                Assert.That(snapshot.GetOutgoingRoutesByPortId(sourcePortId).Single().EdgeId,
                    Is.EqualTo(edge.edgeId));
                Assert.That(snapshot.GetIncomingRoutesByPortId(targetPortId).Single().EdgeId,
                    Is.EqualTo(edge.edgeId));
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void StableGraph_DeepGraphReachabilityUsesIterativeTraversal()
        {
            const int FlowNodeCount = 1500;
            ESTestGraphAsset graph = ScriptableObject.CreateInstance<ESTestGraphAsset>();
            try
            {
                Assert.That(graph.InitializeTestDomain(ESGraphDomainIds.Generic, out string domainError),
                    Is.True, domainError);
                ESGraphNodeRecord previous = AddDefinedNode(graph,
                    ESGraphBuiltInNodeKind.GenericSource, Vector2.zero);
                for (int i = 0; i < FlowNodeCount; i++)
                {
                    ESGraphNodeRecord current = AddDefinedNode(graph,
                        ESGraphBuiltInNodeKind.GenericFlow, new Vector2(i + 1, 0f));
                    Assert.That(graph.TryAddEdge(Output(previous), Input(current), out _, out string edgeError),
                        Is.True, edgeError);
                    previous = current;
                }
                ESGraphNodeRecord sink = AddDefinedNode(graph,
                    ESGraphBuiltInNodeKind.GenericSink, new Vector2(FlowNodeCount + 1, 0f));
                Assert.That(graph.TryAddEdge(Output(previous), Input(sink), out _, out string finalError),
                    Is.True, finalError);

                List<ESGraphValidationIssue> issues = ESGraphAuthoringRegistry.Validate(graph);
                Assert.That(issues.Any(IsError), Is.False, Describe(issues));
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void StableGraph_PersistsGraphIdentityAndNodePosition_WhileIndependentCopyGetsNewIdentity()
        {
            ESTestGraphAsset source = ScriptableObject.CreateInstance<ESTestGraphAsset>();
            ESTestGraphAsset restored = ScriptableObject.CreateInstance<ESTestGraphAsset>();
            try
            {
                ESGraphNodeRecord first = source.AddNode("test.node", "A", new Vector2(123f, 456f), DefaultPorts);
                ESGraphNodeRecord second = source.AddNode("test.node", "B", new Vector2(500f, 456f), DefaultPorts);
                Assert.That(source.TryAddEdge(Output(first), Input(second), out ESGraphEdgeRecord edge,
                    out string edgeError), Is.True, edgeError);
                Assert.That(ESGraphIdentity.IsValid(source.GraphId), Is.True);
                Assert.That(source.OriginGraphId, Is.Empty);

                string serialized = EditorJsonUtility.ToJson(source);
                EditorJsonUtility.FromJsonOverwrite(serialized, restored);
                Assert.That(restored.GraphId, Is.EqualTo(source.GraphId));
                Assert.That(restored.FindNode(first.nodeId).position, Is.EqualTo(new Vector2(123f, 456f)));

                string sourceGraphId = restored.GraphId;
                string firstNodeId = restored.Nodes[0].nodeId;
                string firstPortId = restored.Nodes[0].ports[0].portId;
                string edgeId = restored.Edges[0].edgeId;
                restored.InitializeAsIndependentCopyOf(sourceGraphId);

                Assert.That(ESGraphIdentity.IsValid(restored.GraphId), Is.True);
                Assert.That(restored.GraphId, Is.Not.EqualTo(sourceGraphId));
                Assert.That(restored.OriginGraphId, Is.EqualTo(sourceGraphId));
                Assert.That(restored.Nodes[0].nodeId, Is.EqualTo(firstNodeId));
                Assert.That(restored.Nodes[0].ports[0].portId, Is.EqualTo(firstPortId));
                Assert.That(restored.Edges[0].edgeId, Is.EqualTo(edgeId));

                Assert.That(ESGraphSnapshotBaker.TryBake(restored, out ESBakedGraphSnapshot snapshot,
                    out List<ESGraphValidationIssue> issues), Is.True,
                    string.Join(";", issues.ConvertAll(issue => issue.message)));
                Assert.That(snapshot.GraphId, Is.EqualTo(restored.GraphId));
                Assert.That(snapshot.OriginGraphId, Is.EqualTo(sourceGraphId));
            }
            finally
            {
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(restored);
            }
        }

        [Test]
        public void StableGraph_CreatesStableUniqueNodePortAndEdgeIdentities()
        {
            ESTestGraphAsset graph = ScriptableObject.CreateInstance<ESTestGraphAsset>();
            try
            {
                ESGraphNodeRecord first = graph.AddNode("test.node", "A", Vector2.zero, DefaultPorts);
                ESGraphNodeRecord second = graph.AddNode("test.node", "B", Vector2.right, DefaultPorts);
                Assert.That(first.nodeId, Is.Not.Empty);
                Assert.That(second.nodeId, Is.Not.EqualTo(first.nodeId));
                Assert.That(first.ports[0].portId, Is.Not.EqualTo(second.ports[0].portId));

                Assert.That(graph.TryAddEdge(Output(first), Input(second), out ESGraphEdgeRecord edge, out string error), Is.True, error);
                Assert.That(edge.edgeId, Is.Not.Empty);
                Assert.That(graph.ValidateGraph(), Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void StableGraph_RejectsDirectionTypeCapacityDuplicateAndCycleViolations()
        {
            ESTestGraphAsset graph = ScriptableObject.CreateInstance<ESTestGraphAsset>();
            try
            {
                ESGraphNodeRecord first = graph.AddNode("test.node", "A", Vector2.zero, DefaultPorts);
                ESGraphNodeRecord second = graph.AddNode("test.node", "B", Vector2.right, DefaultPorts);
                ESGraphNodeRecord third = graph.AddNode("test.node", "C", Vector2.right * 2f, DefaultPorts);

                Assert.That(graph.TryAddEdge(Input(first), Input(second), out _, out _), Is.False);
                second.ports[0].valueTypeId = "number";
                Assert.That(graph.TryAddEdge(Output(first), Input(second), out _, out _), Is.False);
                second.ports[0].valueTypeId = "flow";
                Assert.That(graph.TryAddEdge(Output(first), Input(second), out _, out string firstError), Is.True, firstError);
                Assert.That(graph.TryAddEdge(Output(first), Input(second), out _, out _), Is.False, "Duplicate edge must be rejected.");
                Assert.That(graph.TryAddEdge(Output(third), Input(second), out _, out _), Is.False, "Single input capacity must be enforced.");
                Assert.That(graph.TryAddEdge(Output(second), Input(third), out _, out string secondError), Is.True, secondError);
                Assert.That(graph.TryAddEdge(Output(third), Input(first), out _, out _), Is.False, "Cycle must be rejected when allowCycles is false.");
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void StableGraph_ConnectionCompatibilityIndexMatchesAuthoritativeCanConnect()
        {
            ESTestGraphAsset graph = ScriptableObject.CreateInstance<ESTestGraphAsset>();
            try
            {
                ESGraphNodeRecord source = graph.AddNode("test.source", "Source", Vector2.zero,
                    DefaultPorts);
                ESGraphNodeRecord valid = graph.AddNode("test.sink", "Valid", Vector2.right,
                    DefaultPorts);
                ESGraphNodeRecord occupied = graph.AddNode("test.sink", "Occupied",
                    Vector2.right * 2f, DefaultPorts);
                ESGraphNodeRecord wrongType = graph.AddNode("test.sink", "WrongType",
                    Vector2.right * 3f, DefaultPorts);
                wrongType.ports.Single(port => port.direction == ESGraphPortDirection.Input)
                    .valueTypeId = "number";
                Assert.That(graph.TryAddEdge(Output(occupied), Input(occupied), out _, out _), Is.False);

                ESGraphNodeRecord blocker = graph.AddNode("test.source", "Blocker", Vector2.down,
                    DefaultPorts);
                Assert.That(graph.TryAddEdge(Output(blocker), Input(occupied), out _,
                    out string occupiedError), Is.True, occupiedError);

                var compatible = new HashSet<string>(StringComparer.Ordinal);
                Assert.That(graph.TryBuildConnectionCompatibilityIndex(Output(source), compatible,
                    out string indexError), Is.True, indexError);

                foreach (ESGraphPortRecord candidate in graph.Nodes.SelectMany(node => node.ports))
                {
                    bool expected = graph.CanConnect(Output(source), candidate.portId, null, out _);
                    Assert.That(compatible.Contains(candidate.portId), Is.EqualTo(expected),
                        "批量索引与 CanConnect 不一致：" + candidate.portId);
                }
                Assert.That(compatible, Does.Contain(Input(valid)));
                Assert.That(compatible, Does.Not.Contain(Input(occupied)));
                Assert.That(compatible, Does.Not.Contain(Input(wrongType)));
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void StableGraph_BehaviorTreeAndAgentCannotEnableCyclesButGenericCan()
        {
            ESGraphAssetBase[] cyclicGraphs =
            {
                ScriptableObject.CreateInstance<ESGenericGraphAsset>(),
                ScriptableObject.CreateInstance<ESBehaviorTreeGraphAsset>()
            };
            ESAgentAuthoringGraphAsset agent = ScriptableObject.CreateInstance<ESAgentAuthoringGraphAsset>();
            try
            {
                foreach (ESGraphAssetBase graph in cyclicGraphs)
                {
                    graph.allowCycles = true;
                    ESGraphNodeRecord first = graph.AddNode("test.node", "A", Vector2.zero,
                        DefaultPorts);
                    ESGraphNodeRecord second = graph.AddNode("test.node", "B", Vector2.right,
                        DefaultPorts);
                    Assert.That(graph.TryAddEdge(Output(first), Input(second), out _,
                        out string firstError), Is.True, firstError);

                    bool isGeneric = graph is ESGenericGraphAsset;
                    Assert.That(graph.CanEnableCycles, Is.EqualTo(isGeneric), graph.GetType().Name);
                    Assert.That(graph.AllowsCycles, Is.EqualTo(isGeneric), graph.GetType().Name);
                    Assert.That(graph.TryAddEdge(Output(second), Input(first), out _, out string cycleError),
                        Is.EqualTo(isGeneric), cycleError);
                    if (!isGeneric)
                        Assert.That(cycleError, Does.Contain("禁止循环"));
                }
                agent.allowCycles = true;
                Assert.That(agent.CanEnableCycles, Is.False);
                Assert.That(agent.AllowsCycles, Is.False);
            }
            finally
            {
                foreach (ESGraphAssetBase graph in cyclicGraphs)
                    Object.DestroyImmediate(graph);
                Object.DestroyImmediate(agent);
            }
        }

        [Test]
        public void StableGraph_ReconnectsEitherEndpointWithoutChangingEdgeIdentity()
        {
            ESTestGraphAsset graph = ScriptableObject.CreateInstance<ESTestGraphAsset>();
            try
            {
                ESGraphNodeRecord firstSource = graph.AddNode("test.source", "A", Vector2.zero, DefaultPorts);
                ESGraphNodeRecord firstSink = graph.AddNode("test.sink", "B", Vector2.right, DefaultPorts);
                ESGraphNodeRecord secondSink = graph.AddNode("test.sink", "C", Vector2.right * 2f, DefaultPorts);
                ESGraphNodeRecord secondSource = graph.AddNode("test.source", "D", Vector2.left, DefaultPorts);
                Assert.That(graph.TryAddEdge(Output(firstSource), Input(firstSink), out ESGraphEdgeRecord edge,
                    out string addError), Is.True, addError);
                string edgeId = edge.edgeId;

                Assert.That(graph.TryReconnectEdge(edgeId, Output(firstSource), Input(secondSink),
                    out string inputError), Is.True, inputError);
                Assert.That(graph.FindEdge(edgeId).inputPortId, Is.EqualTo(Input(secondSink)));
                Assert.That(graph.FindEdge(edgeId).outputPortId, Is.EqualTo(Output(firstSource)));

                Assert.That(graph.TryReconnectEdge(edgeId, Input(secondSink), Output(secondSource),
                    out string outputError), Is.True, outputError);
                Assert.That(graph.FindEdge(edgeId).edgeId, Is.EqualTo(edgeId));
                Assert.That(graph.FindEdge(edgeId).inputPortId, Is.EqualTo(Input(secondSink)));
                Assert.That(graph.FindEdge(edgeId).outputPortId, Is.EqualTo(Output(secondSource)));

                Assert.That(graph.TryReconnectEdge(edgeId, Input(secondSink), Output(secondSource),
                    out string noChangeError), Is.False);
                Assert.That(noChangeError, Is.Null);
                Assert.That(graph.Edges, Has.Count.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void StableGraph_DeleteAndRedrawCreatesANewEdgeIdentity()
        {
            ESTestGraphAsset graph = ScriptableObject.CreateInstance<ESTestGraphAsset>();
            try
            {
                ESGraphNodeRecord source = graph.AddNode("test.source", "A", Vector2.zero, DefaultPorts);
                ESGraphNodeRecord sink = graph.AddNode("test.sink", "B", Vector2.right, DefaultPorts);
                Assert.That(graph.TryAddEdge(Output(source), Input(sink), out ESGraphEdgeRecord original,
                    out string firstError), Is.True, firstError);
                string originalEdgeId = original.edgeId;

                Assert.That(graph.RemoveEdge(originalEdgeId), Is.True);
                Assert.That(graph.TryAddEdge(Output(source), Input(sink), out ESGraphEdgeRecord redrawn,
                    out string secondError), Is.True, secondError);
                Assert.That(redrawn.edgeId, Is.Not.EqualTo(originalEdgeId));
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void StableGraph_ReconnectIgnoresItsOwnSingleCapacityAndRejectsInvalidTargetsAtomically()
        {
            ESTestGraphAsset graph = ScriptableObject.CreateInstance<ESTestGraphAsset>();
            var multiConnectionFlowPorts = new[]
            {
                new ESGraphPortDefinition("输入", "flow.input", ESGraphPortDirection.Input,
                    ESGraphPortCapacity.Multi),
                new ESGraphPortDefinition("输出", "flow.output", ESGraphPortDirection.Output,
                    ESGraphPortCapacity.Multi)
            };
            try
            {
                ESGraphNodeRecord a = graph.AddNode("test.node", "A", Vector2.zero, multiConnectionFlowPorts);
                ESGraphNodeRecord b = graph.AddNode("test.node", "B", Vector2.right, multiConnectionFlowPorts);
                ESGraphNodeRecord c = graph.AddNode("test.node", "C", Vector2.right * 2f, multiConnectionFlowPorts);
                ESGraphNodeRecord d = graph.AddNode("test.node", "D", Vector2.right * 3f, multiConnectionFlowPorts);
                ESGraphNodeRecord numberSource = graph.AddNode("test.number", "N", Vector2.left,
                    new[]
                    {
                        new ESGraphPortDefinition("输出", "number.output", ESGraphPortDirection.Output,
                            ESGraphPortCapacity.Multi, "number")
                    });
                ESGraphNodeRecord singleSink = graph.AddNode("test.sink", "S", Vector2.down, DefaultPorts);

                Assert.That(graph.TryAddEdge(Output(a), Input(b), out ESGraphEdgeRecord first,
                    out string firstError), Is.True, firstError);
                Assert.That(graph.TryAddEdge(Output(b), Input(c), out _, out string secondError), Is.True, secondError);
                Assert.That(graph.TryAddEdge(Output(a), Input(d), out ESGraphEdgeRecord duplicateCandidate,
                    out string thirdError), Is.True, thirdError);
                Assert.That(graph.TryAddEdge(Output(b), Input(singleSink), out ESGraphEdgeRecord singleEdge,
                    out string fourthError),
                    Is.True, fourthError);

                Assert.That(graph.CanConnect(Output(b), Input(singleSink), singleEdge.edgeId,
                    out string ownCapacityError),
                    Is.True, ownCapacityError);
                AssertReconnectRejectedWithoutMutation(graph, first.edgeId,
                    Output(numberSource), Input(b), "type mismatch");
                AssertReconnectRejectedWithoutMutation(graph, duplicateCandidate.edgeId,
                    Output(a), Input(b), "duplicate edge");
                AssertReconnectRejectedWithoutMutation(graph, first.edgeId,
                    Output(c), Input(b), "cycle");
                AssertReconnectRejectedWithoutMutation(graph, first.edgeId,
                    Output(a), Input(singleSink), "occupied Single input");
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void StableGraph_ReconnectCompatibilityIndexMatchesTheAuthoritativeValidator()
        {
            ESTestGraphAsset graph = ScriptableObject.CreateInstance<ESTestGraphAsset>();
            var multiConnectionFlowPorts = new[]
            {
                new ESGraphPortDefinition("输入", "flow.input", ESGraphPortDirection.Input,
                    ESGraphPortCapacity.Multi),
                new ESGraphPortDefinition("输出", "flow.output", ESGraphPortDirection.Output,
                    ESGraphPortCapacity.Multi)
            };
            try
            {
                ESGraphNodeRecord a = graph.AddNode("test.node", "A", Vector2.zero, multiConnectionFlowPorts);
                ESGraphNodeRecord b = graph.AddNode("test.node", "B", Vector2.right, multiConnectionFlowPorts);
                ESGraphNodeRecord c = graph.AddNode("test.node", "C", Vector2.right * 2f, multiConnectionFlowPorts);
                ESGraphNodeRecord d = graph.AddNode("test.node", "D", Vector2.right * 3f, multiConnectionFlowPorts);
                ESGraphNodeRecord occupied = graph.AddNode("test.node", "Occupied", Vector2.down, DefaultPorts);
                ESGraphNodeRecord number = graph.AddNode("test.number", "Number", Vector2.up,
                    new[]
                    {
                        new ESGraphPortDefinition("输入", "number.input", ESGraphPortDirection.Input,
                            ESGraphPortCapacity.Single, "number")
                    });
                Assert.That(graph.TryAddEdge(Output(a), Input(b), out ESGraphEdgeRecord reconnecting,
                    out string firstError), Is.True, firstError);
                Assert.That(graph.TryAddEdge(Output(c), Input(a), out _, out string secondError),
                    Is.True, secondError);
                Assert.That(graph.TryAddEdge(Output(d), Input(occupied), out _, out string thirdError),
                    Is.True, thirdError);

                var compatible = new HashSet<string>(StringComparer.Ordinal);
                Assert.That(graph.TryBuildReconnectCompatibilityIndex(reconnecting.edgeId, Output(a),
                    compatible, out string indexError), Is.True, indexError);
                string before = EditorJsonUtility.ToJson(graph);
                for (int i = 0; i < graph.Nodes.Count; i++)
                {
                    ESGraphNodeRecord node = graph.Nodes[i];
                    for (int p = 0; p < node.ports.Count; p++)
                    {
                        string candidatePortId = node.ports[p].portId;
                        if (string.Equals(candidatePortId, Output(a), StringComparison.Ordinal))
                            continue;
                        bool expected = graph.CanConnect(Output(a), candidatePortId,
                            reconnecting.edgeId, out _);
                        Assert.That(compatible.Contains(candidatePortId), Is.EqualTo(expected),
                            "批量索引必须与唯一 CanConnect 规则一致：" + candidatePortId);
                    }
                }
                Assert.That(compatible, Does.Contain(Input(b)), "原端点必须可作为无变化目标。 ");
                Assert.That(compatible, Does.Not.Contain(Input(c)), "会形成循环的端点必须被排除。 ");
                Assert.That(compatible, Does.Not.Contain(Input(occupied)), "已占用 Single 端口必须被排除。 ");
                Assert.That(compatible, Does.Not.Contain(number.ports[0].portId), "异类型端口必须被排除。 ");
                Assert.That(EditorJsonUtility.ToJson(graph), Is.EqualTo(before), "兼容索引必须只读。 ");
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void StableGraph_ReconnectServiceCommitsOnceAndUndoRedoPreservesEdgeIdentity()
        {
            ESTestGraphAsset graph = ScriptableObject.CreateInstance<ESTestGraphAsset>();
            try
            {
                Undo.ClearAll();
                ESGraphNodeRecord source = graph.AddNode("test.source", "A", Vector2.zero, DefaultPorts);
                ESGraphNodeRecord firstSink = graph.AddNode("test.sink", "B", Vector2.right, DefaultPorts);
                ESGraphNodeRecord secondSink = graph.AddNode("test.sink", "C", Vector2.right * 2f, DefaultPorts);
                Assert.That(graph.TryAddEdge(Output(source), Input(firstSink), out ESGraphEdgeRecord edge,
                    out string addError), Is.True, addError);
                string edgeId = edge.edgeId;
                int dirtyCount = 0;
                int saveCount = 0;
                int notifyCount = 0;
                var service = new ESGraphEditService(
                    _ => dirtyCount++, () => saveCount++, _ => notifyCount++);

                ESGraphEditResult rejected = service.ReconnectEdge(
                    graph, edgeId, Input(firstSink), Input(secondSink));
                Assert.That(rejected.changed, Is.False);
                Assert.That(rejected.error, Is.Not.Empty);
                Assert.That(dirtyCount + saveCount + notifyCount, Is.Zero);

                ESGraphEditResult noChange = service.ReconnectEdge(
                    graph, edgeId, Input(firstSink), Output(source));
                Assert.That(noChange.changed, Is.False);
                Assert.That(noChange.error, Is.Empty);
                Assert.That(dirtyCount + saveCount + notifyCount, Is.Zero);

                ESGraphEditResult changed = service.ReconnectEdge(
                    graph, edgeId, Output(source), Input(secondSink));
                Undo.FlushUndoRecordObjects();
                Assert.That(changed.changed, Is.True, changed.error);
                Assert.That(dirtyCount, Is.EqualTo(1));
                Assert.That(saveCount, Is.EqualTo(1));
                Assert.That(notifyCount, Is.EqualTo(1));
                Assert.That(graph.FindEdge(edgeId).inputPortId, Is.EqualTo(Input(secondSink)));

                Undo.PerformUndo();
                Assert.That(graph.FindEdge(edgeId).edgeId, Is.EqualTo(edgeId));
                Assert.That(graph.FindEdge(edgeId).inputPortId, Is.EqualTo(Input(firstSink)));
                Undo.PerformRedo();
                Assert.That(graph.FindEdge(edgeId).edgeId, Is.EqualTo(edgeId));
                Assert.That(graph.FindEdge(edgeId).inputPortId, Is.EqualTo(Input(secondSink)));
            }
            finally
            {
                Undo.ClearAll();
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void StableGraph_LayoutChangeKeepsBakeCacheAndContentChangeInvalidatesIt()
        {
            ESTestGraphAsset graph = ScriptableObject.CreateInstance<ESTestGraphAsset>();
            try
            {
                ESGraphNodeRecord node = graph.AddNode("test.node", "Node", Vector2.zero, DefaultPorts);
                Assert.That(ESGraphSnapshotBaker.TryBake(graph, out ESBakedGraphSnapshot snapshot,
                    out List<ESGraphValidationIssue> issues), Is.True,
                    string.Join("\n", issues.Select(issue => issue.message)));
                ESGraphBakeCache.Store(graph, false, snapshot, null, issues);

                ESGraphChange observed = default;
                int notifyCount = 0;
                var service = new ESGraphEditService(null, null, change =>
                {
                    observed = change;
                    notifyCount++;
                    ESGraphBakeCache.NotifyChanged(graph, change);
                });

                ESGraphEditResult moved = service.SetNodePositions(graph,
                    new Dictionary<string, Vector2> { { node.nodeId, Vector2.one } }, "移动图节点");
                Assert.That(moved.changed, Is.True);
                Assert.That(observed.Kind, Is.EqualTo(ESGraphChangeKind.Layout));
                Assert.That(observed.AffectsBake, Is.False);
                Assert.That(ESGraphBakeCache.TryGet(graph, false, out ESBakedGraphSnapshot cached,
                    out _, out _), Is.True);
                Assert.That(cached, Is.SameAs(snapshot));

                ESGraphEditResult noChange = service.SetNodeContent(graph, node.nodeId,
                    node.typeId, node.version, node.title, node.payloadJson);
                Assert.That(noChange.changed, Is.False);
                Assert.That(notifyCount, Is.EqualTo(1));

                ESGraphEditResult content = service.SetNodeContent(graph, node.nodeId,
                    node.typeId, node.version, "Changed", node.payloadJson);
                Assert.That(content.changed, Is.True);
                Assert.That(observed.Kind, Is.EqualTo(ESGraphChangeKind.Content));
                Assert.That(observed.AffectsBake, Is.True);
                Assert.That(ESGraphBakeCache.TryGet(graph, false, out _, out _, out _), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }

        [TestCase(ESGraphDomainIds.Generic)]
        [TestCase(ESGraphDomainIds.Story)]
        [TestCase(ESGraphDomainIds.BehaviorTree)]
        [TestCase(ESAgentGraphStableIds.DomainId)]
        public void StableGraph_AllDomainsUseTheSameReconnectInfrastructure(string domainId)
        {
            ESTestGraphAsset graph = ScriptableObject.CreateInstance<ESTestGraphAsset>();
            try
            {
                Assert.That(graph.InitializeTestDomain(domainId, out string domainError), Is.True, domainError);
                ESGraphNodeRecord source = graph.AddNode("test.source", "A", Vector2.zero, DefaultPorts);
                ESGraphNodeRecord firstSink = graph.AddNode("test.sink", "B", Vector2.right, DefaultPorts);
                ESGraphNodeRecord secondSink = graph.AddNode("test.sink", "C", Vector2.right * 2f, DefaultPorts);
                Assert.That(graph.TryAddEdge(Output(source), Input(firstSink), out ESGraphEdgeRecord edge,
                    out string addError), Is.True, addError);
                Assert.That(graph.TryReconnectEdge(edge.edgeId, Output(source), Input(secondSink),
                    out string reconnectError), Is.True, reconnectError);
                Assert.That(graph.FindEdge(edge.edgeId).inputPortId, Is.EqualTo(Input(secondSink)));
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void StableGraph_AgentReconnectStillRejectsCrossTypePorts()
        {
            ESTestGraphAsset graph = ScriptableObject.CreateInstance<ESTestGraphAsset>();
            try
            {
                Assert.That(graph.InitializeTestDomain(ESAgentGraphStableIds.DomainId, out string domainError),
                    Is.True, domainError);
                ESGraphNodeRecord contextSource = graph.AddNode("agent.context", "Context", Vector2.zero,
                    new[]
                    {
                        new ESGraphPortDefinition("上下文", "context.output", ESGraphPortDirection.Output,
                            ESGraphPortCapacity.Multi, ESAgentGraphStableIds.ContextPort)
                    });
                ESGraphNodeRecord contextSink = graph.AddNode("agent.context-sink", "Context Sink", Vector2.right,
                    new[]
                    {
                        new ESGraphPortDefinition("上下文", "context.input", ESGraphPortDirection.Input,
                            ESGraphPortCapacity.Single, ESAgentGraphStableIds.ContextPort)
                    });
                ESGraphNodeRecord artifactSource = graph.AddNode("agent.artifact", "Artifact", Vector2.left,
                    new[]
                    {
                        new ESGraphPortDefinition("产物", "artifact.output", ESGraphPortDirection.Output,
                            ESGraphPortCapacity.Multi, ESAgentGraphStableIds.ArtifactPort)
                    });
                Assert.That(graph.TryAddEdge(contextSource.ports[0].portId, contextSink.ports[0].portId,
                    out ESGraphEdgeRecord edge, out string addError), Is.True, addError);

                AssertReconnectRejectedWithoutMutation(graph, edge.edgeId,
                    artifactSource.ports[0].portId, contextSink.ports[0].portId,
                    "Agent cross-type reconnect");
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void StableGraph_ReconnectPreviewCancellationNeverMutatesTheAsset()
        {
            ESTestGraphAsset graph = ScriptableObject.CreateInstance<ESTestGraphAsset>();
            ESStableGraphView view = null;
            try
            {
                ESGraphNodeRecord source = graph.AddNode("test.source", "A", Vector2.zero, DefaultPorts);
                ESGraphNodeRecord sink = graph.AddNode("test.sink", "B", Vector2.right, DefaultPorts);
                Assert.That(graph.TryAddEdge(Output(source), Input(sink), out ESGraphEdgeRecord edge,
                    out string addError), Is.True, addError);
                string before = EditorJsonUtility.ToJson(graph);
                view = new ESStableGraphView(null, null, new ESGraphEditService(null, null, null));
                view.SetAsset(graph);

                Assert.That(view.BeginEndpointReconnect(edge.edgeId, false, Vector2.zero), Is.True);
                view.CompleteEndpointReconnect(Vector2.zero);
                Assert.That(view.IsEndpointReconnectActive, Is.False);
                Assert.That(EditorJsonUtility.ToJson(graph), Is.EqualTo(before));

                Assert.That(view.BeginEndpointReconnect(edge.edgeId, true, Vector2.zero), Is.True);
                view.EndPointerInteraction();
                Assert.That(view.IsEndpointReconnectActive, Is.False);
                Assert.That(EditorJsonUtility.ToJson(graph), Is.EqualTo(before));

                Assert.That(view.BeginEndpointReconnect(edge.edgeId, false, Vector2.zero), Is.True);
                view.CancelEndpointReconnect();
                Assert.That(view.IsEndpointReconnectActive, Is.False);
                Assert.That(EditorJsonUtility.ToJson(graph), Is.EqualTo(before));
            }
            finally
            {
                view?.Dispose();
                Object.DestroyImmediate(graph);
            }
        }

        [UnityTest]
        public IEnumerator StableGraph_ReconnectEndpointEventsUseTheRealPanelWithoutGestureCompetition()
        {
            ESTestGraphAsset graph = ScriptableObject.CreateInstance<ESTestGraphAsset>();
            ESGraphPanelHostWindow window = null;
            ESStableGraphView view = null;
            try
            {
                ESGraphNodeRecord source = graph.AddNode("test.source", "A",
                    new Vector2(80f, 180f), DefaultPorts);
                ESGraphNodeRecord firstSink = graph.AddNode("test.sink", "B",
                    new Vector2(500f, 80f), DefaultPorts);
                ESGraphNodeRecord secondSink = graph.AddNode("test.sink", "C",
                    new Vector2(500f, 340f), DefaultPorts);
                Assert.That(graph.TryAddEdge(Output(source), Input(firstSink),
                    out ESGraphEdgeRecord edge, out string addError), Is.True, addError);
                string edgeId = edge.edgeId;

                window = ScriptableObject.CreateInstance<ESGraphPanelHostWindow>();
                window.position = new Rect(120f, 120f, 960f, 720f);
                view = new ESStableGraphView(null, null,
                    new ESGraphEditService(null, null, null));
                view.style.flexGrow = 1f;
                window.rootVisualElement.Add(view);
                window.Show();
                view.SetAsset(graph);
                window.Repaint();
                yield return null;
                yield return null;

                Assert.That(view.panel, Is.Not.Null, "测试必须运行在真实 UI Toolkit Panel。 ");
                Assert.That(view.worldBound.width, Is.GreaterThan(100f));
                ESStableGraphEdgeView edgeView = FindEdgeView(view, edgeId);
                SendMouseEnter(edgeView);
                ESStableGraphEndpointHandle inputHandle = view.GetEndpointHandle(false);
                Assert.That(inputHandle.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
                SendMouseLeave(edgeView);
                SendMouseEnter(inputHandle);
                Assert.That(inputHandle.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex),
                    "鼠标从边移动到共享手柄时，手柄不能提前消失。 ");

                SendMouseDown(inputHandle, inputHandle.worldBound.center);
                Assert.That(view.IsEndpointReconnectActive, Is.True);
                Assert.That(view.HasPendingEdgeReconnect, Is.False,
                    "端点手柄不能启动边主体长按续接。 ");
                Assert.That(view.HasCanvasPointerInteraction, Is.False,
                    "端点手柄不能启动画布拖动。 ");
                Assert.That(view.HasPortDragPreview, Is.False,
                    "端点手柄不能启动普通端口拖线。 ");

                Port targetPort = FindPortView(view, Input(secondSink));
                Vector2 targetPosition = targetPort.worldBound.center;
                AssertPickedPort(view, targetPort, targetPosition);
                SendMouseMove(targetPort, targetPosition);
                SendMouseUp(targetPort, targetPosition);

                ESGraphEdgeRecord reconnected = graph.FindEdge(edgeId);
                Assert.That(reconnected, Is.Not.Null);
                Assert.That(reconnected.edgeId, Is.EqualTo(edgeId));
                Assert.That(reconnected.inputPortId, Is.EqualTo(Input(secondSink)));
                Assert.That(view.IsEndpointReconnectActive, Is.False);
                Assert.That(view.HasPendingEdgeReconnect, Is.False);
                Assert.That(view.HasCanvasPointerInteraction, Is.False);
                Assert.That(view.HasPortDragPreview, Is.False);

                string afterSuccess = EditorJsonUtility.ToJson(graph);
                edgeView = FindEdgeView(view, edgeId);
                SendMouseEnter(edgeView);
                inputHandle = view.GetEndpointHandle(false);
                SendMouseDown(inputHandle, inputHandle.worldBound.center);
                Assert.That(view.IsEndpointReconnectActive, Is.True);
                inputHandle.ReleaseMouse();
                Assert.That(view.IsEndpointReconnectActive, Is.False,
                    "MouseCaptureOut 必须可重入地取消预览。 ");
                Assert.That(EditorJsonUtility.ToJson(graph), Is.EqualTo(afterSuccess));

                edgeView = FindEdgeView(view, edgeId);
                SendMouseEnter(edgeView);
                inputHandle = view.GetEndpointHandle(false);
                SendMouseDown(inputHandle, inputHandle.worldBound.center);
                Vector2 blankPosition = FindBlankPanelPosition(view);
                SendMouseMove(view, blankPosition);
                SendMouseUp(view, blankPosition);
                Assert.That(view.IsEndpointReconnectActive, Is.False);
                Assert.That(EditorJsonUtility.ToJson(graph), Is.EqualTo(afterSuccess),
                    "松开到空白区域必须保留原关系。 ");

                edgeView = FindEdgeView(view, edgeId);
                SendMouseEnter(edgeView);
                inputHandle = view.GetEndpointHandle(false);
                SendMouseDown(inputHandle, inputHandle.worldBound.center);
                Assert.That(view.IsEndpointReconnectActive, Is.True);
                SendKeyDown(view, KeyCode.Escape);
                Assert.That(view.IsEndpointReconnectActive, Is.False);
                Assert.That(EditorJsonUtility.ToJson(graph), Is.EqualTo(afterSuccess),
                    "Esc 必须取消预览且不修改资产。 ");
            }
            finally
            {
                view?.Dispose();
                if (window != null)
                    window.Close();
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void StableGraph_VisitedEdgesShareExactlyOneEndpointHandlePair()
        {
            const int EdgeCount = 64;
            ESTestGraphAsset graph = ScriptableObject.CreateInstance<ESTestGraphAsset>();
            ESStableGraphView view = null;
            try
            {
                for (int i = 0; i < EdgeCount; i++)
                {
                    ESGraphNodeRecord source = graph.AddNode("test.source", "S" + i,
                        new Vector2(0f, i * 120f), DefaultPorts);
                    ESGraphNodeRecord sink = graph.AddNode("test.sink", "T" + i,
                        new Vector2(480f, i * 120f), DefaultPorts);
                    Assert.That(graph.TryAddEdge(Output(source), Input(sink), out _,
                        out string addError), Is.True, addError);
                }

                view = new ESStableGraphView(null, null, new ESGraphEditService(null, null, null));
                view.SetAsset(graph);
                ESStableGraphEdgeView[] edges = view.Query<ESStableGraphEdgeView>().ToList().ToArray();
                Assert.That(edges, Has.Length.EqualTo(EdgeCount));
                Assert.That(view.EndpointHandleElementCount, Is.EqualTo(2));
                for (int i = 0; i < edges.Length; i++)
                {
                    SendMouseEnter(edges[i]);
                    Assert.That(view.EndpointHandleElementCount, Is.EqualTo(2), "edge " + i);
                    SendMouseLeave(edges[i]);
                    Assert.That(view.EndpointHandleElementCount, Is.EqualTo(2), "edge " + i);
                }
            }
            finally
            {
                view?.Dispose();
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void StableGraph_DuplicateNodesCopiesOnlyInternalEdgesWithFreshIdentity()
        {
            ESTestGraphAsset graph = ScriptableObject.CreateInstance<ESTestGraphAsset>();
            try
            {
                ESGraphNodeRecord first = graph.AddNode("test.node", "A", Vector2.zero, DefaultPorts);
                ESGraphNodeRecord second = graph.AddNode("test.node", "B", Vector2.right, DefaultPorts);
                Assert.That(graph.TryAddEdge(Output(first), Input(second), out _, out string error), Is.True, error);

                List<string> clones = graph.DuplicateNodes(new[] { first.nodeId, second.nodeId }, new Vector2(30f, 40f));
                Assert.That(clones.Count, Is.EqualTo(2));
                Assert.That(graph.Nodes.Count, Is.EqualTo(4));
                Assert.That(graph.Edges.Count, Is.EqualTo(2));
                Assert.That(graph.FindNode(clones[0]).position, Is.EqualTo(first.position + new Vector2(30f, 40f)));
                Assert.That(graph.ValidateGraph(), Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void StableGraph_PasteNodesRebuildsStableIdentityAndInternalEdges()
        {
            ESTestGraphAsset source = ScriptableObject.CreateInstance<ESTestGraphAsset>();
            ESTestGraphAsset target = ScriptableObject.CreateInstance<ESTestGraphAsset>();
            try
            {
                ESGraphNodeRecord first = source.AddNode("test.node", "A", Vector2.zero, DefaultPorts);
                ESGraphNodeRecord second = source.AddNode("test.node", "B", Vector2.right, DefaultPorts);
                Assert.That(source.TryAddEdge(Output(first), Input(second), out ESGraphEdgeRecord sourceEdge,
                    out string error), Is.True, error);

                List<string> created = target.PasteNodes(
                    new[] { first, second },
                    new[] { sourceEdge },
                    new Vector2(48f, 64f),
                    out string pasteError,
                    source.schemaVersion,
                    source.DomainId,
                    out int createdEdgeCount);
                Assert.That(created, Has.Count.EqualTo(2), pasteError);
                Assert.That(createdEdgeCount, Is.EqualTo(1));
                Assert.That(target.Nodes, Has.Count.EqualTo(2));
                Assert.That(target.Edges, Has.Count.EqualTo(1));
                Assert.That(target.Nodes[0].nodeId, Is.Not.EqualTo(first.nodeId));
                Assert.That(target.Nodes[0].ports[0].portId, Is.Not.EqualTo(first.ports[0].portId));
                Assert.That(target.Nodes[0].position, Is.EqualTo(first.position + new Vector2(48f, 64f)));
                Assert.That(target.Edges[0].edgeId, Is.Not.EqualTo(sourceEdge.edgeId));
                Assert.That(target.Edges[0].outputPortId,
                    Is.EqualTo(target.Nodes[0].ports[1].portId));
                Assert.That(target.Edges[0].inputPortId,
                    Is.EqualTo(target.Nodes[1].ports[0].portId));
                Assert.That(target.ValidateGraph(), Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void StableGraph_PasteNodesRejectsCrossDomainClipboardWithoutMutation()
        {
            ESTestGraphAsset source = ScriptableObject.CreateInstance<ESTestGraphAsset>();
            ESTestGraphAsset target = ScriptableObject.CreateInstance<ESTestGraphAsset>();
            try
            {
                Assert.That(source.InitializeTestDomain(ESGraphDomainIds.Story, out string domainError), Is.True,
                    domainError);
                ESGraphNodeRecord first = source.AddNode("test.node", "A", Vector2.zero, DefaultPorts);
                ESGraphNodeRecord second = source.AddNode("test.node", "B", Vector2.right, DefaultPorts);
                Assert.That(source.TryAddEdge(Output(first), Input(second), out ESGraphEdgeRecord edge,
                    out string edgeError), Is.True, edgeError);

                List<string> created = target.PasteNodes(
                    new[] { first, second },
                    new[] { edge },
                    Vector2.zero,
                    out string pasteError,
                    source.schemaVersion,
                    source.DomainId,
                    out _);
                Assert.That(created, Is.Empty);
                Assert.That(pasteError, Does.Contain("Domain"));
                Assert.That(target.Nodes, Is.Empty);
                Assert.That(target.Edges, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void StableGraph_PasteNodesRejectsUnknownEdgePortsWithoutMutation()
        {
            ESTestGraphAsset source = ScriptableObject.CreateInstance<ESTestGraphAsset>();
            ESTestGraphAsset target = ScriptableObject.CreateInstance<ESTestGraphAsset>();
            try
            {
                ESGraphNodeRecord first = source.AddNode("test.node", "A", Vector2.zero, DefaultPorts);
                ESGraphNodeRecord second = source.AddNode("test.node", "B", Vector2.right, DefaultPorts);
                ESGraphEdgeRecord invalidEdge = new ESGraphEdgeRecord
                {
                    edgeId = ESGraphIdentity.NewId(),
                    outputPortId = "missing-port",
                    inputPortId = Input(second)
                };

                List<string> created = target.PasteNodes(
                    new[] { first, second },
                    new[] { invalidEdge },
                    Vector2.zero,
                    out string pasteError,
                    source.schemaVersion,
                    source.DomainId,
                    out _);
                Assert.That(created, Is.Empty);
                Assert.That(pasteError, Does.Contain("未知端口"));
                Assert.That(target.Nodes, Is.Empty);
                Assert.That(target.Edges, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void StableGraph_PasteNodesRejectsSchema1WithoutExplicitMigration()
        {
            ESTestGraphAsset source = ScriptableObject.CreateInstance<ESTestGraphAsset>();
            ESTestGraphAsset target = ScriptableObject.CreateInstance<ESTestGraphAsset>();
            try
            {
                ESGraphNodeRecord first = source.AddNode("test.node", "A", Vector2.zero, DefaultPorts);
                List<string> created = target.PasteNodes(
                    new[] { first },
                    null,
                    Vector2.zero,
                    out string pasteError,
                    1,
                    source.DomainId,
                    out _);
                Assert.That(created, Is.Empty);
                Assert.That(pasteError, Does.Contain("显式迁移"));
                Assert.That(target.Nodes, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void StableGraph_PasteNodesRejectsTypeIncompatibleInternalEdge()
        {
            ESTestGraphAsset source = ScriptableObject.CreateInstance<ESTestGraphAsset>();
            ESTestGraphAsset target = ScriptableObject.CreateInstance<ESTestGraphAsset>();
            try
            {
                ESGraphNodeRecord numberSource = source.AddNode("test.source", "Number", Vector2.zero,
                    new[]
                    {
                        new ESGraphPortDefinition("输出", "number.output", ESGraphPortDirection.Output,
                            ESGraphPortCapacity.Single, "number")
                    });
                ESGraphNodeRecord flowSink = source.AddNode("test.sink", "Flow", Vector2.right,
                    new[]
                    {
                        new ESGraphPortDefinition("输入", "flow.input", ESGraphPortDirection.Input,
                            ESGraphPortCapacity.Single)
                    });
                ESGraphEdgeRecord invalidEdge = new ESGraphEdgeRecord
                {
                    edgeId = ESGraphIdentity.NewId(),
                    outputPortId = numberSource.ports[0].portId,
                    inputPortId = flowSink.ports[0].portId
                };

                List<string> created = target.PasteNodes(
                    new[] { numberSource, flowSink },
                    new[] { invalidEdge },
                    Vector2.zero,
                    out string pasteError,
                    source.schemaVersion,
                    source.DomainId,
                    out _);
                Assert.That(created, Is.Empty);
                Assert.That(pasteError, Does.Contain("违反图连接契约"));
                Assert.That(target.Nodes, Is.Empty);
                Assert.That(target.Edges, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void StableGraph_PasteNodesRejectsInvalidPortEnums()
        {
            ESTestGraphAsset source = ScriptableObject.CreateInstance<ESTestGraphAsset>();
            ESTestGraphAsset target = ScriptableObject.CreateInstance<ESTestGraphAsset>();
            try
            {
                ESGraphNodeRecord node = source.AddNode("test.node", "A", Vector2.zero,
                    new[]
                    {
                        new ESGraphPortDefinition("输入", "flow.input", ESGraphPortDirection.Input,
                            ESGraphPortCapacity.Single)
                    });
                node.ports[0].capacity = (ESGraphPortCapacity)255;

                List<string> created = target.PasteNodes(
                    new[] { node },
                    null,
                    Vector2.zero,
                    out string pasteError,
                    source.schemaVersion,
                    source.DomainId,
                    out _);
                Assert.That(created, Is.Empty);
                Assert.That(pasteError, Does.Contain("枚举非法"));
                Assert.That(target.Nodes, Is.Empty);

                ESTestGraphAsset directionSource = ScriptableObject.CreateInstance<ESTestGraphAsset>();
                ESTestGraphAsset directionTarget = ScriptableObject.CreateInstance<ESTestGraphAsset>();
                try
                {
                    ESGraphNodeRecord directionNode = directionSource.AddNode("test.node", "B", Vector2.zero,
                        new[]
                        {
                            new ESGraphPortDefinition("输入", "flow.input", ESGraphPortDirection.Input,
                                ESGraphPortCapacity.Single)
                        });
                    directionNode.ports[0].direction = (ESGraphPortDirection)255;
                    List<string> directionCreated = directionTarget.PasteNodes(
                        new[] { directionNode },
                        null,
                        Vector2.zero,
                        out string directionError,
                        directionSource.schemaVersion,
                        directionSource.DomainId,
                        out _);
                    Assert.That(directionCreated, Is.Empty);
                    Assert.That(directionError, Does.Contain("枚举非法"));
                    Assert.That(directionTarget.Nodes, Is.Empty);
                }
                finally
                {
                    Object.DestroyImmediate(directionSource);
                    Object.DestroyImmediate(directionTarget);
                }
            }
            finally
            {
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void StableGraph_NodeViewAlwaysShowsIndependentInfoAndDetailsButton()
        {
            ESTestGraphAsset asset = ScriptableObject.CreateInstance<ESTestGraphAsset>();
            try
            {
                ESGraphNodeRecord record = asset.AddNode("test.node", "A", Vector2.zero, DefaultPorts);
                ESStableGraphNodeTemplate definition = new ESStableGraphNodeTemplate(
                    ESGraphDomainKind.Generic,
                    ESGraphBuiltInNodeKind.GenericFlow,
                    "Test/Flow",
                    "流程",
                    ESGraphNodeCategory.Flow,
                    ESGraphNodeTheme.Primary,
                    new ESGraphPortDefinition("输入", "flow.input", ESGraphPortDirection.Input,
                        ESGraphPortCapacity.Single),
                    new ESGraphPortDefinition("输出", "flow.output", ESGraphPortDirection.Output,
                        ESGraphPortCapacity.Multi));
                ESStableGraphNodeView view = new ESStableGraphNodeView(
                    asset.DomainKey, record, definition, new NoopEdgeConnectorListener(), null);

                Assert.That(view.expanded, Is.True);
                Button details = view.Q<Button>("es-node-details");
                Assert.That(details, Is.Not.Null);
                Assert.That(details.text, Is.EqualTo("详情"));
                Assert.That(details.tooltip, Does.Contain("独立信息"));
                Assert.That(record.title, Is.EqualTo("A"));
                record.payloadJson = "{\"changed\":true}";
                Assert.That(view.MatchesRecord(record), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void StableGraph_AgentNodeCardProjectsAndCommitsSelectedKeyFields()
        {
            string committedPayload = null;
            string sourcePayload = JsonUtility.ToJson(new ESAgentGoalPayload
            {
                title = "原目标",
                objective = "原目的",
                context = "不在卡片中展开的上下文"
            });
            ESGraphNodeRecord node = CreateCardNode(sourcePayload);
            ESGraphNodeCardContext context = CreateCardContext(node,
                value => committedPayload = value,
                new[] { "incoming-node" },
                new[] { "outgoing-node-a", "outgoing-node-b" });

            bool created = ESGraphAuthoringRegistry.TryCreateNodeCard(context, out VisualElement card);

            Assert.That(created, Is.True);
            Assert.That(card, Is.Not.Null);
            Assert.That(card.userData, Is.SameAs(context));
            Assert.That(context.GraphId, Is.EqualTo("graph-card-test"));
            Assert.That(context.NodeId, Is.EqualTo(node.nodeId));
            Assert.That(context.IncomingNodeIds, Is.EqualTo(new[] { "incoming-node" }));
            Assert.That(context.OutgoingConnectionCount, Is.EqualTo(2));
            Assert.That(context.Ports, Has.Count.EqualTo(1));
            Assert.That(context.Ports[0].StableKey, Is.EqualTo("flow.output"));
            Assert.That(context.Ports[0].ConnectionCount, Is.EqualTo(1));
            TextField title = card.Q<TextField>("es-node-card-goal-title");
            Assert.That(title, Is.Not.Null);
            Assert.That(title.enabledSelf, Is.True);
            Assert.That(card.Q<TextField>("es-node-card-goal-objective"), Is.Not.Null);
            Assert.That(card.Q<TextField>("es-node-card-goal-context"), Is.Null);
            TextField relations = card.Q<TextField>("es-node-card-goal-relations");
            Assert.That(relations, Is.Not.Null);
            Assert.That(relations.isReadOnly, Is.True);
            Assert.That(relations.value, Is.EqualTo("1 入 / 2 出"));

            using (ChangeEvent<string> change = ChangeEvent<string>.GetPooled(title.value, "新目标"))
            {
                change.target = title;
                title.SendEvent(change);
            }
            Assert.That(committedPayload, Is.Not.Null.And.Not.Empty);
            Assert.That(JsonUtility.FromJson<ESAgentGoalPayload>(committedPayload).title,
                Is.EqualTo("新目标"));
        }

        [Test]
        public void StableGraph_AgentPathNodeCardProvidesCopyAndLocateActions()
        {
            string payload = JsonUtility.ToJson(new ESAgentReferencePayload());
            ESGraphNodeRecord node = CreateCardNode(payload,
                ESAgentGraphStableIds.ReferenceNode);
            ESGraphNodeCardContext context = CreateCardContext(node, _ => { });
            bool created = ESGraphAuthoringRegistry.TryCreateNodeCard(context, out VisualElement card);

            Assert.That(created, Is.True);
            Assert.That(card.Q<Button>("es-node-card-reference-actions-copy"), Is.Not.Null);
            Assert.That(card.Q<Button>("es-node-card-reference-actions-locate"), Is.Not.Null);
        }

        [Test]
        public void StableGraph_AgentOutputPayloadsSynchronizePracticalArtifactPaths()
        {
            var command = new ESAgentAICommandOutputPayload
            {
                commandName = "生成_资源检查_AI命令.md",
                targetProjectPath = "Assets/Plugins/ES/AICommands/旧命令.md"
            };
            Assert.That(command.SuggestedTargetProjectPath,
                Is.EqualTo("Assets/Plugins/ES/AICommands/生成_资源检查_AI命令.md"));
            Assert.That(command.SynchronizeTargetProjectPath(), Is.True);
            Assert.That(command.targetProjectPath, Is.EqualTo(command.SuggestedTargetProjectPath));
            Assert.That(command.SynchronizeTargetProjectPath(), Is.False);
            command.commandName = "../越界命令";
            string safeCommandPath = command.targetProjectPath;
            Assert.That(command.SynchronizeTargetProjectPath(), Is.False);
            Assert.That(command.targetProjectPath, Is.EqualTo(safeCommandPath));

            var skill = new ESAgentSkillOutputPayload
            {
                skillName = "es-resource-review",
                targetProjectPath = ".agents/skills/es-old/",
                includeAgentsMetadata = true,
                includeReferences = false,
                includeScripts = true
            };
            Assert.That(skill.SuggestedTargetProjectPath,
                Is.EqualTo(".agents/skills/es-resource-review/"));
            Assert.That(skill.InvocationToken, Is.EqualTo("$es-resource-review"));
            Assert.That(skill.IncludedContentSummary, Is.EqualTo("SKILL.md · agents/openai.yaml · scripts/"));
            Assert.That(skill.SynchronizeTargetProjectPath(), Is.True);
            Assert.That(skill.targetProjectPath, Is.EqualTo(skill.SuggestedTargetProjectPath));
            Assert.That(skill.SynchronizeTargetProjectPath(), Is.False);
        }

        [Test]
        public void StableGraph_AgentOutputCardsExposeEditableMetadataStatusAndArtifactActions()
        {
            var commandPayload = new ESAgentAICommandOutputPayload
            {
                commandName = "生成_不存在的卡片测试_AI命令",
                targetProjectPath = "Assets/Plugins/ES/AICommands/__ESMissingNodeCardCommand__.md",
                operationMode = ESAgentArtifactOperationMode.UpdateOnly,
                commandIntent = ESAgentCommandIntent.ControlledExecution,
                riskLevel = ESAgentRiskLevel.L2
            };
            ESGraphNodeRecord commandNode = CreateCardNode(JsonUtility.ToJson(commandPayload),
                ESAgentGraphStableIds.AICommandOutputNode);
            ESGraphNodeCardContext commandContext = CreateCardContext(commandNode, _ => { },
                canExecuteAction: _ => true, executeAction: _ => { });
            Assert.That(ESGraphAuthoringRegistry.TryCreateNodeCard(commandContext, out VisualElement commandCard),
                Is.True);
            Assert.That(commandCard.Q<PopupField<string>>("es-node-card-command-intent"), Is.Not.Null);
            Assert.That(commandCard.Q<PopupField<string>>("es-node-card-command-write"), Is.Not.Null);
            Assert.That(commandCard.Q<PopupField<string>>("es-node-card-command-risk"), Is.Not.Null);
            TextField commandStatus = commandCard.Q<TextField>("es-node-card-command-status");
            Assert.That(commandStatus, Is.Not.Null);
            Assert.That(commandStatus.isReadOnly, Is.True);
            Assert.That(commandStatus.value, Does.Contain("仅更新将阻断"));
            Assert.That(commandCard.Q<Button>("es-node-card-command-actions-use")?.enabledSelf, Is.True);
            Assert.That(commandCard.Q<Button>("es-node-card-command-actions-candidate")?.enabledSelf, Is.True);
            Assert.That(commandCard.Q<Button>("es-node-card-command-actions-sync"), Is.Not.Null);
            Assert.That(commandCard.Q<Button>("es-node-card-command-actions-copy"), Is.Not.Null);
            Assert.That(commandCard.Q<Button>("es-node-card-command-actions-locate"), Is.Not.Null);

            var skillPayload = new ESAgentSkillOutputPayload
            {
                skillName = "es-node-card-test",
                description = "生成并验证节点卡片能力。",
                includeAgentsMetadata = true,
                includeReferences = true,
                includeScripts = false
            };
            ESGraphNodeRecord skillNode = CreateCardNode(JsonUtility.ToJson(skillPayload),
                ESAgentGraphStableIds.AISkillOutputNode);
            ESGraphNodeCardContext skillContext = CreateCardContext(skillNode, _ => { },
                canExecuteAction: _ => true, executeAction: _ => { });
            Assert.That(ESGraphAuthoringRegistry.TryCreateNodeCard(skillContext, out VisualElement skillCard),
                Is.True);
            Assert.That(skillCard.Q<TextField>("es-node-card-skill-summary")?.isReadOnly, Is.True);
            Assert.That(skillCard.Q<PopupField<string>>("es-node-card-skill-effect"), Is.Not.Null);
            Assert.That(skillCard.Q<PopupField<string>>("es-node-card-skill-idempotency"), Is.Not.Null);
            Assert.That(skillCard.Q<Toggle>("es-node-card-skill-agents-metadata"), Is.Not.Null);
            Assert.That(skillCard.Q<Toggle>("es-node-card-skill-references"), Is.Not.Null);
            Assert.That(skillCard.Q<Toggle>("es-node-card-skill-scripts"), Is.Not.Null);
            Assert.That(skillCard.Q<TextField>("es-node-card-skill-structure")?.value,
                Is.EqualTo("SKILL.md · agents/openai.yaml · references/"));
            Assert.That(skillCard.Q<Button>("es-node-card-skill-actions-use")?.text, Is.EqualTo("临时使用"));
            Assert.That(skillCard.Q<Button>("es-node-card-skill-actions-candidate")?.enabledSelf, Is.True);
            Assert.That(skillCard.Q<Button>("es-node-card-skill-actions-sync"), Is.Not.Null);
            Assert.That(skillCard.Q<Button>("es-node-card-skill-actions-invocation"), Is.Not.Null);
            Assert.That(skillCard.Q<Button>("es-node-card-skill-actions-copy"), Is.Not.Null);
            Assert.That(skillCard.Q<Button>("es-node-card-skill-actions-locate"), Is.Not.Null);
        }

        [Test]
        public void StableGraph_AgentOutputCardReadOnlyModeDisablesPayloadMutations()
        {
            bool committed = false;
            ESGraphNodeRecord node = CreateCardNode(JsonUtility.ToJson(new ESAgentSkillOutputPayload()),
                ESAgentGraphStableIds.AISkillOutputNode);
            ESGraphNodeCardContext context = CreateCardContext(node, _ => committed = true, isReadOnly: true,
                canExecuteAction: _ => true, executeAction: _ => { });

            Assert.That(ESGraphAuthoringRegistry.TryCreateNodeCard(context, out VisualElement card), Is.True);
            TextField name = card.Q<TextField>("es-node-card-skill-name");
            Toggle metadata = card.Q<Toggle>("es-node-card-skill-agents-metadata");
            Assert.That(name?.isReadOnly, Is.True);
            Assert.That(metadata?.enabledSelf, Is.False);
            Assert.That(card.Q<Button>("es-node-card-skill-actions-use")?.enabledSelf, Is.True);
            Assert.That(card.Q<Button>("es-node-card-skill-actions-candidate")?.enabledSelf, Is.True);
            Assert.That(card.Q<Button>("es-node-card-skill-actions-sync")?.enabledSelf, Is.False);
            Assert.That(card.Q<Button>("es-node-card-skill-actions-copy")?.enabledSelf, Is.True);
            Assert.That(card.Q<Button>("es-node-card-skill-actions-invocation")?.enabledSelf, Is.True);

            using (ChangeEvent<string> textChange = ChangeEvent<string>.GetPooled(name.value, "es-illegal-change"))
            {
                textChange.target = name;
                name.SendEvent(textChange);
            }
            using (ChangeEvent<bool> toggleChange = ChangeEvent<bool>.GetPooled(metadata.value, !metadata.value))
            {
                toggleChange.target = metadata;
                metadata.SendEvent(toggleChange);
            }
            Assert.That(committed, Is.False);
        }

        [Test]
        public void StableGraph_NodeCardContextKeepsMutationAndNavigationBehindControlledActions()
        {
            bool committed = false;
            bool opened = false;
            string focused = null;
            string selected = null;
            string reported = null;
            string copied = null;
            ESGraphNodeCardActionKey? executedAction = null;
            ESGraphNodeCardActionKey customAction =
                ESGraphNodeCardActionKey.FromStableId("es.test.node-card.custom-action");
            ESGraphNodeCardActionKey unsupportedAction =
                ESGraphNodeCardActionKey.FromStableId("es.test.node-card.unsupported-action");
            ESGraphNodeRecord node = CreateCardNode("{}");
            ESGraphNodeCardContext context = CreateCardContext(node, _ => committed = true,
                openDetails: () => opened = true,
                focusNode: value => focused = value,
                selectNode: value => selected = value,
                report: value => reported = value,
                copyText: value => copied = value,
                canExecuteAction: value => value == customAction,
                executeAction: value => executedAction = value);

            Assert.That(context.CommitPayload("{\"changed\":true}"), Is.True);
            context.OpenDetails();
            context.FocusNode("focus-target");
            context.SelectNode("select-target");
            context.Report("status");
            context.CopyText("copy-value");
            Assert.That(context.CanExecuteNodeAction(customAction), Is.True);
            Assert.That(context.CanExecuteNodeAction(unsupportedAction), Is.False);
            Assert.That(context.ExecuteNodeAction(customAction), Is.True);

            Assert.That(committed, Is.True);
            Assert.That(opened, Is.True);
            Assert.That(focused, Is.EqualTo("focus-target"));
            Assert.That(selected, Is.EqualTo("select-target"));
            Assert.That(reported, Is.EqualTo("status"));
            Assert.That(copied, Is.EqualTo("copy-value"));
            Assert.That(executedAction, Is.EqualTo(customAction));
            Assert.That(context.ExecuteNodeAction(unsupportedAction), Is.False);
            Assert.That(reported, Does.Contain(unsupportedAction.StableId));
            Assert.That(executedAction, Is.EqualTo(customAction));
            foreach (System.Reflection.PropertyInfo property in typeof(ESGraphNodeCardContext).GetProperties())
                Assert.That(typeof(ESGraphAssetBase).IsAssignableFrom(property.PropertyType), Is.False,
                    property.Name + " 不得暴露可变 Graph Asset。");
            foreach (System.Reflection.FieldInfo field in typeof(ESGraphNodeCardContext).GetFields(
                         System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public
                         | System.Reflection.BindingFlags.NonPublic))
                Assert.That(typeof(ESGraphAssetBase).IsAssignableFrom(field.FieldType), Is.False,
                    field.Name + " 不得持有可变 Graph Asset。");
        }

        [Test]
        public void StableGraph_NodeCardContextReadOnlyModeRejectsPayloadMutationButKeepsActionsAvailable()
        {
            bool committed = false;
            string reported = null;
            ESGraphNodeRecord node = CreateCardNode(JsonUtility.ToJson(new ESAgentGoalPayload()));
            ESGraphNodeCardContext context = CreateCardContext(node, _ => committed = true,
                isReadOnly: true, hasFutureSchema: true, report: value => reported = value);

            Assert.That(context.CanEditPayload, Is.False);
            Assert.That(context.CommitPayload("{\"blocked\":true}"), Is.False);
            Assert.That(committed, Is.False);
            Assert.That(reported, Does.Contain("未来版本"));
            Assert.That(ESGraphAuthoringRegistry.TryCreateNodeCard(context, out VisualElement card), Is.True);
            TextField editableProjection = card.Q<TextField>("es-node-card-goal-title");
            Assert.That(editableProjection, Is.Not.Null);
            Assert.That(editableProjection.isReadOnly, Is.True);
            Assert.That(editableProjection.enabledSelf, Is.True);
        }

        [Test]
        public void StableGraph_NodeCardActionRegistryRequiresExactNodeTypeAndActionRoute()
        {
            ESTestGraphAsset graph = ScriptableObject.CreateInstance<ESTestGraphAsset>();
            try
            {
                Assert.That(graph.InitializeTestDomain(ESAgentGraphStableIds.Domain,
                        out string domainError),
                    Is.True, domainError);
                ESGraphNodeRecord output = graph.AddNode(
                    ESAgentGraphStableIds.Node(ESAgentGraphStableIds.AICommandOutputNode),
                    "Command Output", Vector2.zero, DefaultPorts);
                ESGraphNodeRecord goal = graph.AddNode(
                    ESAgentGraphStableIds.Node(ESAgentGraphStableIds.GoalNode),
                    "Goal", Vector2.right, DefaultPorts);
                var outputContext = new ESGraphNodeCardActionContext(graph, output,
                    false, false, null, null);
                var goalContext = new ESGraphNodeCardActionContext(graph, goal,
                    false, false, null, null);
                ESGraphNodeCardActionKey unknown =
                    ESGraphNodeCardActionKey.FromStableId("es.test.node-card.unregistered-action");

                Assert.That(ESGraphAuthoringRegistry.CanExecuteNodeCardAction(outputContext,
                    ESAgentNodeCardActionKeys.UseOnce, out string outputError), Is.True, outputError);
                Assert.That(ESGraphAuthoringRegistry.CanExecuteNodeCardAction(outputContext,
                    unknown, out _), Is.False);
                Assert.That(ESGraphAuthoringRegistry.CanExecuteNodeCardAction(goalContext,
                    ESAgentNodeCardActionKeys.UseOnce, out _), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void StableGraph_NodeCardContextUsesIndexedGraphConnections()
        {
            ESTestGraphAsset graph = ScriptableObject.CreateInstance<ESTestGraphAsset>();
            ESStableGraphView graphView = null;
            try
            {
                Assert.That(graph.InitializeTestDomain(ESAgentGraphStableIds.DomainId, out string domainError),
                    Is.True, domainError);
                string goalPayload = JsonUtility.ToJson(new ESAgentGoalPayload { title = "Source" });
                ESGraphNodeRecord source = graph.AddNode(
                    ESAgentGraphStableIds.GoalNode,
                    "Source", Vector2.zero, DefaultPorts);
                ESGraphNodeRecord target = graph.AddNode(
                    ESAgentGraphStableIds.GoalNode,
                    "Target", Vector2.right * 320f, DefaultPorts);
                source.payloadJson = goalPayload;
                target.payloadJson = goalPayload;
                Assert.That(graph.TryAddEdge(Output(source), Input(target), out _, out string edgeError),
                    Is.True, edgeError);

                graphView = new ESStableGraphView(null, null);
                graphView.SetAsset(graph);
                ESStableGraphNodeView sourceView = null;
                foreach (Node candidate in graphView.nodes)
                {
                    if (candidate is ESStableGraphNodeView stable
                        && string.Equals(stable.NodeId, source.nodeId, System.StringComparison.Ordinal))
                    {
                        sourceView = stable;
                        break;
                    }
                }

                Assert.That(sourceView, Is.Not.Null);
                VisualElement card = sourceView.Q<VisualElement>("es-node-key-fields");
                Assert.That(card, Is.Not.Null);
                Assert.That(card.userData, Is.TypeOf<ESGraphNodeCardContext>());
                ESGraphNodeCardContext context = (ESGraphNodeCardContext)card.userData;
                Assert.That(context.GraphId, Is.EqualTo(graph.GraphId));
                Assert.That(context.NodeId, Is.EqualTo(source.nodeId));
                Assert.That(context.OutgoingNodeIds, Is.EqualTo(new[] { target.nodeId }));
                Assert.That(context.IncomingConnectionCount, Is.Zero);
                Assert.That(context.Ports[1].ConnectionCount, Is.EqualTo(1));
            }
            finally
            {
                graphView?.Dispose();
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void StableGraph_EditServiceCreateNodeReturnsRebuildResult()
        {
            ESTestGraphAsset asset = ScriptableObject.CreateInstance<ESTestGraphAsset>();
            try
            {
                ESStableGraphNodeTemplate definition = new ESStableGraphNodeTemplate(
                    ESGraphDomainKind.Generic,
                    ESGraphBuiltInNodeKind.GenericFlow,
                    "Test/Flow",
                    "流程",
                    ESGraphNodeCategory.Flow,
                    ESGraphNodeTheme.Primary,
                    new ESGraphPortDefinition("输入", "flow.input", ESGraphPortDirection.Input,
                        ESGraphPortCapacity.Single),
                    new ESGraphPortDefinition("输出", "flow.output", ESGraphPortDirection.Output,
                        ESGraphPortCapacity.Multi));
                var service = new ESGraphEditService(null, null, null);
                ESGraphEditResult result = service.CreateNode(asset, definition, new Vector2(80f, 120f));

                Assert.That(result.changed, Is.True);
                Assert.That(result.rebuildRequired, Is.True);
                Assert.That(result.createdNodeIds, Has.Count.EqualTo(1));
                Assert.That(asset.FindNode(result.createdNodeIds[0]).position,
                    Is.EqualTo(new Vector2(80f, 120f)));
                Assert.That(asset.ValidateGraph(), Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void StableGraph_RemoveNodeAlsoRemovesIncidentEdges()
        {
            ESTestGraphAsset graph = ScriptableObject.CreateInstance<ESTestGraphAsset>();
            try
            {
                ESGraphNodeRecord first = graph.AddNode("test.node", "A", Vector2.zero, DefaultPorts);
                ESGraphNodeRecord second = graph.AddNode("test.node", "B", Vector2.right, DefaultPorts);
                Assert.That(graph.TryAddEdge(Output(first), Input(second), out _, out string error), Is.True, error);
                Assert.That(graph.RemoveNode(first.nodeId), Is.True);
                Assert.That(graph.Nodes.Count, Is.EqualTo(1));
                Assert.That(graph.Edges, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void StableGraph_InsertionMutationRollsBackAtomically()
        {
            ESTestGraphAsset graph = ScriptableObject.CreateInstance<ESTestGraphAsset>();
            try
            {
                ESGraphNodeRecord first = graph.AddNode("test.source", "A", Vector2.zero, DefaultPorts);
                ESGraphNodeRecord second = graph.AddNode("test.sink", "B", Vector2.right, DefaultPorts);
                Assert.That(graph.TryAddEdge(Output(first), Input(second), out ESGraphEdgeRecord original,
                    out string setupError), Is.True, setupError);

                ESStableGraphNodeTemplate invalidDefinition = new ESStableGraphNodeTemplate(
                    ESGraphDomainKey.FromKind(ESGraphDomainKind.Generic),
                    ESGraphNodeTypeKey.Custom("es.test.insert"),
                    "Test/Insert",
                    "Insert",
                    string.Empty,
                    ESGraphNodeCategory.General,
                    ESGraphNodeTheme.Neutral,
                    "Insert",
                    string.Empty,
                    1,
                    0,
                    default,
                    new ESGraphPortDefinition("输入", "flow.input", ESGraphPortDirection.Input,
                        ESGraphPortCapacity.Single),
                    new ESGraphPortDefinition("输出", "number.output", ESGraphPortDirection.Output,
                        ESGraphPortCapacity.Single, "number"));

                var service = new ESGraphEditService(null, null, null);
                ESGraphEditResult insertResult = service.InsertNodeOnEdge(
                    graph,
                    invalidDefinition,
                    0,
                    1,
                    original.edgeId,
                    Vector2.one);
                Assert.That(insertResult.changed, Is.False, insertResult.error);
                Assert.That(insertResult.createdNodeIds, Is.Null);
                Assert.That(insertResult.error, Is.Not.Empty);
                Assert.That(graph.Nodes, Has.Count.EqualTo(2));
                Assert.That(graph.Edges, Has.Count.EqualTo(1));
                Assert.That(graph.Edges[0].edgeId, Is.EqualTo(original.edgeId));
            }
            finally
            {
                Undo.ClearAll();
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void StableGraph_UndoRestoresAtomicAssetMutation()
        {
            ESTestGraphAsset graph = ScriptableObject.CreateInstance<ESTestGraphAsset>();
            try
            {
                Undo.ClearAll();
                Undo.RecordObject(graph, "Add stable graph node");
                graph.AddNode("test.node", "A", Vector2.zero, DefaultPorts);
                Undo.FlushUndoRecordObjects();
                Assert.That(graph.Nodes.Count, Is.EqualTo(1));

                Undo.PerformUndo();
                Assert.That(graph.Nodes.Count, Is.EqualTo(0));
            }
            finally
            {
                Undo.ClearAll();
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void StableGraph_DeepAcyclicValidationDoesNotUseRecursiveTraversal()
        {
            ESTestGraphAsset graph = ScriptableObject.CreateInstance<ESTestGraphAsset>();
            try
            {
                const int nodeCount = 1500;
                ESGraphNodeRecord previous = null;
                for (int i = 0; i < nodeCount; i++)
                {
                    ESGraphNodeRecord current = graph.AddNode("test.node", i.ToString(), new Vector2(i, 0f), DefaultPorts);
                    if (previous != null)
                        Assert.That(graph.TryAddEdge(Output(previous), Input(current), out _, out string error), Is.True, error);
                    previous = current;
                }
                Assert.That(graph.ValidateGraph(), Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void StableGraph_NodeAndPortEditsValidateBeforeMutation()
        {
            ESTestGraphAsset graph = ScriptableObject.CreateInstance<ESTestGraphAsset>();
            try
            {
                ESGraphNodeRecord node = graph.AddNode("test.node", "A", Vector2.zero, DefaultPorts);
                Assert.That(graph.UpdateNode(node.nodeId, "test.changed", 2, "Changed", "{\"value\":1}", out string nodeError),
                    Is.True, nodeError);
                Assert.That(node.typeId, Is.EqualTo("test.changed"));
                Assert.That(node.version, Is.EqualTo(2));
                Assert.That(graph.UpdateNode(node.nodeId, string.Empty, 0, "Invalid", string.Empty, out _), Is.False);
                Assert.That(node.typeId, Is.EqualTo("test.changed"), "Rejected edits must not partially mutate the node.");

                Assert.That(graph.AddPort(node.nodeId,
                    new ESGraphPortDefinition("数据", "data.input", ESGraphPortDirection.Input,
                        ESGraphPortCapacity.Single, "number"), out string addError), Is.Not.Null, addError);
                Assert.That(graph.AddPort(node.nodeId,
                    new ESGraphPortDefinition("重复", "data.input", ESGraphPortDirection.Output,
                        ESGraphPortCapacity.Single, "number"), out _), Is.Null);
                Assert.That(graph.ValidateGraph(), Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void StableGraph_RemovePortAlsoRemovesIncidentEdges()
        {
            ESTestGraphAsset graph = ScriptableObject.CreateInstance<ESTestGraphAsset>();
            try
            {
                ESGraphNodeRecord first = graph.AddNode("test.node", "A", Vector2.zero, DefaultPorts);
                ESGraphNodeRecord second = graph.AddNode("test.node", "B", Vector2.right, DefaultPorts);
                Assert.That(graph.TryAddEdge(Output(first), Input(second), out _, out string error), Is.True, error);
                Assert.That(graph.RemovePort(Output(first)), Is.True);
                Assert.That(graph.Edges, Is.Empty);
                Assert.That(graph.ValidateGraph(), Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void StableGraph_ConnectedPortRejectsDirectionTypeAndCapacityViolations()
        {
            ESTestGraphAsset graph = ScriptableObject.CreateInstance<ESTestGraphAsset>();
            try
            {
                ESGraphNodeRecord source = graph.AddNode("test.source", "Source", Vector2.zero,
                    new[] { new ESGraphPortDefinition("输出", "flow.output", ESGraphPortDirection.Output, ESGraphPortCapacity.Multi) });
                ESGraphNodeRecord firstSink = graph.AddNode("test.sink", "A", Vector2.right, DefaultPorts);
                ESGraphNodeRecord secondSink = graph.AddNode("test.sink", "B", Vector2.right * 2f, DefaultPorts);
                string output = Output(source);
                Assert.That(graph.TryAddEdge(output, Input(firstSink), out _, out string firstError), Is.True, firstError);
                Assert.That(graph.TryAddEdge(output, Input(secondSink), out _, out string secondError), Is.True, secondError);

                Assert.That(graph.UpdatePort(output, "flow.output", "输出", "输出流程", "flow",
                    ESGraphPortDirection.Input, ESGraphPortCapacity.Multi,
                    ESGraphPortAggregation.Auto, out _), Is.False);
                Assert.That(graph.UpdatePort(output, "flow.output", "输出", "输出流程", "number",
                    ESGraphPortDirection.Output, ESGraphPortCapacity.Multi,
                    ESGraphPortAggregation.Auto, out _), Is.False);
                Assert.That(graph.UpdatePort(output, "flow.output", "输出", "输出流程", "flow",
                    ESGraphPortDirection.Output, ESGraphPortCapacity.Single,
                    ESGraphPortAggregation.Auto, out _), Is.False);
                Assert.That(source.ports[0].direction, Is.EqualTo(ESGraphPortDirection.Output));
                Assert.That(source.ports[0].valueTypeId, Is.EqualTo("flow"));
                Assert.That(source.ports[0].capacity, Is.EqualTo(ESGraphPortCapacity.Multi));
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void StableGraph_PortMeaningRejectsEmptyAndOversizedUpdatesWithoutMutation()
        {
            ESTestGraphAsset graph = ScriptableObject.CreateInstance<ESTestGraphAsset>();
            try
            {
                ESGraphNodeRecord node = graph.AddNode("test.node", "Node", Vector2.zero,
                    DefaultPorts);
                Assert.That(node.TryGetPort(ESGraphBuiltInPortKeys.Output,
                    out ESGraphPortRecord output), Is.True);
                string originalMeaning = output.meaning;

                Assert.That(graph.UpdatePort(output.portId, output.stableKey, output.name,
                    string.Empty, output.valueTypeId, output.direction, output.capacity,
                    output.aggregation, out _), Is.False);
                Assert.That(graph.UpdatePort(output.portId, output.stableKey, output.name,
                    new string('x', ESGraphEndpointRules.MaxMeaningLength + 1), output.valueTypeId,
                    output.direction, output.capacity, output.aggregation, out _), Is.False);
                Assert.That(output.meaning, Is.EqualTo(originalMeaning));
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void StableGraph_SnapshotIsDetachedAndSupportsStableIdLookup()
        {
            ESTestGraphAsset graph = ScriptableObject.CreateInstance<ESTestGraphAsset>();
            try
            {
                ESGraphNodeRecord first = graph.AddNode("test.node", "A", Vector2.zero, DefaultPorts);
                ESGraphNodeRecord second = graph.AddNode("test.node", "B", Vector2.right, DefaultPorts);
                Assert.That(graph.TryAddEdge(Output(first), Input(second), out ESGraphEdgeRecord edge, out string edgeError),
                    Is.True, edgeError);
                Assert.That(ESGraphSnapshotBaker.TryBake(graph, out ESBakedGraphSnapshot snapshot,
                    out List<ESGraphValidationIssue> issues), Is.True, string.Join(";", issues.ConvertAll(issue => issue.message)));

                Assert.That(snapshot.TryGetNode(first.nodeId, out ESGraphNodeSnapshot nodeSnapshot), Is.True);
                Assert.That(snapshot.TryGetPort(Output(first), out ESGraphPortSnapshot portSnapshot), Is.True);
                Assert.That(snapshot.TryGetPort(first.nodeId, "flow.output",
                    out ESGraphPortSnapshot endpointPort), Is.True);
                Assert.That(endpointPort.PortId, Is.EqualTo(portSnapshot.PortId));
                Assert.That(snapshot.TryGetEdge(edge.edgeId, out ESGraphEdgeSnapshot edgeSnapshot), Is.True);
                Assert.That(portSnapshot.PortId, Is.EqualTo(Output(first)));
                Assert.That(edgeSnapshot.EdgeId, Is.EqualTo(edge.edgeId));

                Assert.That(graph.UpdateNode(first.nodeId, "test.changed", 3, "Changed", "{}", out string updateError),
                    Is.True, updateError);
                first.ports[1].name = "Changed Port";
                Assert.That(nodeSnapshot.TypeId, Is.EqualTo("test.node"));
                Assert.That(nodeSnapshot.Title, Is.EqualTo("A"));
                Assert.That(portSnapshot.Name, Is.EqualTo("输出"));
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void StableGraph_EndpointAggregationIsBakedAndSigned()
        {
            ESTestGraphAsset graph = ScriptableObject.CreateInstance<ESTestGraphAsset>();
            try
            {
                ESGraphNodeRecord sourceA = graph.AddNode("test.source", "A", Vector2.zero,
                    new[] { new ESGraphPortDefinition("值", "data.value", ESGraphPortDirection.Output,
                        ESGraphPortCapacity.Multi, ESGraphPortValueIds.Text) });
                ESGraphNodeRecord sourceB = graph.AddNode("test.source", "B", Vector2.right,
                    new[] { new ESGraphPortDefinition("值", "data.value", ESGraphPortDirection.Output,
                        ESGraphPortCapacity.Multi, ESGraphPortValueIds.Text) });
                ESGraphNodeRecord target = graph.AddNode("test.target", "Target", Vector2.right * 2f,
                    new[] { new ESGraphPortDefinition("输入", "data.input", ESGraphPortDirection.Input,
                        ESGraphPortCapacity.Multi, ESGraphPortValueIds.Text, ESGraphPortAggregation.Named) });
                Assert.That(graph.TryAddEdge(Output(sourceA), target.ports[0].portId,
                    out _, out string firstError), Is.True, firstError);
                Assert.That(graph.TryAddEdge(Output(sourceB), target.ports[0].portId,
                    out _, out string secondError), Is.True, secondError);
                Assert.That(ESGraphSnapshotBaker.TryBake(graph, out ESBakedGraphSnapshot named,
                    out List<ESGraphValidationIssue> issues), Is.True,
                    string.Join(";", issues.Select(issue => issue.message)));
                Assert.That(named.Routes.Count, Is.EqualTo(2));
                Assert.That(named.Routes.All(route => route.TargetAggregation == ESGraphPortAggregation.Named),
                    Is.True);
                Assert.That(named.Routes.All(route => route.SourceAggregation == ESGraphPortAggregation.Single),
                    Is.True);

                string originalSignature = named.ContentSignature;
                Assert.That(graph.UpdatePort(target.ports[0].portId, "data.input", "输入", "接收文本列表",
                    ESGraphPortValueIds.Text, ESGraphPortDirection.Input, ESGraphPortCapacity.Multi,
                    ESGraphPortAggregation.Ordered, out string updateError), Is.True, updateError);
                Assert.That(ESGraphSnapshotBaker.TryBake(graph, out ESBakedGraphSnapshot ordered,
                    out _), Is.True);
                Assert.That(ordered.Routes.All(route => route.TargetAggregation == ESGraphPortAggregation.Ordered),
                    Is.True);
                Assert.That(ordered.ContentSignature, Is.Not.EqualTo(originalSignature));
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void StableGraph_EndpointMeaningKeepsSameTypePortsIndependentAcrossBake()
        {
            ESTestGraphAsset graph = ScriptableObject.CreateInstance<ESTestGraphAsset>();
            try
            {
                ESGraphNodeRecord source = graph.AddNode("test.split", "Split", Vector2.zero,
                    new[]
                    {
                        new ESGraphPortDefinition("成功", "result.success",
                            ESGraphPortDirection.Output, ESGraphPortCapacity.Single,
                            ESGraphPortValueIds.Text, meaning: "成功时产生的文本"),
                        new ESGraphPortDefinition("失败", "result.failure",
                            ESGraphPortDirection.Output, ESGraphPortCapacity.Single,
                            ESGraphPortValueIds.Text, meaning: "失败时产生的文本")
                    });
                ESGraphNodeRecord target = graph.AddNode("test.collect", "Collect", Vector2.right,
                    new[]
                    {
                        new ESGraphPortDefinition("成功输入", "input.success",
                            ESGraphPortDirection.Input, ESGraphPortCapacity.Single,
                            ESGraphPortValueIds.Text, meaning: "接收成功文本"),
                        new ESGraphPortDefinition("失败输入", "input.failure",
                            ESGraphPortDirection.Input, ESGraphPortCapacity.Single,
                            ESGraphPortValueIds.Text, meaning: "接收失败文本")
                    });
                Assert.That(graph.TryAddEdge(source.ports[0].portId, target.ports[0].portId,
                    out ESGraphEdgeRecord successEdge, out string successError), Is.True, successError);
                Assert.That(graph.TryAddEdge(source.ports[1].portId, target.ports[1].portId,
                    out ESGraphEdgeRecord failureEdge, out string failureError), Is.True, failureError);

                Assert.That(ESGraphSnapshotBaker.TryBake(graph, out ESBakedGraphSnapshot snapshot,
                    out List<ESGraphValidationIssue> issues), Is.True, Describe(issues));
                var successEndpoint = new ESGraphEndpointKey(source.nodeId, "result.success");
                var failureEndpoint = new ESGraphEndpointKey(source.nodeId, "result.failure");
                Assert.That(snapshot.TryGetPort(successEndpoint, out ESGraphPortSnapshot successPort), Is.True);
                Assert.That(snapshot.TryGetPort(failureEndpoint, out ESGraphPortSnapshot failurePort), Is.True);
                Assert.That(successPort.NodeId, Is.EqualTo(source.nodeId));
                Assert.That(successPort.Meaning, Is.EqualTo("成功时产生的文本"));
                Assert.That(failurePort.Meaning, Is.EqualTo("失败时产生的文本"));
                Assert.That(snapshot.GetOutgoingRoutes(successEndpoint).Single().EdgeId,
                    Is.EqualTo(successEdge.edgeId));
                Assert.That(snapshot.GetOutgoingRoutes(failureEndpoint).Single().EdgeId,
                    Is.EqualTo(failureEdge.edgeId));
                Assert.That(snapshot.GetOutgoingRoutes(successEndpoint).Single().TargetEndpoint,
                    Is.EqualTo(new ESGraphEndpointKey(target.nodeId, "input.success")));
                Assert.That(snapshot.GetOutgoingRoutes(failureEndpoint).Single().TargetEndpoint,
                    Is.EqualTo(new ESGraphEndpointKey(target.nodeId, "input.failure")));

                string originalSignature = snapshot.ContentSignature;
                ESGraphPortRecord success = source.ports[0];
                Assert.That(graph.UpdatePort(success.portId, success.stableKey, success.name,
                    "成功时产生的最终文本", success.valueTypeId, success.direction,
                    success.capacity, success.aggregation, out string updateError), Is.True, updateError);
                Assert.That(ESGraphSnapshotBaker.TryBake(graph, out ESBakedGraphSnapshot updated,
                    out _), Is.True);
                Assert.That(updated.ContentSignature, Is.Not.EqualTo(originalSignature));
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void StableGraph_SchemaUpgradeAddsMeaningWithoutChangingStableIdentities()
        {
            ESTestGraphAsset graph = ScriptableObject.CreateInstance<ESTestGraphAsset>();
            try
            {
                ESGraphNodeRecord node = graph.AddNode("test.node", "Node", Vector2.zero,
                    DefaultPorts);
                string graphId = graph.GraphId;
                string nodeId = node.nodeId;
                string[] portIds = node.ports.Select(port => port.portId).ToArray();
                graph.schemaVersion = 2;
                foreach (ESGraphPortRecord port in node.ports)
                    port.meaning = string.Empty;

                List<ESGraphValidationIssue> before = graph.ValidateGraph();
                Assert.That(before.Any(issue => issue.code == "Graph.Schema.MigrationRequired"), Is.True);
                Assert.That(before.Any(issue => issue.code == "Graph.Port.Meaning"), Is.True);
                Assert.That(graph.TryUpgradeSchema(out bool changed, out string error), Is.True, error);
                Assert.That(changed, Is.True);
                Assert.That(graph.schemaVersion, Is.EqualTo(ESGraphAssetBase.CurrentSchemaVersion));
                Assert.That(graph.GraphId, Is.EqualTo(graphId));
                Assert.That(node.nodeId, Is.EqualTo(nodeId));
                Assert.That(node.ports.Select(port => port.portId), Is.EqualTo(portIds));
                Assert.That(node.ports.All(port => port.meaning == port.name), Is.True);
                Assert.That(graph.TryUpgradeSchema(out bool changedAgain, out string secondError),
                    Is.True, secondError);
                Assert.That(changedAgain, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void StableGraph_SchemaUpgradeFailureDoesNotPartiallyMutateMeaningsOrVersion()
        {
            ESTestGraphAsset graph = ScriptableObject.CreateInstance<ESTestGraphAsset>();
            try
            {
                ESGraphNodeRecord valid = graph.AddNode("test.valid", "Valid", Vector2.zero,
                    DefaultPorts);
                ESGraphNodeRecord invalid = graph.AddNode("test.invalid", "Invalid", Vector2.right,
                    DefaultPorts);
                graph.schemaVersion = 2;
                valid.ports[0].meaning = string.Empty;
                invalid.ports[0].meaning = string.Empty;
                invalid.ports[0].name = string.Empty;
                invalid.ports[0].stableKey = string.Empty;

                Assert.That(graph.TryUpgradeSchema(out bool changed, out string error), Is.False);
                Assert.That(changed, Is.False);
                Assert.That(error, Does.Contain("用途"));
                Assert.That(graph.schemaVersion, Is.EqualTo(2));
                Assert.That(valid.ports[0].meaning, Is.Empty);
                Assert.That(invalid.ports[0].meaning, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void StableGraph_SchemaUpgradeAssignsDeterministicEdgeOrderWithoutIdentityDrift()
        {
            ESTestGraphAsset graph = ScriptableObject.CreateInstance<ESTestGraphAsset>();
            try
            {
                ESGraphNodeRecord source = graph.AddNode("test.source", "Source", Vector2.zero,
                    new[]
                    {
                        new ESGraphPortDefinition("输出", "flow.out", ESGraphPortDirection.Output,
                            ESGraphPortCapacity.Multi, ESGraphPortValueIds.Flow)
                    });
                ESGraphNodeRecord first = graph.AddNode("test.target", "First", Vector2.right,
                    new[] { new ESGraphPortDefinition("输入", "flow.in", ESGraphPortDirection.Input) });
                ESGraphNodeRecord second = graph.AddNode("test.target", "Second", Vector2.right * 2f,
                    new[] { new ESGraphPortDefinition("输入", "flow.in", ESGraphPortDirection.Input) });
                Assert.That(graph.TryAddEdge(source.ports[0].portId, first.ports[0].portId,
                    out ESGraphEdgeRecord firstEdge, out string firstError), Is.True, firstError);
                Assert.That(graph.TryAddEdge(source.ports[0].portId, second.ports[0].portId,
                    out ESGraphEdgeRecord secondEdge, out string secondError), Is.True, secondError);
                string[] identities = graph.Edges.Select(edge => edge.edgeId).ToArray();
                string[] endpoints = graph.Edges.Select(edge => edge.outputPortId + ">" + edge.inputPortId)
                    .ToArray();
                graph.schemaVersion = 3;
                firstEdge.order = 99;
                secondEdge.order = 99;

                Assert.That(graph.TryUpgradeSchema(out bool changed, out string error), Is.True, error);
                Assert.That(changed, Is.True);
                Assert.That(graph.schemaVersion, Is.EqualTo(ESGraphAssetBase.CurrentSchemaVersion));
                Assert.That(graph.Edges.Select(edge => edge.edgeId), Is.EqualTo(identities));
                Assert.That(graph.Edges.Select(edge => edge.outputPortId + ">" + edge.inputPortId),
                    Is.EqualTo(endpoints));
                ESGraphEdgeRecord[] byIdentity = graph.Edges.OrderBy(edge => edge.edgeId,
                    StringComparer.Ordinal).ToArray();
                Assert.That(byIdentity.Select(edge => edge.order), Is.EqualTo(new[] { 0, 1 }));
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void StableGraph_EdgeOrderMovesWithinOneGroupAndChangesSnapshotSignature()
        {
            ESTestGraphAsset graph = ScriptableObject.CreateInstance<ESTestGraphAsset>();
            try
            {
                ESGraphNodeRecord source = graph.AddNode("test.source", "Source", Vector2.zero,
                    new[]
                    {
                        new ESGraphPortDefinition("输出", "flow.out", ESGraphPortDirection.Output,
                            ESGraphPortCapacity.Multi, ESGraphPortValueIds.Flow)
                    });
                var targets = new List<ESGraphNodeRecord>();
                var created = new List<ESGraphEdgeRecord>();
                for (int i = 0; i < 3; i++)
                {
                    ESGraphNodeRecord target = graph.AddNode("test.target", "Target " + i,
                        Vector2.right * (i + 1),
                        new[] { new ESGraphPortDefinition("输入", "flow.in", ESGraphPortDirection.Input) });
                    targets.Add(target);
                    Assert.That(graph.TryAddEdge(source.ports[0].portId, target.ports[0].portId,
                        out ESGraphEdgeRecord edge, out string addError), Is.True, addError);
                    created.Add(edge);
                }
                Assert.That(ESGraphSnapshotBaker.TryBake(graph, out ESBakedGraphSnapshot before,
                    out List<ESGraphValidationIssue> beforeIssues), Is.True, Describe(beforeIssues));

                Assert.That(graph.TryMoveEdge(created[2].edgeId, -1, out string moveError),
                    Is.True, moveError);
                Assert.That(graph.TryGetEdgeOrderPosition(created[2].edgeId,
                    out int position, out int count), Is.True);
                Assert.That(position, Is.EqualTo(1));
                Assert.That(count, Is.EqualTo(3));
                Assert.That(created[2].edgeId, Is.Not.Empty);
                Assert.That(created[2].outputPortId, Is.EqualTo(source.ports[0].portId));
                Assert.That(created[2].inputPortId, Is.EqualTo(targets[2].ports[0].portId));
                Assert.That(ESGraphSnapshotBaker.TryBake(graph, out ESBakedGraphSnapshot after,
                    out List<ESGraphValidationIssue> afterIssues), Is.True, Describe(afterIssues));
                Assert.That(after.ContentSignature, Is.Not.EqualTo(before.ContentSignature));
                Assert.That(after.GetOutgoingRoutes(source.nodeId, "flow.out")
                    .Select(route => route.TargetNodeId),
                    Is.EqualTo(new[] { targets[0].nodeId, targets[2].nodeId, targets[1].nodeId }));
                Assert.That(after.GetOutgoingRoutes(source.nodeId, "flow.out")
                    .Select(route => route.Order), Is.EqualTo(new[] { 0, 1, 2 }));
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void StableGraph_MoveEdgeServiceIsAtomicAndUndoRedoKeepsIdentityAndEndpoints()
        {
            ESTestGraphAsset graph = ScriptableObject.CreateInstance<ESTestGraphAsset>();
            try
            {
                Undo.ClearAll();
                ESGraphNodeRecord source = graph.AddNode("test.source", "Source", Vector2.zero,
                    new[]
                    {
                        new ESGraphPortDefinition("输出", "flow.out", ESGraphPortDirection.Output,
                            ESGraphPortCapacity.Multi, ESGraphPortValueIds.Flow)
                    });
                var edges = new List<ESGraphEdgeRecord>();
                for (int i = 0; i < 3; i++)
                {
                    ESGraphNodeRecord target = graph.AddNode("test.target", "Target " + i,
                        Vector2.right * (i + 1),
                        new[] { new ESGraphPortDefinition("输入", "flow.in", ESGraphPortDirection.Input) });
                    Assert.That(graph.TryAddEdge(source.ports[0].portId, target.ports[0].portId,
                        out ESGraphEdgeRecord edge, out string addError), Is.True, addError);
                    edges.Add(edge);
                }

                string movedEdgeId = edges[2].edgeId;
                string movedOutputPortId = edges[2].outputPortId;
                string movedInputPortId = edges[2].inputPortId;
                int originalOrder = edges[2].order;
                int swappedOrder = edges[1].order;
                int dirtyCount = 0;
                int saveCount = 0;
                int notifyCount = 0;
                var service = new ESGraphEditService(
                    _ => dirtyCount++, () => saveCount++, _ => notifyCount++);

                ESGraphEditResult rejected = service.MoveEdge(graph, edges[0].edgeId, -1);
                Assert.That(rejected.changed, Is.False);
                Assert.That(rejected.error, Is.Not.Empty);
                Assert.That(dirtyCount + saveCount + notifyCount, Is.Zero);

                ESGraphEditResult moved = service.MoveEdge(graph, movedEdgeId, -1);
                Undo.FlushUndoRecordObjects();
                Assert.That(moved.changed, Is.True, moved.error);
                Assert.That(dirtyCount, Is.EqualTo(1));
                Assert.That(saveCount, Is.EqualTo(1));
                Assert.That(notifyCount, Is.EqualTo(1));
                Assert.That(graph.FindEdge(movedEdgeId).order, Is.EqualTo(swappedOrder));
                Assert.That(graph.FindEdge(movedEdgeId).outputPortId, Is.EqualTo(movedOutputPortId));
                Assert.That(graph.FindEdge(movedEdgeId).inputPortId, Is.EqualTo(movedInputPortId));

                Undo.PerformUndo();
                Assert.That(graph.FindEdge(movedEdgeId).edgeId, Is.EqualTo(movedEdgeId));
                Assert.That(graph.FindEdge(movedEdgeId).order, Is.EqualTo(originalOrder));
                Assert.That(graph.FindEdge(movedEdgeId).outputPortId, Is.EqualTo(movedOutputPortId));
                Assert.That(graph.FindEdge(movedEdgeId).inputPortId, Is.EqualTo(movedInputPortId));

                Undo.PerformRedo();
                Assert.That(graph.FindEdge(movedEdgeId).edgeId, Is.EqualTo(movedEdgeId));
                Assert.That(graph.FindEdge(movedEdgeId).order, Is.EqualTo(swappedOrder));
                Assert.That(graph.FindEdge(movedEdgeId).outputPortId, Is.EqualTo(movedOutputPortId));
                Assert.That(graph.FindEdge(movedEdgeId).inputPortId, Is.EqualTo(movedInputPortId));
            }
            finally
            {
                Undo.ClearAll();
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void StableGraph_SignatureIgnoresAuthoringOrderAndPositionButTracksPayload()
        {
            ESTestGraphAsset graph = ScriptableObject.CreateInstance<ESTestGraphAsset>();
            try
            {
                ESGraphNodeRecord first = graph.AddNode("test.node", "A", Vector2.zero, DefaultPorts);
                ESGraphNodeRecord second = graph.AddNode("test.node", "B", Vector2.right, DefaultPorts);
                ESGraphNodeRecord third = graph.AddNode("test.node", "C", Vector2.right * 2f, DefaultPorts);
                Assert.That(graph.TryAddEdge(Output(first), Input(second), out _, out string firstError), Is.True, firstError);
                Assert.That(graph.TryAddEdge(Output(second), Input(third), out _, out string secondError), Is.True, secondError);
                Assert.That(ESGraphSnapshotBaker.TryBake(graph, out ESBakedGraphSnapshot original, out _), Is.True);

                graph.SetNodePosition(first.nodeId, new Vector2(900f, -300f));
                ReorderSerializedGraph(graph);
                Assert.That(ESGraphSnapshotBaker.TryBake(graph, out ESBakedGraphSnapshot reordered, out _), Is.True);
                Assert.That(reordered.ContentSignature, Is.EqualTo(original.ContentSignature));

                Assert.That(graph.UpdateNode(first.nodeId, first.typeId, first.version, first.title, "{\"changed\":true}",
                    out string error), Is.True, error);
                Assert.That(ESGraphSnapshotBaker.TryBake(graph, out ESBakedGraphSnapshot changed, out _), Is.True);
                Assert.That(changed.ContentSignature, Is.Not.EqualTo(original.ContentSignature));
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void StableGraph_InvalidGraphCannotBakeSnapshot()
        {
            ESTestGraphAsset graph = ScriptableObject.CreateInstance<ESTestGraphAsset>();
            try
            {
                ESGraphNodeRecord node = graph.AddNode("test.node", "A", Vector2.zero, DefaultPorts);
                node.version = 0;
                Assert.That(ESGraphSnapshotBaker.TryBake(graph, out ESBakedGraphSnapshot snapshot,
                    out List<ESGraphValidationIssue> issues), Is.False);
                Assert.That(snapshot, Is.Null);
                Assert.That(issues, Is.Not.Empty);
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void StableGraph_TestFixtureDomainInitializationRejectsInvalidOrLateValues()
        {
            ESTestGraphAsset graph = ScriptableObject.CreateInstance<ESTestGraphAsset>();
            try
            {
                Assert.That(graph.DomainId, Is.EqualTo(ESGraphDomainIds.Generic));
                Assert.That(graph.InitializeTestDomain(ESGraphDomainIds.Story, out string error), Is.True, error);
                Assert.That(graph.DomainId, Is.EqualTo(ESGraphDomainIds.Story));
                Assert.That(graph.schemaVersion, Is.EqualTo(ESGraphAssetBase.CurrentSchemaVersion));
                Assert.That(graph.InitializeTestDomain("Invalid Domain", out _), Is.False);

                graph.AddNode("es.story.start", "Start", Vector2.zero,
                    new[] { new ESGraphPortDefinition("输出", "flow.output", ESGraphPortDirection.Output) });
                Assert.That(graph.InitializeTestDomain(ESGraphDomainIds.BehaviorTree, out _), Is.False);
                Assert.That(graph.DomainId, Is.EqualTo(ESGraphDomainIds.Story));
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void StableGraph_StrongKeysPreserveSerializedStableIdentity()
        {
            ESTestGraphAsset graph = ScriptableObject.CreateInstance<ESTestGraphAsset>();
            try
            {
                ESGraphDomainKey domain = ESGraphDomainKey.FromKind(ESGraphDomainKind.Story);
                ESGraphNodeTypeKey nodeType = ESGraphNodeTypeKey.FromKind(ESGraphBuiltInNodeKind.StoryCondition);
                Assert.That(graph.InitializeTestDomain(domain, out string error), Is.True, error);
                ESGraphNodeRecord node = graph.AddNode(nodeType, "条件", Vector2.zero, new[]
                {
                    new ESGraphPortDefinition("是否成立", "condition.result", ESGraphPortDirection.Output,
                        ESGraphPortCapacity.Single, ESGraphPortValueKind.Boolean)
                });

                Assert.That(graph.DomainKind, Is.EqualTo(ESGraphDomainKind.Story));
                Assert.That(graph.DomainId, Is.EqualTo(ESGraphDomainIds.Story));
                Assert.That(node.BuiltInKind, Is.EqualTo(ESGraphBuiltInNodeKind.StoryCondition));
                Assert.That(node.typeId, Is.EqualTo(ESGraphNodeTypeIds.StoryCondition));
                Assert.That(node.ports[0].ValueKind, Is.EqualTo(ESGraphPortValueKind.Boolean));
                Assert.That(node.ports[0].valueTypeId, Is.EqualTo(ESGraphPortValueIds.Boolean));
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void StableGraph_CustomKeysKeepNodeExtensionsOpen()
        {
            ESGraphDomainKey domain = ESGraphDomainKey.Custom("es.tests.custom-graph");
            ESGraphNodeTypeKey nodeType = ESGraphNodeTypeKey.Custom("es.tests.custom-node");

            Assert.That(domain.Kind, Is.EqualTo(ESGraphDomainKind.Custom));
            Assert.That(domain.StableId, Is.EqualTo("es.tests.custom-graph"));
            Assert.That(nodeType.Kind, Is.EqualTo(ESGraphBuiltInNodeKind.Custom));
            Assert.That(nodeType.StableId, Is.EqualTo("es.tests.custom-node"));
        }

        [Test]
        public void StableGraph_BakedSnapshotCarriesDomainAndSignatureIncludesDomain()
        {
            ESTestGraphAsset graph = ScriptableObject.CreateInstance<ESTestGraphAsset>();
            ESTestGraphAsset behaviorGraph = ScriptableObject.CreateInstance<ESTestGraphAsset>();
            try
            {
                Assert.That(graph.InitializeTestDomain(ESGraphDomainIds.Story, out string storyError), Is.True, storyError);
                Assert.That(ESGraphSnapshotBaker.TryBake(graph, out ESBakedGraphSnapshot story, out _), Is.True);
                Assert.That(story.DomainId, Is.EqualTo(ESGraphDomainIds.Story));

                Assert.That(behaviorGraph.InitializeTestDomain(ESGraphDomainIds.BehaviorTree,
                    out string behaviorError), Is.True, behaviorError);
                Assert.That(ESGraphSnapshotBaker.TryBake(behaviorGraph,
                    out ESBakedGraphSnapshot behavior, out _), Is.True);
                Assert.That(behavior.DomainId, Is.EqualTo(ESGraphDomainIds.BehaviorTree));
                Assert.That(behavior.ContentSignature, Is.Not.EqualTo(story.ContentSignature));
            }
            finally
            {
                Object.DestroyImmediate(graph);
                Object.DestroyImmediate(behaviorGraph);
            }
        }

        [Test]
        public void StableGraph_PlanBakeGuardRejectsCrossDomainSnapshot()
        {
            ESTestGraphAsset graph = ScriptableObject.CreateInstance<ESTestGraphAsset>();
            try
            {
                Assert.That(graph.InitializeTestDomain(ESGraphDomainIds.Story, out string error), Is.True, error);
                Assert.That(ESGraphSnapshotBaker.TryBake(graph, out ESBakedGraphSnapshot snapshot, out _), Is.True);
                Assert.That(ESGraphPlanBakeGuard.TryValidateSource(snapshot,
                    ESGraphDomainKey.FromKind(ESGraphDomainKind.Story), out _), Is.True);
                Assert.That(ESGraphPlanBakeGuard.TryValidateSource(snapshot,
                    ESGraphDomainKey.FromKind(ESGraphDomainKind.BehaviorTree),
                    out IReadOnlyList<ESGraphValidationIssue> issues), Is.False);
                Assert.That(issues, Has.Count.EqualTo(1));
                Assert.That(issues[0].code, Is.EqualTo("Graph.Plan.DomainMismatch"));
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }

        private static ESGraphNodeRecord CreateCardNode(string payloadJson, string nodeTypeId = null)
        {
            return new ESGraphNodeRecord
            {
                nodeId = "node-card-test",
                typeId = nodeTypeId
                    ?? ESAgentGraphStableIds.GoalNode,
                version = 1,
                title = "Card Test",
                payloadJson = payloadJson ?? string.Empty,
                ports = new List<ESGraphPortRecord>
                {
                    new ESGraphPortRecord
                    {
                        portId = "port-card-test",
                        stableKey = "flow.output",
                        name = "输出",
                        valueTypeId = ESGraphPortValueIds.Flow,
                        direction = ESGraphPortDirection.Output,
                        capacity = ESGraphPortCapacity.Multi
                    }
                }
            };
        }

        private static ESGraphNodeCardContext CreateCardContext(ESGraphNodeRecord node,
            System.Action<string> commitPayload, string[] incomingNodeIds = null,
            string[] outgoingNodeIds = null, bool isReadOnly = false, bool hasFutureSchema = false,
            System.Action openDetails = null, System.Action<string> focusNode = null,
            System.Action<string> selectNode = null, System.Action<string> report = null,
            System.Action<string> copyText = null,
            System.Func<ESGraphNodeCardActionKey, bool> canExecuteAction = null,
            System.Action<ESGraphNodeCardActionKey> executeAction = null)
        {
            var ports = new ESGraphNodeCardPortSummary[node?.ports?.Count ?? 0];
            for (int i = 0; i < ports.Length; i++)
                ports[i] = new ESGraphNodeCardPortSummary(node.ports[i], i + 1);
            return new ESGraphNodeCardContext(
                "graph-card-test",
                ESGraphAssetBase.CurrentSchemaVersion,
                ESAgentGraphStableIds.DomainId,
                node,
                isReadOnly,
                hasFutureSchema,
                ports,
                incomingNodeIds ?? System.Array.Empty<string>(),
                outgoingNodeIds ?? System.Array.Empty<string>(),
                commitPayload,
                openDetails,
                focusNode,
                selectNode,
                report,
                copyText,
                () => false,
                canExecuteAction,
                executeAction);
        }

        private sealed class NoopEdgeConnectorListener : IEdgeConnectorListener
        {
            public void OnDrop(GraphView targetGraphView, Edge edge)
            {
            }

            public void OnDropOutsidePort(Edge edge, Vector2 position)
            {
            }
        }

        private sealed class ESGraphPanelHostWindow : EditorWindow
        {
        }

        private static ESStableGraphEdgeView FindEdgeView(ESStableGraphView view, string edgeId)
        {
            return view.Query<ESStableGraphEdgeView>().ToList().Single(candidate =>
                string.Equals(candidate.userData as string, edgeId, StringComparison.Ordinal));
        }

        private static Port FindPortView(ESStableGraphView view, string portId)
        {
            return view.Query<Port>().ToList().Single(candidate =>
                string.Equals(candidate.userData as string, portId, StringComparison.Ordinal));
        }

        private static void AssertPickedPort(ESStableGraphView view, Port expected, Vector2 position)
        {
            VisualElement picked = view.panel.Pick(position);
            Port actual = picked as Port ?? picked?.GetFirstAncestorOfType<Port>();
            Assert.That(actual, Is.SameAs(expected), "Panel.Pick 必须命中真实目标端口。 ");
        }

        private static Vector2 FindBlankPanelPosition(ESStableGraphView view)
        {
            Rect bounds = view.worldBound;
            Vector2[] candidates =
            {
                new Vector2(bounds.xMax - 24f, bounds.yMax - 24f),
                new Vector2(bounds.xMin + 24f, bounds.yMax - 24f),
                new Vector2(bounds.center.x, bounds.yMax - 24f),
                new Vector2(bounds.center.x, bounds.center.y)
            };
            for (int i = 0; i < candidates.Length; i++)
            {
                VisualElement picked = view.panel.Pick(candidates[i]);
                if (!(picked is Port) && picked?.GetFirstAncestorOfType<Port>() == null)
                    return candidates[i];
            }
            Assert.Fail("无法在测试 GraphView 中找到空白落点。 ");
            return bounds.center;
        }

        private static void SendMouseDown(VisualElement target, Vector2 panelPosition)
        {
            using (MouseDownEvent evt = MouseDownEvent.GetPooled(new Event
                   {
                       type = EventType.MouseDown,
                       button = 0,
                       mousePosition = panelPosition
                   }))
                target.SendEvent(evt);
        }

        private static void SendMouseMove(VisualElement target, Vector2 panelPosition)
        {
            using (MouseMoveEvent evt = MouseMoveEvent.GetPooled(new Event
                   {
                       type = EventType.MouseMove,
                       button = 0,
                       mousePosition = panelPosition
                   }))
                target.SendEvent(evt);
        }

        private static void SendMouseUp(VisualElement target, Vector2 panelPosition)
        {
            using (MouseUpEvent evt = MouseUpEvent.GetPooled(new Event
                   {
                       type = EventType.MouseUp,
                       button = 0,
                       mousePosition = panelPosition
                   }))
                target.SendEvent(evt);
        }

        private static void SendMouseEnter(VisualElement target)
        {
            using (MouseEnterEvent evt = MouseEnterEvent.GetPooled())
                target.SendEvent(evt);
        }

        private static void SendMouseLeave(VisualElement target)
        {
            using (MouseLeaveEvent evt = MouseLeaveEvent.GetPooled())
                target.SendEvent(evt);
        }

        private static void SendKeyDown(VisualElement target, KeyCode keyCode)
        {
            using (KeyDownEvent evt = KeyDownEvent.GetPooled(new Event
                   {
                       type = EventType.KeyDown,
                       keyCode = keyCode
                   }))
                target.SendEvent(evt);
        }

        private static void ReorderSerializedGraph(ESGraphAssetBase graph)
        {
            SerializedObject serialized = new SerializedObject(graph);
            SerializedProperty nodes = serialized.FindProperty("nodes");
            SerializedProperty edges = serialized.FindProperty("edges");
            nodes.MoveArrayElement(0, nodes.arraySize - 1);
            SerializedProperty ports = nodes.GetArrayElementAtIndex(0).FindPropertyRelative("ports");
            if (ports.arraySize > 1)
                ports.MoveArrayElement(0, ports.arraySize - 1);
            if (edges.arraySize > 1)
                edges.MoveArrayElement(0, edges.arraySize - 1);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static string Input(ESGraphNodeRecord node)
        {
            return node.ports.Find(port => port.direction == ESGraphPortDirection.Input).portId;
        }

        private static ESGraphNodeRecord AddDefinedNode(ESGraphAssetBase graph,
            ESGraphBuiltInNodeKind nodeKind, Vector2 position)
        {
            return AddDefinedNode(graph, ESGraphNodeTypeKey.FromKind(nodeKind).StableId, position);
        }

        private static ESGraphNodeRecord AddDefinedNode(ESGraphAssetBase graph,
            string nodeTypeId, Vector2 position)
        {
            ESGraphNodeTypeKey nodeType = ESGraphNodeTypeKey.Parse(nodeTypeId);
            Assert.That(ESGraphAuthoringRegistry.TryGetNodeDefinition(graph.DomainKey, nodeType,
                out IESGraphNodeDefinition definition), Is.True,
                "缺少内置节点定义：" + nodeType.StableId);
            ESGraphNodeRecord node = graph.AddNode(nodeType, definition.DisplayName,
                position, definition.Ports);
            Assert.That(graph.UpdateNode(node.nodeId, nodeType, definition.CurrentVersion,
                node.title, definition.CreateDefaultPayload(), out string updateError),
                Is.True, updateError);
            return node;
        }

        private static bool IsError(ESGraphValidationIssue issue)
        {
            return issue != null && issue.severity == ESGraphValidationSeverity.Error;
        }

        private static string Describe(IEnumerable<ESGraphValidationIssue> issues)
        {
            return string.Join("; ", (issues ?? Array.Empty<ESGraphValidationIssue>())
                .Where(issue => issue != null)
                .Select(issue => issue.code + ": " + issue.message));
        }

        private static void AssertReconnectRejectedWithoutMutation(ESGraphAssetBase graph, string edgeId,
            string firstPortId, string secondPortId, string scenario)
        {
            ESGraphEdgeRecord before = graph.FindEdge(edgeId);
            Assert.That(before, Is.Not.Null, scenario);
            string originalOutput = before.outputPortId;
            string originalInput = before.inputPortId;
            Assert.That(graph.TryReconnectEdge(edgeId, firstPortId, secondPortId, out string error),
                Is.False, scenario);
            Assert.That(error, Is.Not.Empty, scenario);
            ESGraphEdgeRecord after = graph.FindEdge(edgeId);
            Assert.That(after, Is.Not.Null, scenario);
            Assert.That(after.edgeId, Is.EqualTo(edgeId), scenario);
            Assert.That(after.outputPortId, Is.EqualTo(originalOutput), scenario);
            Assert.That(after.inputPortId, Is.EqualTo(originalInput), scenario);
        }

        private static string Output(ESGraphNodeRecord node)
        {
            return node.ports.Find(port => port.direction == ESGraphPortDirection.Output).portId;
        }
    }
}
#endif
