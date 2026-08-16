using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Runtime.CompilerServices;
using GraphAsset = global::ES.ESGraphAssetBase;

[assembly: InternalsVisibleTo("ES_Editor")]

namespace ES
{
    public sealed class ESGraphPortSnapshot
    {
        public string NodeId { get; }
        public string PortId { get; }
        public string StableKey { get; }
        public string Name { get; }
        public string Meaning { get; }
        public string ValueTypeId { get; }
        public ESGraphPortValueKind ValueKind => ESGraphPortValueCatalog.GetKind(ValueTypeId);
        public ESGraphPortDirection Direction { get; }
        public ESGraphPortCapacity Capacity { get; }
        public ESGraphPortAggregation Aggregation { get; }
        public ESGraphEndpointKey Endpoint { get; }

        internal ESGraphPortSnapshot(string nodeId, ESGraphPortRecord source)
        {
            NodeId = nodeId ?? string.Empty;
            PortId = source.portId;
            StableKey = source.stableKey;
            Name = source.name;
            Meaning = source.meaning;
            ValueTypeId = source.valueTypeId;
            Direction = source.direction;
            Capacity = source.capacity;
            Aggregation = ESGraphPortAggregationRules.Resolve(source.direction, source.capacity,
                source.aggregation);
            Endpoint = new ESGraphEndpointKey(NodeId, StableKey);
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
                ports[i] = new ESGraphPortSnapshot(NodeId, ordered[i]);
        }
    }

    public sealed class ESGraphEdgeSnapshot
    {
        public string EdgeId { get; }
        public string OutputPortId { get; }
        public string InputPortId { get; }
        public int Order { get; }

        internal ESGraphEdgeSnapshot(ESGraphEdgeRecord source)
        {
            EdgeId = source.edgeId;
            OutputPortId = source.outputPortId;
            InputPortId = source.inputPortId;
            Order = source.order;
        }
    }

    /// <summary>
    /// Immutable semantic connection produced by the common Graph bake. An edge is expressed in
    /// endpoint terms, not only raw PortIds, so every domain can read one-to-many and many-to-one
    /// routes without rebuilding a second adjacency model.
    /// </summary>
    public sealed class ESGraphRouteSnapshot
    {
        public string EdgeId { get; }
        public int Order { get; }
        public ESGraphNodeSnapshot SourceNode { get; }
        public ESGraphPortSnapshot SourcePort { get; }
        public ESGraphNodeSnapshot TargetNode { get; }
        public ESGraphPortSnapshot TargetPort { get; }
        public string SourceNodeId { get; }
        public string SourcePortId { get; }
        public string SourcePortKey { get; }
        public string SourceMeaning { get; }
        public string SourceValueTypeId { get; }
        public string TargetNodeId { get; }
        public string TargetPortId { get; }
        public string TargetPortKey { get; }
        public string TargetMeaning { get; }
        public string TargetValueTypeId { get; }
        public ESGraphPortAggregation SourceAggregation { get; }
        public ESGraphPortAggregation TargetAggregation { get; }
        public ESGraphEndpointKey SourceEndpoint { get; }
        public ESGraphEndpointKey TargetEndpoint { get; }
        public bool IsFlow => string.Equals(SourceValueTypeId, ESGraphPortValueIds.Flow,
            StringComparison.Ordinal);

        internal ESGraphRouteSnapshot(string edgeId, int order, ESGraphNodeSnapshot sourceNode,
            ESGraphPortSnapshot sourcePort, ESGraphNodeSnapshot targetNode,
            ESGraphPortSnapshot targetPort)
        {
            EdgeId = edgeId ?? string.Empty;
            Order = order;
            SourceNode = sourceNode;
            SourcePort = sourcePort;
            TargetNode = targetNode;
            TargetPort = targetPort;
            SourceNodeId = sourceNode?.NodeId ?? string.Empty;
            SourcePortId = sourcePort?.PortId ?? string.Empty;
            SourcePortKey = sourcePort?.StableKey ?? string.Empty;
            SourceMeaning = sourcePort?.Meaning ?? string.Empty;
            SourceValueTypeId = sourcePort?.ValueTypeId ?? string.Empty;
            SourceAggregation = sourcePort?.Aggregation ?? ESGraphPortAggregation.Single;
            TargetNodeId = targetNode?.NodeId ?? string.Empty;
            TargetPortId = targetPort?.PortId ?? string.Empty;
            TargetPortKey = targetPort?.StableKey ?? string.Empty;
            TargetMeaning = targetPort?.Meaning ?? string.Empty;
            TargetValueTypeId = targetPort?.ValueTypeId ?? string.Empty;
            TargetAggregation = targetPort?.Aggregation ?? ESGraphPortAggregation.Single;
            SourceEndpoint = sourcePort?.Endpoint
                ?? new ESGraphEndpointKey(SourceNodeId, SourcePortKey);
            TargetEndpoint = targetPort?.Endpoint
                ?? new ESGraphEndpointKey(TargetNodeId, TargetPortKey);
        }
    }

    /// <summary>Validated immutable graph data. Domain bakers may consume it; it is not a universal runner.</summary>
    public sealed class ESBakedGraphSnapshot
    {
        private readonly ESGraphNodeSnapshot[] nodes;
        private readonly ESGraphEdgeSnapshot[] edges;
        private readonly Dictionary<string, ESGraphNodeSnapshot> nodesById;
        private readonly Dictionary<string, ESGraphPortSnapshot> portsById;
        private readonly Dictionary<ESGraphEndpointKey, ESGraphPortSnapshot> portsByEndpoint;
        private readonly Dictionary<string, ESGraphEdgeSnapshot> edgesById;
        private readonly ESGraphRouteSnapshot[] routes;
        private readonly Dictionary<ESGraphEndpointKey, IReadOnlyList<ESGraphRouteSnapshot>> outgoingRoutesByEndpoint;
        private readonly Dictionary<ESGraphEndpointKey, IReadOnlyList<ESGraphRouteSnapshot>> incomingRoutesByEndpoint;
        private readonly Dictionary<string, IReadOnlyList<ESGraphRouteSnapshot>> outgoingRoutesByPort;
        private readonly Dictionary<string, IReadOnlyList<ESGraphRouteSnapshot>> incomingRoutesByPort;

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
        public IReadOnlyList<ESGraphRouteSnapshot> Routes => routes;

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
            portsByEndpoint = new Dictionary<ESGraphEndpointKey, ESGraphPortSnapshot>();
            edgesById = new Dictionary<string, ESGraphEdgeSnapshot>(orderedEdges.Count, StringComparer.Ordinal);
            var nodeIdsByPort = new Dictionary<string, string>(StringComparer.Ordinal);

            for (int i = 0; i < orderedNodes.Count; i++)
            {
                ESGraphNodeSnapshot node = new ESGraphNodeSnapshot(orderedNodes[i]);
                nodes[i] = node;
                nodesById.Add(node.NodeId, node);
                for (int p = 0; p < node.Ports.Count; p++)
                {
                    portsById.Add(node.Ports[p].PortId, node.Ports[p]);
                    portsByEndpoint.Add(node.Ports[p].Endpoint, node.Ports[p]);
                    nodeIdsByPort.Add(node.Ports[p].PortId, node.NodeId);
                }
            }
            var routeList = new List<ESGraphRouteSnapshot>(orderedEdges.Count);
            var outgoingRoutes = new Dictionary<ESGraphEndpointKey, List<ESGraphRouteSnapshot>>();
            var incomingRoutes = new Dictionary<ESGraphEndpointKey, List<ESGraphRouteSnapshot>>();
            var outgoingByPort = new Dictionary<string, List<ESGraphRouteSnapshot>>(StringComparer.Ordinal);
            var incomingByPort = new Dictionary<string, List<ESGraphRouteSnapshot>>(StringComparer.Ordinal);
            for (int i = 0; i < orderedEdges.Count; i++)
            {
                ESGraphEdgeSnapshot edge = new ESGraphEdgeSnapshot(orderedEdges[i]);
                edges[i] = edge;
                edgesById.Add(edge.EdgeId, edge);
                if (!portsById.TryGetValue(edge.OutputPortId, out ESGraphPortSnapshot output)
                    || !portsById.TryGetValue(edge.InputPortId, out ESGraphPortSnapshot input)
                    || !nodeIdsByPort.TryGetValue(edge.OutputPortId, out string sourceNodeId)
                    || !nodeIdsByPort.TryGetValue(edge.InputPortId, out string targetNodeId)
                    || !nodesById.TryGetValue(sourceNodeId, out ESGraphNodeSnapshot sourceNode)
                    || !nodesById.TryGetValue(targetNodeId, out ESGraphNodeSnapshot targetNode))
                    continue;
                var route = new ESGraphRouteSnapshot(edge.EdgeId, edge.Order,
                    sourceNode, output, targetNode, input);
                routeList.Add(route);
                AddRoute(outgoingRoutes, route.SourceEndpoint, route);
                AddRoute(incomingRoutes, route.TargetEndpoint, route);
                AddRoute(outgoingByPort, route.SourcePortId, route);
                AddRoute(incomingByPort, route.TargetPortId, route);
            }
            routeList.Sort(CompareRoutes);
            routes = routeList.ToArray();
            outgoingRoutesByEndpoint = FreezeRouteIndex(outgoingRoutes);
            incomingRoutesByEndpoint = FreezeRouteIndex(incomingRoutes);
            outgoingRoutesByPort = FreezeRouteIndex(outgoingByPort);
            incomingRoutesByPort = FreezeRouteIndex(incomingByPort);
        }

        public bool TryGetNode(string nodeId, out ESGraphNodeSnapshot node)
        {
            return nodesById.TryGetValue(nodeId ?? string.Empty, out node);
        }

        public bool TryGetPort(string portId, out ESGraphPortSnapshot port)
        {
            return portsById.TryGetValue(portId ?? string.Empty, out port);
        }

        public bool TryGetPort(string nodeId, string portKey, out ESGraphPortSnapshot port)
        {
            return TryGetPort(new ESGraphEndpointKey(nodeId, portKey), out port);
        }

        public bool TryGetPort(ESGraphEndpointKey endpoint, out ESGraphPortSnapshot port)
        {
            return portsByEndpoint.TryGetValue(endpoint, out port);
        }

        public bool TryGetEdge(string edgeId, out ESGraphEdgeSnapshot edge)
        {
            return edgesById.TryGetValue(edgeId ?? string.Empty, out edge);
        }

        public IReadOnlyList<ESGraphRouteSnapshot> GetOutgoingRoutes(string nodeId, string portKey)
        {
            return GetOutgoingRoutes(new ESGraphEndpointKey(nodeId, portKey));
        }

        public IReadOnlyList<ESGraphRouteSnapshot> GetOutgoingRoutes(ESGraphEndpointKey endpoint)
        {
            return outgoingRoutesByEndpoint.TryGetValue(endpoint,
                out IReadOnlyList<ESGraphRouteSnapshot> result)
                ? result : Array.Empty<ESGraphRouteSnapshot>();
        }

        public IReadOnlyList<ESGraphRouteSnapshot> GetIncomingRoutes(string nodeId, string portKey)
        {
            return GetIncomingRoutes(new ESGraphEndpointKey(nodeId, portKey));
        }

        public IReadOnlyList<ESGraphRouteSnapshot> GetIncomingRoutes(ESGraphEndpointKey endpoint)
        {
            return incomingRoutesByEndpoint.TryGetValue(endpoint,
                out IReadOnlyList<ESGraphRouteSnapshot> result)
                ? result : Array.Empty<ESGraphRouteSnapshot>();
        }

        public IReadOnlyList<ESGraphRouteSnapshot> GetOutgoingRoutesByPortId(string portId)
            => outgoingRoutesByPort.TryGetValue(portId ?? string.Empty,
                out IReadOnlyList<ESGraphRouteSnapshot> result) ? result : Array.Empty<ESGraphRouteSnapshot>();

        public IReadOnlyList<ESGraphRouteSnapshot> GetIncomingRoutesByPortId(string portId)
            => incomingRoutesByPort.TryGetValue(portId ?? string.Empty,
                out IReadOnlyList<ESGraphRouteSnapshot> result) ? result : Array.Empty<ESGraphRouteSnapshot>();

        private static void AddRoute(Dictionary<string, List<ESGraphRouteSnapshot>> index,
            string key, ESGraphRouteSnapshot route)
        {
            if (!index.TryGetValue(key, out List<ESGraphRouteSnapshot> routes))
            {
                routes = new List<ESGraphRouteSnapshot>();
                index.Add(key, routes);
            }
            routes.Add(route);
        }

        private static void AddRoute(Dictionary<ESGraphEndpointKey, List<ESGraphRouteSnapshot>> index,
            ESGraphEndpointKey key, ESGraphRouteSnapshot route)
        {
            if (!index.TryGetValue(key, out List<ESGraphRouteSnapshot> routes))
            {
                routes = new List<ESGraphRouteSnapshot>();
                index.Add(key, routes);
            }
            routes.Add(route);
        }

        private static Dictionary<string, IReadOnlyList<ESGraphRouteSnapshot>> FreezeRouteIndex(
            Dictionary<string, List<ESGraphRouteSnapshot>> source)
        {
            var frozen = new Dictionary<string, IReadOnlyList<ESGraphRouteSnapshot>>(
                source.Count, StringComparer.Ordinal);
            foreach (KeyValuePair<string, List<ESGraphRouteSnapshot>> pair in source)
            {
                pair.Value.Sort(CompareRoutes);
                frozen.Add(pair.Key, pair.Value.ToArray());
            }
            return frozen;
        }

        private static Dictionary<ESGraphEndpointKey, IReadOnlyList<ESGraphRouteSnapshot>> FreezeRouteIndex(
            Dictionary<ESGraphEndpointKey, List<ESGraphRouteSnapshot>> source)
        {
            var frozen = new Dictionary<ESGraphEndpointKey, IReadOnlyList<ESGraphRouteSnapshot>>(
                source.Count);
            foreach (KeyValuePair<ESGraphEndpointKey, List<ESGraphRouteSnapshot>> pair in source)
            {
                pair.Value.Sort(CompareRoutes);
                frozen.Add(pair.Key, pair.Value.ToArray());
            }
            return frozen;
        }

        private static int CompareRoutes(ESGraphRouteSnapshot left, ESGraphRouteSnapshot right)
        {
            int order = (left?.Order ?? int.MaxValue).CompareTo(right?.Order ?? int.MaxValue);
            return order != 0 ? order : string.CompareOrdinal(left?.EdgeId, right?.EdgeId);
        }
    }

    public static class ESGraphSnapshotBaker
    {
        public static bool TryBake(GraphAsset asset, out ESBakedGraphSnapshot snapshot,
            out List<ESGraphValidationIssue> issues)
        {
            return TryBakeInternal(asset, true, out snapshot, out issues);
        }

        /// <summary>
        /// Editor authoring registry uses this entry after it has already run the complete
        /// model/domain validation pipeline. It prevents the generic model validation from
        /// being allocated and executed a second time during the same bake transaction.
        /// The public overload above remains self-validating for all other callers.
        /// </summary>
        internal static bool TryBakeValidated(GraphAsset asset,
            out ESBakedGraphSnapshot snapshot, out List<ESGraphValidationIssue> issues)
        {
            return TryBakeInternal(asset, false, out snapshot, out issues);
        }

        private static bool TryBakeInternal(GraphAsset asset, bool validate,
            out ESBakedGraphSnapshot snapshot, out List<ESGraphValidationIssue> issues)
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

            issues = validate ? asset.ValidateGraph() : new List<ESGraphValidationIssue>();
            if (validate)
            {
                for (int i = 0; i < issues.Count; i++)
                {
                    ESGraphValidationIssue issue = issues[i];
                    if (issue != null && issue.severity == ESGraphValidationSeverity.Error)
                        return false;
                }
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
                        WriteString(writer, port.meaning);
                        WriteString(writer, port.valueTypeId);
                        writer.Write((byte)port.direction);
                        writer.Write((byte)port.capacity);
                        writer.Write((byte)port.aggregation);
                    }
                }

                writer.Write(edges.Count);
                for (int i = 0; i < edges.Count; i++)
                {
                    ESGraphEdgeRecord edge = edges[i];
                    WriteString(writer, edge.edgeId);
                    WriteString(writer, edge.outputPortId);
                    WriteString(writer, edge.inputPortId);
                    writer.Write(edge.order);
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
