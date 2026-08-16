using System;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{
    public enum ESWorldDialoguePlacementSpace : byte
    {
        Map2D = 0,
        Scene3D = 1,
        Scene2D = 2
    }

    [Serializable]
    public sealed class ESWorldDialoguePortData
    {
        public string portId;
        public string displayName = "选项";
        public bool terminal;
    }

    [Serializable]
    public sealed class ESWorldDialogueNodeData
    {
        public string nodeId;
        public string title = "对话节点";
        public string speaker = "旁白";
        [TextArea(2, 8)] public string text = "请输入对话内容。";
        public Vector2 graphPosition = new Vector2(80f, 80f);
        public List<ESWorldDialoguePortData> outputs = new List<ESWorldDialoguePortData>();
    }

    [Serializable]
    public sealed class ESWorldDialogueEdgeData
    {
        public string edgeId;
        public string fromNodeId;
        public string fromPortId;
        public string toNodeId;
        public string toPortId;
        public string conditionKey;
    }

    [Serializable]
    public sealed class ESWorldDialogueGraphDefinition
    {
        public const int CurrentSchemaVersion = 1;
        public string graphId;
        public int schemaVersion = CurrentSchemaVersion;
        public int contentVersion = 1;
        public string contentHash;
        public string entryNodeId;
        public List<ESWorldDialogueNodeData> nodes = new List<ESWorldDialogueNodeData>();
        public List<ESWorldDialogueEdgeData> edges = new List<ESWorldDialogueEdgeData>();

        public bool EnsureStableIds()
        {
            bool changed = false;
            if (string.IsNullOrWhiteSpace(graphId)) { graphId = Guid.NewGuid().ToString("N"); changed = true; }
            if (nodes == null) { nodes = new List<ESWorldDialogueNodeData>(); changed = true; }
            if (edges == null) { edges = new List<ESWorldDialogueEdgeData>(); changed = true; }

            var nodeIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < nodes.Count; i++)
            {
                ESWorldDialogueNodeData node = nodes[i] ?? new ESWorldDialogueNodeData();
                if (nodes[i] == null) { nodes[i] = node; changed = true; }
                if (string.IsNullOrWhiteSpace(node.nodeId))
                {
                    node.nodeId = Guid.NewGuid().ToString("N");
                    nodeIds.Add(node.nodeId);
                    changed = true;
                }
                else nodeIds.Add(node.nodeId);
                if (node.outputs == null) { node.outputs = new List<ESWorldDialoguePortData>(); changed = true; }
                var portIds = new HashSet<string>(StringComparer.Ordinal);
                for (int p = 0; p < node.outputs.Count; p++)
                {
                    ESWorldDialoguePortData port = node.outputs[p] ?? new ESWorldDialoguePortData();
                    if (node.outputs[p] == null) { node.outputs[p] = port; changed = true; }
                    if (string.IsNullOrWhiteSpace(port.portId))
                    {
                        port.portId = Guid.NewGuid().ToString("N");
                        portIds.Add(port.portId);
                        changed = true;
                    }
                    else portIds.Add(port.portId);
                }
            }

            var edgeIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < edges.Count; i++)
            {
                ESWorldDialogueEdgeData edge = edges[i] ?? new ESWorldDialogueEdgeData();
                if (edges[i] == null) { edges[i] = edge; changed = true; }
                if (string.IsNullOrWhiteSpace(edge.edgeId))
                {
                    edge.edgeId = Guid.NewGuid().ToString("N");
                    edgeIds.Add(edge.edgeId);
                    changed = true;
                }
                else edgeIds.Add(edge.edgeId);
            }
            if (string.IsNullOrWhiteSpace(entryNodeId) && nodes.Count > 0)
            {
                entryNodeId = nodes[0].nodeId;
                changed = true;
            }
            return changed;
        }

        public ESWorldDialogueNodeData FindNode(string nodeId)
        {
            if (nodes == null || string.IsNullOrWhiteSpace(nodeId)) return null;
            for (int i = 0; i < nodes.Count; i++)
                if (nodes[i] != null && string.Equals(nodes[i].nodeId, nodeId, StringComparison.Ordinal)) return nodes[i];
            return null;
        }

        public bool IsValid(out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(graphId)) { error = "对话图 graphId 不能为空。"; return false; }
            if (schemaVersion != CurrentSchemaVersion) { error = "对话图 schemaVersion 不受支持：" + schemaVersion; return false; }
            if (nodes == null || nodes.Count == 0) { error = "对话图至少需要一个节点。"; return false; }
            var nodeIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < nodes.Count; i++)
            {
                ESWorldDialogueNodeData node = nodes[i];
                if (node == null || string.IsNullOrWhiteSpace(node.nodeId) || !nodeIds.Add(node.nodeId)) { error = "对话图节点 ID 为空或重复。"; return false; }
                var portIds = new HashSet<string>(StringComparer.Ordinal);
                if (node.outputs != null)
                    for (int p = 0; p < node.outputs.Count; p++)
                        if (node.outputs[p] == null || string.IsNullOrWhiteSpace(node.outputs[p].portId) || !portIds.Add(node.outputs[p].portId)) { error = "对话图端口 ID 为空或重复：" + node.nodeId; return false; }
            }
            if (FindNode(entryNodeId) == null) { error = "对话图入口节点不存在。"; return false; }
            if (edges == null) return true;
            var edgeIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < edges.Count; i++)
            {
                ESWorldDialogueEdgeData edge = edges[i];
                if (edge == null || string.IsNullOrWhiteSpace(edge.edgeId) || !edgeIds.Add(edge.edgeId)) { error = "对话图边 ID 为空或重复。"; return false; }
                ESWorldDialogueNodeData from = FindNode(edge.fromNodeId);
                ESWorldDialogueNodeData to = FindNode(edge.toNodeId);
                if (from == null || to == null) { error = "对话图边引用了不存在的节点。"; return false; }
                bool fromPortExists = from.outputs != null && from.outputs.Exists(port => port != null && port.portId == edge.fromPortId);
                if (!fromPortExists) { error = "对话图边引用了不存在的输出端口。"; return false; }
            }
            return true;
        }
    }

    [CreateAssetMenu(fileName = "ESWorldDialogueGraph", menuName = "【ES】/内容/世界/对话图资产", order = 121)]
    public sealed class ESWorldDialogueGraphAsset : ScriptableObject
    {
        [SerializeField] private ESWorldDialogueGraphDefinition definition = new ESWorldDialogueGraphDefinition();

        public ESWorldDialogueGraphDefinition Definition => definition;

        public bool Validate(out string error)
        {
            error = null;
            if (definition == null) { error = "对话图资产缺少定义。"; return false; }
            return definition.IsValid(out error);
        }
    }

    [Serializable]
    public sealed class ESWorldDialoguePlacement
    {
        public string placementId;
        public string dialogueGraphKey;
        public string dialogueGraphAssetGuid;
        public string entryNodeId;
        public string displayName = "对话入口";
        public ESWorldDialoguePlacementSpace space = ESWorldDialoguePlacementSpace.Map2D;
        public Vector3 position;
        public Vector3 eulerAngles;
        public Vector3 scale = Vector3.one;
        public string scenePath;
        public string sceneObjectKey;
    }

    public sealed class ESWorldDialogueAnchor : MonoBehaviour
    {
        public string placementId;
        public string dialogueGraphKey;
        public string dialogueGraphAssetGuid;
        public string entryNodeId;
        public string mapAssetGuid;
        public string sceneObjectKey;
        public ESWorldDialoguePlacementSpace placementSpace = ESWorldDialoguePlacementSpace.Scene3D;

        private void OnDrawGizmos()
        {
            Gizmos.color = placementSpace == ESWorldDialoguePlacementSpace.Scene2D ? new Color(0.3f, 0.9f, 1f, 0.9f) : new Color(1f, 0.75f, 0.2f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, 0.45f);
            Gizmos.DrawLine(transform.position, transform.position + transform.up * 1.2f);
        }
    }
}
