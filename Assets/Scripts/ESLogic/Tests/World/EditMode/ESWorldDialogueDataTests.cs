using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace ES.Tests
{
    public sealed class ESWorldDialogueDataTests
    {
        [Test]
        public void EnsureStableIds_FillsMissingIdentity_AndBuildsEntry()
        {
            var definition = new ESWorldDialogueGraphDefinition
            {
                nodes = new List<ESWorldDialogueNodeData>
                {
                    new ESWorldDialogueNodeData
                    {
                        outputs = new List<ESWorldDialoguePortData> { new ESWorldDialoguePortData() }
                    }
                }
            };

            Assert.That(definition.EnsureStableIds(), Is.True);
            Assert.That(definition.graphId, Is.Not.Empty);
            Assert.That(definition.nodes[0].nodeId, Is.Not.Empty);
            Assert.That(definition.nodes[0].outputs[0].portId, Is.Not.Empty);
            Assert.That(definition.entryNodeId, Is.EqualTo(definition.nodes[0].nodeId));
            Assert.That(definition.IsValid(out string error), Is.True, error);
        }

        [Test]
        public void DuplicateNodeId_IsRejected_WithoutSilentRekey()
        {
            const string duplicate = "node.duplicate";
            var definition = new ESWorldDialogueGraphDefinition
            {
                graphId = "graph.test",
                entryNodeId = duplicate,
                nodes = new List<ESWorldDialogueNodeData>
                {
                    new ESWorldDialogueNodeData { nodeId = duplicate },
                    new ESWorldDialogueNodeData { nodeId = duplicate }
                }
            };

            definition.EnsureStableIds();

            Assert.That(definition.nodes[0].nodeId, Is.EqualTo(duplicate));
            Assert.That(definition.nodes[1].nodeId, Is.EqualTo(duplicate));
            Assert.That(definition.IsValid(out string error), Is.False);
            StringAssert.Contains("节点 ID", error);
        }

        [Test]
        public void EdgeWithMissingTarget_IsRejected()
        {
            var definition = CreateConnectedGraph();
            definition.edges[0].toNodeId = "missing";

            Assert.That(definition.IsValid(out string error), Is.False);
            StringAssert.Contains("不存在的节点", error);
        }

        [Test]
        public void MapDialoguePlacement_RejectsDuplicateAndOutOfBounds2DEntry()
        {
            var definition = new ESWorldMapDefinition
            {
                mapId = "map.dialogue.test",
                contentHash = "draft",
                generatorKey = "test.generator",
                worldMin = Vector2.zero,
                worldMax = new Vector2(100f, 100f)
            };
            definition.heightfield.EnsureSamples();
            definition.dialoguePlacements.Add(new ESWorldDialoguePlacement
            {
                placementId = "placement.1",
                dialogueGraphKey = "graph.test",
                space = ESWorldDialoguePlacementSpace.Map2D,
                position = new Vector3(120f, 0f, 50f)
            });

            Assert.That(definition.IsValid(out string outOfBounds), Is.False);
            StringAssert.Contains("超出地图范围", outOfBounds);

            definition.dialoguePlacements[0].position = new Vector3(50f, 0f, 50f);
            definition.dialoguePlacements.Add(new ESWorldDialoguePlacement
            {
                placementId = "placement.1",
                dialogueGraphKey = "graph.test",
                space = ESWorldDialoguePlacementSpace.Scene3D
            });
            Assert.That(definition.IsValid(out string duplicate), Is.False);
            StringAssert.Contains("为空或重复", duplicate);
        }

        private static ESWorldDialogueGraphDefinition CreateConnectedGraph()
        {
            var definition = new ESWorldDialogueGraphDefinition
            {
                graphId = "graph.connected"
            };
            var first = new ESWorldDialogueNodeData
            {
                nodeId = "node.first",
                outputs = new List<ESWorldDialoguePortData>
                {
                    new ESWorldDialoguePortData { portId = "port.next", displayName = "继续" }
                }
            };
            var second = new ESWorldDialogueNodeData { nodeId = "node.second" };
            definition.nodes.Add(first);
            definition.nodes.Add(second);
            definition.entryNodeId = first.nodeId;
            definition.edges.Add(new ESWorldDialogueEdgeData
            {
                edgeId = "edge.first.second",
                fromNodeId = first.nodeId,
                fromPortId = first.outputs[0].portId,
                toNodeId = second.nodeId
            });
            return definition;
        }
    }
}
