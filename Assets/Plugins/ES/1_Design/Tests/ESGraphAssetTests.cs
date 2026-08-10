#if UNITY_EDITOR
using System.Collections.Generic;
using ES.EditorInternal;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

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
        public void StableGraph_PasteNodesRebuildsStableIdentityAndInternalEdges()
        {
            ESGraphAsset source = ScriptableObject.CreateInstance<ESGraphAsset>();
            ESGraphAsset target = ScriptableObject.CreateInstance<ESGraphAsset>();
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
            ESGraphAsset source = ScriptableObject.CreateInstance<ESGraphAsset>();
            ESGraphAsset target = ScriptableObject.CreateInstance<ESGraphAsset>();
            try
            {
                Assert.That(source.TrySetDomainId(ESGraphDomainIds.Story, out string domainError), Is.True,
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
            ESGraphAsset source = ScriptableObject.CreateInstance<ESGraphAsset>();
            ESGraphAsset target = ScriptableObject.CreateInstance<ESGraphAsset>();
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
            ESGraphAsset source = ScriptableObject.CreateInstance<ESGraphAsset>();
            ESGraphAsset target = ScriptableObject.CreateInstance<ESGraphAsset>();
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
            ESGraphAsset source = ScriptableObject.CreateInstance<ESGraphAsset>();
            ESGraphAsset target = ScriptableObject.CreateInstance<ESGraphAsset>();
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
            ESGraphAsset source = ScriptableObject.CreateInstance<ESGraphAsset>();
            ESGraphAsset target = ScriptableObject.CreateInstance<ESGraphAsset>();
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

                ESGraphAsset directionSource = ScriptableObject.CreateInstance<ESGraphAsset>();
                ESGraphAsset directionTarget = ScriptableObject.CreateInstance<ESGraphAsset>();
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
            ESGraphAsset asset = ScriptableObject.CreateInstance<ESGraphAsset>();
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
                ESGraphNodeTypeKey.FromKind(ESGraphBuiltInNodeKind.AgentReference).StableId);
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
                ESGraphNodeTypeKey.FromKind(ESGraphBuiltInNodeKind.AgentAICommandOutput).StableId);
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
                ESGraphNodeTypeKey.FromKind(ESGraphBuiltInNodeKind.AgentSkillOutput).StableId);
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
                ESGraphNodeTypeKey.FromKind(ESGraphBuiltInNodeKind.AgentSkillOutput).StableId);
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
                Assert.That(typeof(ESGraphAsset).IsAssignableFrom(property.PropertyType), Is.False,
                    property.Name + " 不得暴露可变 Graph Asset。");
            foreach (System.Reflection.FieldInfo field in typeof(ESGraphNodeCardContext).GetFields(
                         System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public
                         | System.Reflection.BindingFlags.NonPublic))
                Assert.That(typeof(ESGraphAsset).IsAssignableFrom(field.FieldType), Is.False,
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
            ESGraphAsset graph = ScriptableObject.CreateInstance<ESGraphAsset>();
            try
            {
                Assert.That(graph.TrySetDomain(ESGraphDomainKey.Parse(ESGraphDomainIds.AgentAuthoring),
                        out string domainError),
                    Is.True, domainError);
                ESGraphNodeRecord output = graph.AddNode(
                    ESGraphNodeTypeKey.FromKind(ESGraphBuiltInNodeKind.AgentAICommandOutput),
                    "Command Output", Vector2.zero, DefaultPorts);
                ESGraphNodeRecord goal = graph.AddNode(
                    ESGraphNodeTypeKey.FromKind(ESGraphBuiltInNodeKind.AgentGoal),
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
            ESGraphAsset graph = ScriptableObject.CreateInstance<ESGraphAsset>();
            ESStableGraphView graphView = null;
            try
            {
                Assert.That(graph.TrySetDomainId(ESGraphDomainIds.AgentAuthoring, out string domainError),
                    Is.True, domainError);
                string goalPayload = JsonUtility.ToJson(new ESAgentGoalPayload { title = "Source" });
                ESGraphNodeRecord source = graph.AddNode(
                    ESGraphNodeTypeKey.FromKind(ESGraphBuiltInNodeKind.AgentGoal).StableId,
                    "Source", Vector2.zero, DefaultPorts);
                ESGraphNodeRecord target = graph.AddNode(
                    ESGraphNodeTypeKey.FromKind(ESGraphBuiltInNodeKind.AgentGoal).StableId,
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
            ESGraphAsset asset = ScriptableObject.CreateInstance<ESGraphAsset>();
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
        public void StableGraph_InsertionMutationRollsBackAtomically()
        {
            ESGraphAsset graph = ScriptableObject.CreateInstance<ESGraphAsset>();
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

        private static ESGraphNodeRecord CreateCardNode(string payloadJson, string nodeTypeId = null)
        {
            return new ESGraphNodeRecord
            {
                nodeId = "node-card-test",
                typeId = nodeTypeId
                    ?? ESGraphNodeTypeKey.FromKind(ESGraphBuiltInNodeKind.AgentGoal).StableId,
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
                ESGraphAsset.CurrentSchemaVersion,
                ESGraphDomainIds.AgentAuthoring,
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
