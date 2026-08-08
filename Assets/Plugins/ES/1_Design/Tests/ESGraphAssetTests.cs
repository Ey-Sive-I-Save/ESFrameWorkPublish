#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ES.Tests
{
    public sealed class ESGraphAssetTests
    {
        private static readonly ESGraphPortDefinition[] DefaultPorts =
        {
            new ESGraphPortDefinition("输入", "flow.input", ESGraphPortDirection.Input, ESGraphPortCapacity.Single),
            new ESGraphPortDefinition("输出", "flow.output", ESGraphPortDirection.Output, ESGraphPortCapacity.Multi)
        };

        [Test]
        public void StableGraph_PersistsGraphIdentityAndNodePosition_WhileIndependentCopyGetsNewIdentity()
        {
            ESGraphAsset source = ScriptableObject.CreateInstance<ESGraphAsset>();
            ESGraphAsset restored = ScriptableObject.CreateInstance<ESGraphAsset>();
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
            ESGraphAsset graph = ScriptableObject.CreateInstance<ESGraphAsset>();
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
            ESGraphAsset graph = ScriptableObject.CreateInstance<ESGraphAsset>();
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
        public void StableGraph_DuplicateNodesCopiesOnlyInternalEdgesWithFreshIdentity()
        {
            ESGraphAsset graph = ScriptableObject.CreateInstance<ESGraphAsset>();
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
        public void StableGraph_RemoveNodeAlsoRemovesIncidentEdges()
        {
            ESGraphAsset graph = ScriptableObject.CreateInstance<ESGraphAsset>();
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
        public void StableGraph_UndoRestoresAtomicAssetMutation()
        {
            ESGraphAsset graph = ScriptableObject.CreateInstance<ESGraphAsset>();
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
            ESGraphAsset graph = ScriptableObject.CreateInstance<ESGraphAsset>();
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
            ESGraphAsset graph = ScriptableObject.CreateInstance<ESGraphAsset>();
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
            ESGraphAsset graph = ScriptableObject.CreateInstance<ESGraphAsset>();
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
            ESGraphAsset graph = ScriptableObject.CreateInstance<ESGraphAsset>();
            try
            {
                ESGraphNodeRecord source = graph.AddNode("test.source", "Source", Vector2.zero,
                    new[] { new ESGraphPortDefinition("输出", "flow.output", ESGraphPortDirection.Output, ESGraphPortCapacity.Multi) });
                ESGraphNodeRecord firstSink = graph.AddNode("test.sink", "A", Vector2.right, DefaultPorts);
                ESGraphNodeRecord secondSink = graph.AddNode("test.sink", "B", Vector2.right * 2f, DefaultPorts);
                string output = Output(source);
                Assert.That(graph.TryAddEdge(output, Input(firstSink), out _, out string firstError), Is.True, firstError);
                Assert.That(graph.TryAddEdge(output, Input(secondSink), out _, out string secondError), Is.True, secondError);

                Assert.That(graph.UpdatePort(output, "flow.output", "输出", "flow", ESGraphPortDirection.Input,
                    ESGraphPortCapacity.Multi, out _), Is.False);
                Assert.That(graph.UpdatePort(output, "flow.output", "输出", "number", ESGraphPortDirection.Output,
                    ESGraphPortCapacity.Multi, out _), Is.False);
                Assert.That(graph.UpdatePort(output, "flow.output", "输出", "flow", ESGraphPortDirection.Output,
                    ESGraphPortCapacity.Single, out _), Is.False);
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
        public void StableGraph_SnapshotIsDetachedAndSupportsStableIdLookup()
        {
            ESGraphAsset graph = ScriptableObject.CreateInstance<ESGraphAsset>();
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
        public void StableGraph_SignatureIgnoresAuthoringOrderAndPositionButTracksPayload()
        {
            ESGraphAsset graph = ScriptableObject.CreateInstance<ESGraphAsset>();
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
            ESGraphAsset graph = ScriptableObject.CreateInstance<ESGraphAsset>();
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
        public void StableGraph_DomainIdentityIsValidatedAndCannotChangeAfterAuthoringStarts()
        {
            ESGraphAsset graph = ScriptableObject.CreateInstance<ESGraphAsset>();
            try
            {
                Assert.That(graph.DomainId, Is.EqualTo(ESGraphDomainIds.Generic));
                Assert.That(graph.TrySetDomainId(ESGraphDomainIds.Story, out string error), Is.True, error);
                Assert.That(graph.DomainId, Is.EqualTo(ESGraphDomainIds.Story));
                Assert.That(graph.schemaVersion, Is.EqualTo(ESGraphAsset.CurrentSchemaVersion));
                Assert.That(graph.TrySetDomainId("Invalid Domain", out _), Is.False);

                graph.AddNode("es.story.start", "Start", Vector2.zero,
                    new[] { new ESGraphPortDefinition("输出", "flow.output", ESGraphPortDirection.Output) });
                Assert.That(graph.TrySetDomainId(ESGraphDomainIds.BehaviorTree, out _), Is.False);
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
            ESGraphAsset graph = ScriptableObject.CreateInstance<ESGraphAsset>();
            try
            {
                ESGraphDomainKey domain = ESGraphDomainKey.FromKind(ESGraphDomainKind.Story);
                ESGraphNodeTypeKey nodeType = ESGraphNodeTypeKey.FromKind(ESGraphBuiltInNodeKind.StoryCondition);
                Assert.That(graph.TrySetDomain(domain, out string error), Is.True, error);
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
        public void StableGraph_BakedSnapshotCarriesDomainAndDomainChangesSignature()
        {
            ESGraphAsset graph = ScriptableObject.CreateInstance<ESGraphAsset>();
            try
            {
                Assert.That(graph.TrySetDomainId(ESGraphDomainIds.Story, out string storyError), Is.True, storyError);
                Assert.That(ESGraphSnapshotBaker.TryBake(graph, out ESBakedGraphSnapshot story, out _), Is.True);
                Assert.That(story.DomainId, Is.EqualTo(ESGraphDomainIds.Story));

                Assert.That(graph.TrySetDomainId(ESGraphDomainIds.BehaviorTree, out string behaviorError), Is.True, behaviorError);
                Assert.That(ESGraphSnapshotBaker.TryBake(graph, out ESBakedGraphSnapshot behavior, out _), Is.True);
                Assert.That(behavior.DomainId, Is.EqualTo(ESGraphDomainIds.BehaviorTree));
                Assert.That(behavior.ContentSignature, Is.Not.EqualTo(story.ContentSignature));
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void StableGraph_PlanBakeGuardRejectsCrossDomainSnapshot()
        {
            ESGraphAsset graph = ScriptableObject.CreateInstance<ESGraphAsset>();
            try
            {
                Assert.That(graph.TrySetDomainId(ESGraphDomainIds.Story, out string error), Is.True, error);
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

        private static void ReorderSerializedGraph(ESGraphAsset graph)
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

        private static string Output(ESGraphNodeRecord node)
        {
            return node.ports.Find(port => port.direction == ESGraphPortDirection.Output).portId;
        }
    }
}
#endif
