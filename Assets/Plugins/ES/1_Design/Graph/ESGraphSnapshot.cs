using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using GraphAsset = global::ES.ESGraphAssetBase;

namespace ES
{
    public sealed class ESGraphPortSnapshot
    {
        public string PortId { get; }
        public string StableKey { get; }
        public string Name { get; }
        public string ValueTypeId { get; }
        public ESGraphPortValueKind ValueKind => ESGraphPortValueCatalog.GetKind(ValueTypeId);
        public ESGraphPortDirection Direction { get; }
        public ESGraphPortCapacity Capacity { get; }

        internal ESGraphPortSnapshot(ESGraphPortRecord source)
        {
            PortId = source.portId;
            StableKey = source.stableKey;
            Name = source.name;
            ValueTypeId = source.valueTypeId;
            Direction = source.direction;
            Capacity = source.capacity;
        }
    }

    public sealed class ESGraphNodeSnapshot
    {
        private readonly ESGraphPortSnapshot[] ports;
        public string NodeId { get; }
        public string TypeId { get; }
        public ESGraphNodeTypeKey NodeType => ESGraphNodeTypeKey.Parse(TypeId);
        public ESGraphBuiltInNodeKind BuiltInKind => ESGraphNodeTypeCatalog.GetKind(TypeId);
        public int Version { get; }
        public string Title { get; }
        public string PayloadJson { get; }
        public IReadOnlyList<ESGraphPortSnapshot> Ports => ports;

        internal ESGraphNodeSnapshot(ESGraphNodeRecord source)
        {
            NodeId = source.nodeId;
            TypeId = source.typeId;
            Version = source.version;
            Title = source.title;
            PayloadJson = source.payloadJson;
            List<ESGraphPortRecord> ordered = source.ports != null
                ? new List<ESGraphPortRecord>(source.ports)
                : new List<ESGraphPortRecord>();
            ordered.Sort((left, right) => string.CompareOrdinal(left?.portId, right?.portId));
            ports = new ESGraphPortSnapshot[ordered.Count];
            for (int i = 0; i < ordered.Count; i++)
                ports[i] = new ESGraphPortSnapshot(ordered[i]);
        }
    }

    public sealed class ESGraphEdgeSnapshot
    {
        public string EdgeId { get; }
        public string OutputPortId { get; }
        public string InputPortId { get; }

        internal ESGraphEdgeSnapshot(ESGraphEdgeRecord source)
        {
            EdgeId = source.edgeId;
            OutputPortId = source.outputPortId;
            InputPortId = source.inputPortId;
        }
    }

    /// <summary>Validated immutable graph data. Domain bakers may consume it; it is not a universal runner.</summary>
    public sealed class ESBakedGraphSnapshot
    {
        private readonly ESGraphNodeSnapshot[] nodes;
        private readonly ESGraphEdgeSnapshot[] edges;
        private readonly Dictionary<string, ESGraphNodeSnapshot> nodesById;
        private readonly Dictionary<string, ESGraphPortSnapshot> portsById;
        private readonly Dictionary<string, ESGraphEdgeSnapshot> edgesById;

        public int SchemaVersion { get; }
        public string GraphId { get; }
        public string OriginGraphId { get; }
        public string DomainId { get; }
        public ESGraphDomainKey Domain => ESGraphDomainKey.Parse(DomainId);
        public ESGraphDomainKind DomainKind => ESGraphDomainCatalog.GetKind(DomainId);
        public bool AllowCycles { get; }
        public string ContentSignature { get; }
        public IReadOnlyList<ESGraphNodeSnapshot> Nodes => nodes;
        public IReadOnlyList<ESGraphEdgeSnapshot> Edges => edges;

        internal ESBakedGraphSnapshot(GraphAsset asset, List<ESGraphNodeRecord> orderedNodes,
            List<ESGraphEdgeRecord> orderedEdges, string signature)
        {
            SchemaVersion = asset.schemaVersion;
            GraphId = asset.GraphId;
            OriginGraphId = asset.OriginGraphId;
            DomainId = asset.DomainId;
            AllowCycles = asset.AllowsCycles;
            ContentSignature = signature;
            nodes = new ESGraphNodeSnapshot[orderedNodes.Count];
            edges = new ESGraphEdgeSnapshot[orderedEdges.Count];
            nodesById = new Dictionary<string, ESGraphNodeSnapshot>(orderedNodes.Count, StringComparer.Ordinal);
            portsById = new Dictionary<string, ESGraphPortSnapshot>(StringComparer.Ordinal);
            edgesById = new Dictionary<string, ESGraphEdgeSnapshot>(orderedEdges.Count, StringComparer.Ordinal);

            for (int i = 0; i < orderedNodes.Count; i++)
            {
                ESGraphNodeSnapshot node = new ESGraphNodeSnapshot(orderedNodes[i]);
                nodes[i] = node;
                nodesById.Add(node.NodeId, node);
                for (int p = 0; p < node.Ports.Count; p++)
                    portsById.Add(node.Ports[p].PortId, node.Ports[p]);
            }
            for (int i = 0; i < orderedEdges.Count; i++)
            {
                ESGraphEdgeSnapshot edge = new ESGraphEdgeSnapshot(orderedEdges[i]);
                edges[i] = edge;
                edgesById.Add(edge.EdgeId, edge);
            }
        }

        public bool TryGetNode(string nodeId, out ESGraphNodeSnapshot node)
        {
            return nodesById.TryGetValue(nodeId ?? string.Empty, out node);
        }

        public bool TryGetPort(string portId, out ESGraphPortSnapshot port)
        {
            return portsById.TryGetValue(portId ?? string.Empty, out port);
        }

        public bool TryGetEdge(string edgeId, out ESGraphEdgeSnapshot edge)
        {
            return edgesById.TryGetValue(edgeId ?? string.Empty, out edge);
        }
    }

    public static class ESGraphSnapshotBaker
    {
        public static bool TryBake(GraphAsset asset, out ESBakedGraphSnapshot snapshot,
            out List<ESGraphValidationIssue> issues)
        {
            snapshot = null;
            if (asset == null)
            {
                issues = new List<ESGraphValidationIssue>
                {
                    ESGraphValidationIssue.Error("Graph.Asset.Null", "Graph Asset 不能为空。")
                };
                return false;
            }

            issues = asset.ValidateGraph();
            for (int i = 0; i < issues.Count; i++)
            {
                ESGraphValidationIssue issue = issues[i];
                if (issue != null && issue.severity == ESGraphValidationSeverity.Error)
                    return false;
            }

            List<ESGraphNodeRecord> orderedNodes = new List<ESGraphNodeRecord>(asset.Nodes.Count);
            for (int i = 0; i < asset.Nodes.Count; i++) orderedNodes.Add(asset.Nodes[i]);
            orderedNodes.Sort((left, right) => string.CompareOrdinal(left.nodeId, right.nodeId));
            List<ESGraphEdgeRecord> orderedEdges = new List<ESGraphEdgeRecord>(asset.Edges.Count);
            for (int i = 0; i < asset.Edges.Count; i++) orderedEdges.Add(asset.Edges[i]);
            orderedEdges.Sort((left, right) => string.CompareOrdinal(left.edgeId, right.edgeId));

            string signature = CalculateSignature(asset.schemaVersion, asset.DomainId, asset.AllowsCycles, orderedNodes, orderedEdges);
            snapshot = new ESBakedGraphSnapshot(asset, orderedNodes, orderedEdges, signature);
            return true;
        }

        private static string CalculateSignature(int schemaVersion, string domainId, bool allowCycles,
            List<ESGraphNodeRecord> nodes, List<ESGraphEdgeRecord> edges)
        {
            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                writer.Write(schemaVersion);
                WriteString(writer, domainId);
                writer.Write(allowCycles);
                writer.Write(nodes.Count);
                for (int i = 0; i < nodes.Count; i++)
                {
                    ESGraphNodeRecord node = nodes[i];
                    WriteString(writer, node.nodeId);
                    WriteString(writer, node.typeId);
                    writer.Write(node.version);
                    WriteString(writer, node.title);
                    // Unity may normalize an unassigned serialized string from null to empty after
                    // reorder/apply. Both forms mean "no payload" for the graph contract and must
                    // produce the same content signature.
                    WriteString(writer, node.payloadJson ?? string.Empty);
                    List<ESGraphPortRecord> ports = node.ports != null
                        ? new List<ESGraphPortRecord>(node.ports)
                        : new List<ESGraphPortRecord>();
                    ports.Sort((left, right) => string.CompareOrdinal(left.portId, right.portId));
                    writer.Write(ports.Count);
                    for (int p = 0; p < ports.Count; p++)
                    {
                        ESGraphPortRecord port = ports[p];
                        WriteString(writer, port.portId);
                        WriteString(writer, port.stableKey);
                        WriteString(writer, port.name);
                        WriteString(writer, port.valueTypeId);
                        writer.Write((byte)port.direction);
                        writer.Write((byte)port.capacity);
                    }
                }

                writer.Write(edges.Count);
                for (int i = 0; i < edges.Count; i++)
                {
                    ESGraphEdgeRecord edge = edges[i];
                    WriteString(writer, edge.edgeId);
                    WriteString(writer, edge.outputPortId);
                    WriteString(writer, edge.inputPortId);
                }
                writer.Flush();
                stream.Position = 0;
                using (SHA256 sha = SHA256.Create())
                {
                    byte[] hash = sha.ComputeHash(stream);
                    StringBuilder builder = new StringBuilder(hash.Length * 2);
                    for (int i = 0; i < hash.Length; i++) builder.Append(hash[i].ToString("x2"));
                    return builder.ToString();
                }
            }
        }

        private static void WriteString(BinaryWriter writer, string value)
        {
            if (value == null)
            {
                writer.Write(-1);
                return;
            }
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            writer.Write(bytes.Length);
            writer.Write(bytes);
        }
    }
}
