using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using GraphAsset = global::ES.ESGraphAssetBase;

namespace ES.EditorInternal
{
    internal struct ESGraphEditResult
    {
        public bool changed;
        public bool rebuildRequired;
        public string error;
        public string createdEdgeId;
        public List<string> createdEdgeIds;
        public string changedNodeId;
        public List<string> createdNodeIds;
        public ESGraphChange change;
    }

    internal sealed class ESGraphEditService
    {
        private readonly Action<GraphAsset> markDirty;
        private readonly Action requestAutoSave;
        private readonly Action<ESGraphChange> notifyModelChanged;

        public ESGraphEditService(
            Action<GraphAsset> markDirty,
            Action requestAutoSave,
            Action<ESGraphChange> notifyModelChanged)
        {
            this.markDirty = markDirty;
            this.requestAutoSave = requestAutoSave;
            this.notifyModelChanged = notifyModelChanged;
        }

        public ESGraphEditResult CreateNode(
            GraphAsset asset,
            IESGraphNodeDefinition definition,
            Vector2 position)
        {
            if (asset == null || definition == null || !definition.Domain.Equals(asset.DomainKey))
                return Fail("节点定义与当前图不匹配。");

            Undo.RecordObject(asset, "创建图节点");
            ESGraphNodeRecord created = asset.AddNode(
                definition.NodeType, definition.DisplayName, position, definition.Ports);
            asset.UpdateNode(created.nodeId, definition.NodeType, definition.CurrentVersion,
                created.title, definition.CreateDefaultPayload(), out _);
            var result = new ESGraphEditResult
            {
                changed = true,
                rebuildRequired = true,
                createdNodeIds = new List<string> { created.nodeId },
                change = new ESGraphChange(ESGraphChangeKind.Structure, created.nodeId,
                    nodeIds: new[] { created.nodeId })
            };
            Commit(asset, result.change);
            return result;
        }

        public ESGraphEditResult CreateNodeAndConnect(
            GraphAsset asset,
            ESStableGraphNodeCreationChoice choice,
            Vector2 position)
        {
            IESGraphNodeDefinition definition = choice.Definition;
            if (asset == null
                || definition == null
                || choice.CompatiblePort == null
                || !definition.Domain.Equals(asset.DomainKey))
                return Fail("创建并连接节点参数非法。");

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("创建并连接图节点");
            Undo.RegisterCompleteObjectUndo(asset, "创建并连接图节点");
            try
            {
                ESGraphNodeRecord created = asset.AddNode(
                    definition.NodeType, definition.DisplayName, position, definition.Ports);
                asset.UpdateNode(created.nodeId, definition.NodeType, definition.CurrentVersion,
                    created.title, definition.CreateDefaultPayload(), out _);
                ESGraphPortRecord targetPort = FindPort(created, choice.CompatiblePort);
                if (targetPort == null)
                    throw new InvalidOperationException("新节点没有找到预期的兼容端口。");
                if (!asset.TryFindPort(choice.SourcePortId, out _, out ESGraphPortRecord sourcePort))
                    throw new InvalidOperationException("起始端口已不存在，请重新拖线。");

                if (!string.IsNullOrEmpty(choice.ReplaceEdgeId))
                {
                    ESGraphEdgeRecord replacement = asset.FindEdge(choice.ReplaceEdgeId);
                    if (replacement == null
                        || (!string.Equals(replacement.outputPortId, choice.SourcePortId, StringComparison.Ordinal)
                            && !string.Equals(replacement.inputPortId, choice.SourcePortId, StringComparison.Ordinal)))
                        throw new InvalidOperationException("原关系已不存在，请重新选择续接目标。");
                    if (!asset.RemoveEdge(choice.ReplaceEdgeId))
                        throw new InvalidOperationException("原关系删除失败，请重试。");
                }

                string outputPortId = sourcePort.direction == ESGraphPortDirection.Output
                    ? sourcePort.portId : targetPort.portId;
                string inputPortId = sourcePort.direction == ESGraphPortDirection.Input
                    ? sourcePort.portId : targetPort.portId;
                if (!asset.TryAddEdge(outputPortId, inputPortId, out _, out string error))
                    throw new InvalidOperationException(error);

                Undo.CollapseUndoOperations(undoGroup);
                var result = new ESGraphEditResult
                {
                    changed = true,
                    rebuildRequired = true,
                    createdNodeIds = new List<string> { created.nodeId },
                    change = new ESGraphChange(ESGraphChangeKind.Structure, created.nodeId,
                        nodeIds: new[] { created.nodeId })
                };
                Commit(asset, result.change);
                return result;
            }
            catch (Exception exception)
            {
                Undo.RevertAllDownToGroup(undoGroup);
                return Fail(exception.Message);
            }
        }

        public ESGraphEditResult InsertNodeOnEdge(
            GraphAsset asset,
            IESGraphNodeDefinition definition,
            int inputPortIndex,
            int outputPortIndex,
            string edgeId,
            Vector2 position)
        {
            if (asset == null
                || definition == null
                || definition.Ports == null
                || inputPortIndex < 0
                || inputPortIndex >= definition.Ports.Count
                || outputPortIndex < 0
                || outputPortIndex >= definition.Ports.Count
                || string.IsNullOrEmpty(edgeId)
                || !definition.Domain.Equals(asset.DomainKey))
                return Fail("插入节点参数非法。");

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("在关系中间插入节点");
            Undo.RegisterCompleteObjectUndo(asset, "在关系中间插入节点");
            try
            {
                ESGraphEdgeRecord oldEdge = asset.FindEdge(edgeId);
                if (oldEdge == null)
                    throw new InvalidOperationException("原关系已不存在，请重新选择。");

                ESGraphNodeRecord created = asset.AddNode(
                    definition.NodeType, definition.DisplayName, position, definition.Ports);
                asset.UpdateNode(created.nodeId, definition.NodeType, definition.CurrentVersion,
                    created.title, definition.CreateDefaultPayload(), out _);
                ESGraphPortRecord inputPort = FindPort(created, definition.Ports[inputPortIndex]);
                ESGraphPortRecord outputPort = FindPort(created, definition.Ports[outputPortIndex]);
                if (inputPort == null || outputPort == null)
                    throw new InvalidOperationException("新节点没有找到可插入的输入/输出端口。");

                if (!asset.RemoveEdge(edgeId))
                    throw new InvalidOperationException("原关系删除失败，请重试。");
                if (!asset.TryAddEdge(oldEdge.outputPortId, inputPort.portId, out _, out string firstError))
                    throw new InvalidOperationException(firstError);
                if (!asset.TryAddEdge(outputPort.portId, oldEdge.inputPortId, out _, out string secondError))
                    throw new InvalidOperationException(secondError);

                Undo.CollapseUndoOperations(undoGroup);
                var result = new ESGraphEditResult
                {
                    changed = true,
                    rebuildRequired = true,
                    createdNodeIds = new List<string> { created.nodeId },
                    change = new ESGraphChange(ESGraphChangeKind.Structure, created.nodeId,
                        nodeIds: new[] { created.nodeId })
                };
                Commit(asset, result.change);
                return result;
            }
            catch (Exception exception)
            {
                Undo.RevertAllDownToGroup(undoGroup);
                return Fail(exception.Message);
            }
        }

        public ESGraphEditResult AddEdge(
            GraphAsset asset,
            string outputPortId,
            string inputPortId)
        {
            if (asset == null
                || string.IsNullOrEmpty(outputPortId)
                || string.IsNullOrEmpty(inputPortId))
                return Fail("连线参数非法。");

            Undo.RecordObject(asset, "创建图连线");
            if (!asset.TryAddEdge(outputPortId, inputPortId, out ESGraphEdgeRecord record, out string error))
                return Fail(error);
            var result = new ESGraphEditResult
            {
                changed = true,
                createdEdgeId = record.edgeId,
                change = new ESGraphChange(ESGraphChangeKind.Structure, edgeId: record.edgeId,
                    edgeIds: new[] { record.edgeId })
            };
            Commit(asset, result.change);
            return result;
        }

        public ESGraphEditResult ReconnectEdge(
            GraphAsset asset,
            string edgeId,
            string firstPortId,
            string secondPortId)
        {
            if (asset == null
                || string.IsNullOrEmpty(edgeId)
                || string.IsNullOrEmpty(firstPortId)
                || string.IsNullOrEmpty(secondPortId))
                return Fail("重连参数非法。");

            ESGraphEdgeRecord edge = asset.FindEdge(edgeId);
            if (edge == null)
                return Fail("需要重连的关系不存在。");
            bool sameEndpoints = (string.Equals(edge.outputPortId, firstPortId, StringComparison.Ordinal)
                    && string.Equals(edge.inputPortId, secondPortId, StringComparison.Ordinal))
                || (string.Equals(edge.outputPortId, secondPortId, StringComparison.Ordinal)
                    && string.Equals(edge.inputPortId, firstPortId, StringComparison.Ordinal));
            if (sameEndpoints)
                return new ESGraphEditResult();

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("重连图连线");
            Undo.RegisterCompleteObjectUndo(asset, "重连图连线");
            if (!asset.TryReconnectEdge(edgeId, firstPortId, secondPortId, out string error))
            {
                Undo.RevertAllDownToGroup(undoGroup);
                return Fail(string.IsNullOrEmpty(error) ? "连线端点没有变化。" : error);
            }

            Undo.CollapseUndoOperations(undoGroup);
            var result = new ESGraphEditResult
            {
                changed = true,
                rebuildRequired = true,
                change = new ESGraphChange(ESGraphChangeKind.Structure, edgeId: edgeId,
                    edgeIds: new[] { edgeId })
            };
            Commit(asset, result.change);
            return result;
        }

        public ESGraphEditResult MoveEdge(GraphAsset asset, string edgeId, int direction)
        {
            if (asset == null || string.IsNullOrEmpty(edgeId)
                || direction != -1 && direction != 1)
                return Fail("关系顺序参数非法。");

            string undoName = direction < 0 ? "前移图关系" : "后移图关系";
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(undoName);
            Undo.RegisterCompleteObjectUndo(asset, undoName);
            if (!asset.TryMoveEdge(edgeId, direction, out string error))
            {
                Undo.RevertAllDownToGroup(undoGroup);
                return Fail(error);
            }
            Undo.CollapseUndoOperations(undoGroup);
            var result = new ESGraphEditResult
            {
                changed = true,
                change = new ESGraphChange(ESGraphChangeKind.Structure, edgeId: edgeId,
                    edgeIds: new[] { edgeId })
            };
            Commit(asset, result.change);
            return result;
        }

        public ESGraphEditResult AddEdges(
            GraphAsset asset,
            IReadOnlyList<KeyValuePair<string, string>> endpoints)
        {
            if (asset == null || endpoints == null || endpoints.Count == 0)
                return Fail("没有可创建的连线。");
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.RegisterCompleteObjectUndo(asset, "创建图连线");
            var createdEdgeIds = new List<string>(endpoints.Count);
            try
            {
                for (int i = 0; i < endpoints.Count; i++)
                {
                    if (!asset.TryAddEdge(endpoints[i].Key, endpoints[i].Value,
                            out ESGraphEdgeRecord record, out string error))
                        throw new InvalidOperationException(error);
                    createdEdgeIds.Add(record.edgeId);
                }
                Undo.CollapseUndoOperations(undoGroup);
            }
            catch (Exception exception)
            {
                Undo.RevertAllDownToGroup(undoGroup);
                return Fail(exception.Message);
            }
            var result = new ESGraphEditResult
            {
                changed = true,
                createdEdgeIds = createdEdgeIds,
                change = new ESGraphChange(ESGraphChangeKind.Structure, edgeIds: createdEdgeIds)
            };
            Commit(asset, result.change);
            return result;
        }

        public ESGraphEditResult DuplicateNodes(
            GraphAsset asset,
            IReadOnlyCollection<string> nodeIds,
            Vector2 offset)
        {
            if (asset == null || nodeIds == null || nodeIds.Count == 0)
                return Fail("没有可复制的节点。");
            Undo.RecordObject(asset, "复制图节点");
            var result = new ESGraphEditResult
            {
                changed = true,
                rebuildRequired = true,
                createdNodeIds = asset.DuplicateNodes(nodeIds, offset)
            };
            if (result.createdNodeIds.Count == 0)
                return Fail("复制节点失败。");
            result.change = new ESGraphChange(ESGraphChangeKind.Structure,
                result.createdNodeIds[0], nodeIds: result.createdNodeIds);
            Commit(asset, result.change);
            return result;
        }

        public ESGraphEditResult PasteNodes(
            GraphAsset asset,
            IReadOnlyList<ESGraphNodeRecord> sourceNodes,
            IReadOnlyList<ESGraphEdgeRecord> sourceEdges,
            Vector2 offset,
            int sourceSchemaVersion,
            string sourceDomainId)
        {
            if (asset == null)
                return Fail("图资产不可用。");
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.RegisterCompleteObjectUndo(asset, "粘贴图节点");
            List<string> createdIds = asset.PasteNodes(
                sourceNodes,
                sourceEdges,
                offset,
                out string error,
                sourceSchemaVersion,
                sourceDomainId,
                out _);
            if (createdIds.Count == 0)
            {
                Undo.RevertAllDownToGroup(undoGroup);
                return Fail(string.IsNullOrEmpty(error) ? "剪贴板粘贴失败。" : error);
            }
            Undo.CollapseUndoOperations(undoGroup);
            var result = new ESGraphEditResult
            {
                changed = true,
                rebuildRequired = true,
                createdNodeIds = createdIds,
                change = new ESGraphChange(ESGraphChangeKind.Structure, createdIds[0],
                    nodeIds: createdIds)
            };
            Commit(asset, result.change);
            return result;
        }

        public ESGraphEditResult SetNodePositions(
            GraphAsset asset,
            IReadOnlyDictionary<string, Vector2> positions,
            string undoName)
        {
            if (asset == null || positions == null || positions.Count == 0)
                return Fail("没有需要更新的节点位置。");
            var changedPositions = new List<KeyValuePair<string, Vector2>>(positions.Count);
            foreach (KeyValuePair<string, Vector2> pair in positions)
            {
                ESGraphNodeRecord node = asset.FindNode(pair.Key);
                if (node != null && node.position != pair.Value)
                    changedPositions.Add(pair);
            }
            if (changedPositions.Count == 0)
                return new ESGraphEditResult();
            Undo.RecordObject(asset, undoName);
            var changedNodeIds = new List<string>(changedPositions.Count);
            for (int i = 0; i < changedPositions.Count; i++)
            {
                KeyValuePair<string, Vector2> pair = changedPositions[i];
                asset.SetNodePosition(pair.Key, pair.Value);
                changedNodeIds.Add(pair.Key);
            }
            var result = new ESGraphEditResult
            {
                changed = true,
                change = new ESGraphChange(ESGraphChangeKind.Layout,
                    changedNodeIds[0], nodeIds: changedNodeIds)
            };
            Commit(asset, result.change);
            return result;
        }

        public ESGraphEditResult SetNodeContent(
            GraphAsset asset,
            string nodeId,
            string typeId,
            int version,
            string title,
            string payloadJson)
        {
            if (asset == null || string.IsNullOrEmpty(nodeId))
                return Fail("节点参数非法。");
            ESGraphNodeRecord node = asset.FindNode(nodeId);
            if (node == null)
                return Fail("节点不存在。");
            string normalizedPayload = payloadJson ?? string.Empty;
            bool contentChanged = !string.Equals(node.typeId, typeId, StringComparison.Ordinal)
                || node.version != version
                || !string.Equals(node.title, title, StringComparison.Ordinal)
                || !string.Equals(node.payloadJson ?? string.Empty, normalizedPayload,
                    StringComparison.Ordinal);
            if (!contentChanged)
                return new ESGraphEditResult();
            Undo.RecordObject(asset, "修改图节点");
            if (!asset.UpdateNode(nodeId, typeId, version, title, normalizedPayload, out string error))
                return Fail(error);
            var result = new ESGraphEditResult
            {
                changed = true,
                changedNodeId = nodeId,
                rebuildRequired = true,
                change = new ESGraphChange(ESGraphChangeKind.Content, nodeId,
                    nodeIds: new[] { nodeId })
            };
            Commit(asset, result.change);
            return result;
        }

        public ESGraphEditResult UpgradeSchema(GraphAsset asset)
        {
            if (asset == null)
                return Fail("图资产不可用。");
            if (asset.schemaVersion == GraphAsset.CurrentSchemaVersion)
                return new ESGraphEditResult();

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("升级图数据");
            Undo.RegisterCompleteObjectUndo(asset, "升级图数据");
            if (!asset.TryUpgradeSchema(out bool changed, out string error))
            {
                Undo.RevertAllDownToGroup(undoGroup);
                return Fail(error);
            }
            if (!changed)
            {
                Undo.RevertAllDownToGroup(undoGroup);
                return new ESGraphEditResult();
            }

            Undo.CollapseUndoOperations(undoGroup);
            var result = new ESGraphEditResult
            {
                changed = true,
                rebuildRequired = true,
                change = new ESGraphChange(ESGraphChangeKind.Schema)
            };
            Commit(asset, result.change);
            return result;
        }

        public ESGraphEditResult DeleteElements(
            GraphAsset asset,
            IReadOnlyCollection<string> nodeIds,
            IReadOnlyCollection<string> edgeIds)
        {
            if (asset == null)
                return Fail("图资产不可用。");
            if ((nodeIds == null || nodeIds.Count == 0)
                && (edgeIds == null || edgeIds.Count == 0))
                return Fail("没有可删除的元素。");
            var removedNodeIds = new List<string>();
            var removedEdgeIds = new List<string>();
            Undo.RecordObject(asset, "删除图元素");
            if (nodeIds != null)
                foreach (string nodeId in nodeIds)
                    if (asset.RemoveNode(nodeId))
                        removedNodeIds.Add(nodeId);
            if (edgeIds != null)
                foreach (string edgeId in edgeIds)
                    if (asset.RemoveEdge(edgeId))
                        removedEdgeIds.Add(edgeId);
            if (removedNodeIds.Count == 0 && removedEdgeIds.Count == 0)
                return Fail("没有找到可删除的图元素。");
            var result = new ESGraphEditResult
            {
                changed = true,
                rebuildRequired = true,
                change = new ESGraphChange(ESGraphChangeKind.Structure,
                    nodeIds: removedNodeIds, edgeIds: removedEdgeIds)
            };
            Commit(asset, result.change);
            return result;
        }

        private static ESGraphPortRecord FindPort(ESGraphNodeRecord node,
            ESGraphPortDefinition preferred)
        {
            if (node == null || preferred == null
                || !node.TryGetPort(preferred.stableKey, out ESGraphPortRecord port)
                || port.direction != preferred.direction)
                return null;
            return port;
        }

        private static ESGraphEditResult Fail(string error)
        {
            return new ESGraphEditResult { error = error ?? string.Empty };
        }

        private void Commit(GraphAsset asset, ESGraphChange change)
        {
            markDirty?.Invoke(asset);
            requestAutoSave?.Invoke();
            notifyModelChanged?.Invoke(change);
        }
    }
}
