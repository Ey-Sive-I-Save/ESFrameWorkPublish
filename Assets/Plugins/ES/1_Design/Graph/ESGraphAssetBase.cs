using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// Stable semantic identity of one endpoint inside a graph. PortId identifies the serialized
    /// authoring record; NodeId + PortKey identifies what the endpoint means to bakers and runners.
    /// </summary>
    public readonly struct ESGraphEndpointKey : IEquatable<ESGraphEndpointKey>
    {
        public string NodeId { get; }
        public string PortKey { get; }
        public bool IsValid => ESGraphIdentity.IsValid(NodeId)
            && ESGraphStableIdUtility.IsValid(PortKey);

        public ESGraphEndpointKey(string nodeId, string portKey)
        {
            NodeId = nodeId ?? string.Empty;
            PortKey = portKey ?? string.Empty;
        }

        public bool Equals(ESGraphEndpointKey other)
            => string.Equals(NodeId, other.NodeId, StringComparison.Ordinal)
                && string.Equals(PortKey, other.PortKey, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is ESGraphEndpointKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return ((NodeId != null ? StringComparer.Ordinal.GetHashCode(NodeId) : 0) * 397)
                    ^ (PortKey != null ? StringComparer.Ordinal.GetHashCode(PortKey) : 0);
            }
        }

        public override string ToString() => (NodeId ?? string.Empty) + "/" + (PortKey ?? string.Empty);
    }

    public static class ESGraphEndpointRules
    {
        public const int MaxMeaningLength = 256;

        public static string ResolveMeaning(string meaning, string name, string stableKey)
        {
            string result = !string.IsNullOrWhiteSpace(meaning) ? meaning
                : !string.IsNullOrWhiteSpace(name) ? name : stableKey;
            return result?.Trim() ?? string.Empty;
        }

        public static bool IsValidMeaning(string meaning)
            => !string.IsNullOrWhiteSpace(meaning)
                && meaning.Trim().Length <= MaxMeaningLength;
    }

    /// <summary>Stable keys shared by the built-in flow node definitions and their consumers.</summary>
    public static class ESGraphBuiltInPortKeys
    {
        public const string Input = "flow.input";
        public const string Output = "flow.output";
        public const string True = "flow.true";
        public const string False = "flow.false";
        public const string Option = "flow.option";
    }

    public enum ESGraphPortDirection : byte
    {
        Input,
        Output
    }

    public enum ESGraphPortCapacity : byte
    {
        Single,
        Multi
    }

    /// <summary>
    /// Defines how an input endpoint interprets multiple incoming routes. Auto keeps the
    /// serialized contract backward compatible: output endpoints resolve to Single; input
    /// endpoints resolve to Single or Ordered from their capacity. Aggregation is applied at the
    /// target endpoint, while the source value remains one independently named value.
    /// </summary>
    public enum ESGraphPortAggregation : byte
    {
        Auto,
        Single,
        Ordered,
        Named
    }

    public static class ESGraphPortAggregationRules
    {
        public static ESGraphPortAggregation Resolve(ESGraphPortDirection direction,
            ESGraphPortCapacity capacity, ESGraphPortAggregation aggregation)
        {
            if (aggregation != ESGraphPortAggregation.Auto)
                return aggregation;
            if (direction == ESGraphPortDirection.Output)
                return ESGraphPortAggregation.Single;
            return Resolve(capacity, aggregation);
        }

        public static ESGraphPortAggregation Resolve(ESGraphPortCapacity capacity,
            ESGraphPortAggregation aggregation)
        {
            if (aggregation != ESGraphPortAggregation.Auto)
                return aggregation;
            return capacity == ESGraphPortCapacity.Single
                ? ESGraphPortAggregation.Single : ESGraphPortAggregation.Ordered;
        }

        public static bool IsCompatible(ESGraphPortDirection direction,
            ESGraphPortCapacity capacity, ESGraphPortAggregation aggregation)
        {
            if (!Enum.IsDefined(typeof(ESGraphPortDirection), direction)
                || !Enum.IsDefined(typeof(ESGraphPortCapacity), capacity)
                || !Enum.IsDefined(typeof(ESGraphPortAggregation), aggregation))
                return false;
            ESGraphPortAggregation resolved = Resolve(direction, capacity, aggregation);
            if (direction == ESGraphPortDirection.Output)
                return resolved == ESGraphPortAggregation.Single;
            return capacity != ESGraphPortCapacity.Single
                || resolved == ESGraphPortAggregation.Single;
        }
    }

    public enum ESGraphValidationSeverity : byte
    {
        Info,
        Warning,
        Error
    }

    [Serializable]
    public sealed class ESGraphValidationIssue
    {
        public ESGraphValidationSeverity severity;
        public string code;
        public string message;
        public string elementId;
        /// <summary>
        /// True only when the issue describes a quality risk that still leaves a complete,
        /// structurally valid execution contract. Authorization and identity failures must remain false.
        /// </summary>
        public bool canForceContinue { get; private set; }

        public static ESGraphValidationIssue Error(string code, string message, string elementId = null,
            bool canForceContinue = false)
        {
            return new ESGraphValidationIssue
            {
                severity = ESGraphValidationSeverity.Error,
                code = code,
                message = message,
                elementId = elementId,
                canForceContinue = canForceContinue
            };
        }

        public static ESGraphValidationIssue Warning(string code, string message, string elementId = null)
        {
            return new ESGraphValidationIssue
            {
                severity = ESGraphValidationSeverity.Warning,
                code = code,
                message = message,
                elementId = elementId
            };
        }
    }

    [Serializable]
    public sealed class ESGraphPortDefinition
    {
        public string name;
        public string stableKey;
        public string meaning;
        public string valueTypeId = ESGraphPortValueIds.Flow;
        public ESGraphPortDirection direction;
        public ESGraphPortCapacity capacity = ESGraphPortCapacity.Single;
        public ESGraphPortAggregation aggregation = ESGraphPortAggregation.Auto;
        public ESGraphPortValueKind ValueKind => ESGraphPortValueCatalog.GetKind(valueTypeId);

        public ESGraphPortDefinition()
        {
        }

        public ESGraphPortDefinition(string name, string stableKey, ESGraphPortDirection direction,
            ESGraphPortCapacity capacity = ESGraphPortCapacity.Single,
            string valueTypeId = ESGraphPortValueIds.Flow,
            ESGraphPortAggregation aggregation = ESGraphPortAggregation.Auto,
            string meaning = null)
        {
            this.name = name;
            this.stableKey = stableKey;
            this.meaning = ESGraphEndpointRules.ResolveMeaning(meaning, name, stableKey);
            this.direction = direction;
            this.capacity = capacity;
            this.valueTypeId = valueTypeId;
            this.aggregation = aggregation;
        }

        public ESGraphPortDefinition(string name, string stableKey, ESGraphPortDirection direction,
            ESGraphPortCapacity capacity, ESGraphPortValueKind valueKind, string customValueTypeId = null,
            ESGraphPortAggregation aggregation = ESGraphPortAggregation.Auto,
            string meaning = null)
            : this(name, stableKey, direction, capacity,
                ESGraphPortValueCatalog.GetStableId(valueKind, customValueTypeId), aggregation, meaning)
        {
        }
    }

    [Serializable]
    public sealed class ESGraphPortRecord
    {
        public string portId;
        public string stableKey;
        public string name;
        public string meaning;
        public string valueTypeId = ESGraphPortValueIds.Flow;
        public ESGraphPortDirection direction;
        public ESGraphPortCapacity capacity = ESGraphPortCapacity.Single;
        public ESGraphPortAggregation aggregation = ESGraphPortAggregation.Auto;
        public ESGraphPortValueKind ValueKind => ESGraphPortValueCatalog.GetKind(valueTypeId);

#if UNITY_EDITOR
        internal ESGraphPortRecord CloneWithNewIdentity(Dictionary<string, string> portIdMap)
        {
            string newId = ESGraphIdentity.NewId();
            if (!string.IsNullOrEmpty(portId))
                portIdMap[portId] = newId;
            return new ESGraphPortRecord
            {
                portId = newId,
                stableKey = stableKey,
                name = name,
                meaning = meaning,
                valueTypeId = valueTypeId,
                direction = direction,
                capacity = capacity,
                aggregation = aggregation
            };
        }
#endif
    }

    [Serializable]
    public sealed class ESGraphNodeRecord
    {
        public string nodeId;
        public string typeId;
        public int version = 1;
        public string title;
        [TextArea(2, 10)] public string payloadJson;
        public Vector2 position;
        public List<ESGraphPortRecord> ports = new List<ESGraphPortRecord>();
        public ESGraphBuiltInNodeKind BuiltInKind => ESGraphNodeTypeCatalog.GetKind(typeId);
        public ESGraphNodeTypeKey TypeKey => ESGraphNodeTypeKey.Parse(typeId);

        /// <summary>
        /// Finds the endpoint with the exact stable key owned by this node. Consumers use this
        /// identity instead of relying on port order or selecting the first matching direction.
        /// </summary>
        public bool TryGetPort(string portKey, out ESGraphPortRecord port)
        {
            port = null;
            if (string.IsNullOrWhiteSpace(portKey) || ports == null)
                return false;
            for (int i = 0; i < ports.Count; i++)
            {
                ESGraphPortRecord candidate = ports[i];
                if (candidate != null
                    && string.Equals(candidate.stableKey, portKey, StringComparison.Ordinal))
                {
                    if (port != null)
                    {
                        port = null;
                        return false;
                    }
                    port = candidate;
                }
            }
            return port != null;
        }

#if UNITY_EDITOR
        internal ESGraphNodeRecord CloneWithNewIdentity(Vector2 offset, Dictionary<string, string> portIdMap)
        {
            ESGraphNodeRecord clone = new ESGraphNodeRecord
            {
                nodeId = ESGraphIdentity.NewId(),
                typeId = typeId,
                version = version,
                title = title,
                payloadJson = payloadJson,
                position = position + offset,
                ports = new List<ESGraphPortRecord>()
            };

            if (ports != null)
            {
                for (int i = 0; i < ports.Count; i++)
                {
                    ESGraphPortRecord port = ports[i];
                    if (port != null)
                        clone.ports.Add(port.CloneWithNewIdentity(portIdMap));
                }
            }

            return clone;
        }
#endif
    }

    [Serializable]
    public sealed class ESGraphEdgeRecord
    {
        public string edgeId;
        public string outputPortId;
        public string inputPortId;
        [Min(0)] public int order;
    }

    /// <summary>
    /// Read-only topology facts for one stable endpoint. Capacity describes whether this one
    /// endpoint accepts multiple edges; it never describes how many endpoints its node owns.
    /// </summary>
    public readonly struct ESGraphEndpointTopology
    {
        public string PortId { get; }
        public string StableKey { get; }
        public string Meaning { get; }
        public ESGraphPortDirection Direction { get; }
        public ESGraphPortCapacity Capacity { get; }
        public int ConnectionCount { get; }
        public bool IsStableIndependent { get; }

        internal ESGraphEndpointTopology(ESGraphPortRecord port, int connectionCount,
            bool isStableIndependent)
        {
            PortId = port?.portId ?? string.Empty;
            StableKey = port?.stableKey ?? string.Empty;
            Meaning = port?.meaning ?? string.Empty;
            Direction = port?.direction ?? ESGraphPortDirection.Input;
            Capacity = port?.capacity ?? ESGraphPortCapacity.Single;
            ConnectionCount = Math.Max(0, connectionCount);
            IsStableIndependent = isStableIndependent;
        }
    }

    /// <summary>
    /// Separates a node's independent stable endpoints from the number of edges connected to
    /// those endpoints. A node is multi-endpoint only when one direction owns at least two
    /// distinct PortId + StableKey + Direction + Meaning identities.
    /// </summary>
    public sealed class ESGraphNodeTopology
    {
        private static readonly ESGraphEndpointTopology[] EmptyEndpoints =
            Array.Empty<ESGraphEndpointTopology>();

        public static ESGraphNodeTopology Empty { get; } =
            new ESGraphNodeTopology(EmptyEndpoints, 0, 0, 0, 0);

        public IReadOnlyList<ESGraphEndpointTopology> Endpoints { get; }
        public int InputEndpointCount { get; }
        public int OutputEndpointCount { get; }
        public int TotalEndpointCount => InputEndpointCount + OutputEndpointCount;
        public int InputConnectionCount { get; }
        public int OutputConnectionCount { get; }
        public int TotalConnectionCount => InputConnectionCount + OutputConnectionCount;
        public int ConnectedEndpointCount { get; }
        public int MultiConnectionCapacityEndpointCount { get; }
        public int InvalidEndpointRecordCount { get; }
        public bool HasMultipleInputEndpoints => InputEndpointCount >= 2;
        public bool HasMultipleOutputEndpoints => OutputEndpointCount >= 2;
        public bool IsMultiEndpointNode => HasMultipleInputEndpoints || HasMultipleOutputEndpoints;

        internal ESGraphNodeTopology(ESGraphEndpointTopology[] endpoints,
            int inputEndpointCount, int outputEndpointCount,
            int inputConnectionCount, int outputConnectionCount)
        {
            Endpoints = endpoints ?? EmptyEndpoints;
            InputEndpointCount = Math.Max(0, inputEndpointCount);
            OutputEndpointCount = Math.Max(0, outputEndpointCount);
            InputConnectionCount = Math.Max(0, inputConnectionCount);
            OutputConnectionCount = Math.Max(0, outputConnectionCount);
            ConnectedEndpointCount = Endpoints.Count(endpoint =>
                endpoint.IsStableIndependent && endpoint.ConnectionCount > 0);
            MultiConnectionCapacityEndpointCount = Endpoints.Count(endpoint =>
                endpoint.IsStableIndependent
                && endpoint.Capacity == ESGraphPortCapacity.Multi);
            InvalidEndpointRecordCount = Endpoints.Count(endpoint => !endpoint.IsStableIndependent);
        }
    }

    public static class ESGraphTopologyAnalyzer
    {
        private readonly struct EndpointIdentity : IEquatable<EndpointIdentity>
        {
            private readonly string portId;
            private readonly string stableKey;
            private readonly string meaning;
            private readonly ESGraphPortDirection direction;

            public EndpointIdentity(ESGraphPortRecord port)
            {
                portId = port?.portId ?? string.Empty;
                stableKey = port?.stableKey ?? string.Empty;
                meaning = port?.meaning ?? string.Empty;
                direction = port?.direction ?? ESGraphPortDirection.Input;
            }

            public bool Equals(EndpointIdentity other)
                => direction == other.direction
                    && string.Equals(portId, other.portId, StringComparison.Ordinal)
                    && string.Equals(stableKey, other.stableKey, StringComparison.Ordinal)
                    && string.Equals(meaning, other.meaning, StringComparison.Ordinal);

            public override bool Equals(object obj)
                => obj is EndpointIdentity other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = (int)direction;
                    hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(portId);
                    hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(stableKey);
                    return (hash * 397) ^ StringComparer.Ordinal.GetHashCode(meaning);
                }
            }
        }

        public static ESGraphNodeTopology Analyze(ESGraphNodeRecord node,
            IReadOnlyList<ESGraphNodeRecord> graphNodes,
            IReadOnlyList<ESGraphEdgeRecord> edges)
        {
            if (node?.ports == null || node.ports.Count == 0)
                return ESGraphNodeTopology.Empty;

            var connectionCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            if (edges != null)
            {
                for (int i = 0; i < edges.Count; i++)
                {
                    ESGraphEdgeRecord edge = edges[i];
                    if (edge == null)
                        continue;
                    Increment(connectionCounts, edge.inputPortId);
                    Increment(connectionCounts, edge.outputPortId);
                }
            }

            var endpoints = new List<ESGraphEndpointTopology>(node.ports.Count);
            var inputIdentities = new HashSet<EndpointIdentity>();
            var outputIdentities = new HashSet<EndpointIdentity>();
            var portIdCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            var stableKeyCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            if (graphNodes != null)
            {
                for (int nodeIndex = 0; nodeIndex < graphNodes.Count; nodeIndex++)
                {
                    ESGraphNodeRecord graphNode = graphNodes[nodeIndex];
                    if (graphNode?.ports == null)
                        continue;
                    for (int portIndex = 0; portIndex < graphNode.ports.Count; portIndex++)
                        Increment(portIdCounts, graphNode.ports[portIndex]?.portId);
                }
            }
            else
            {
                for (int i = 0; i < node.ports.Count; i++)
                    Increment(portIdCounts, node.ports[i]?.portId);
            }
            for (int i = 0; i < node.ports.Count; i++)
            {
                ESGraphPortRecord port = node.ports[i];
                if (port == null)
                    continue;
                Increment(stableKeyCounts, port.stableKey);
            }
            int inputConnections = 0;
            int outputConnections = 0;
            for (int i = 0; i < node.ports.Count; i++)
            {
                ESGraphPortRecord port = node.ports[i];
                if (port == null)
                    continue;
                connectionCounts.TryGetValue(port.portId ?? string.Empty, out int count);
                bool stableIndependent = ESGraphIdentity.IsValid(port.portId)
                    && ESGraphStableIdUtility.IsValid(port.stableKey)
                    && ESGraphEndpointRules.IsValidMeaning(port.meaning)
                    && Enum.IsDefined(typeof(ESGraphPortDirection), port.direction)
                    && portIdCounts.TryGetValue(port.portId, out int portIdCount)
                    && portIdCount == 1
                    && stableKeyCounts.TryGetValue(port.stableKey, out int stableKeyCount)
                    && stableKeyCount == 1;
                endpoints.Add(new ESGraphEndpointTopology(port, count, stableIndependent));
                if (!stableIndependent)
                    continue;
                if (port.direction == ESGraphPortDirection.Input)
                {
                    inputIdentities.Add(new EndpointIdentity(port));
                    inputConnections += count;
                }
                else
                {
                    outputIdentities.Add(new EndpointIdentity(port));
                    outputConnections += count;
                }
            }
            return new ESGraphNodeTopology(endpoints.ToArray(), inputIdentities.Count,
                outputIdentities.Count, inputConnections, outputConnections);
        }

        private static void Increment(Dictionary<string, int> counts, string portId)
        {
            if (string.IsNullOrEmpty(portId))
                return;
            counts.TryGetValue(portId, out int count);
            counts[portId] = count + 1;
        }
    }

    public static class ESGraphIdentity
    {
        public static string NewId()
        {
            return Guid.NewGuid().ToString("N");
        }

        public static bool IsValid(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length != 32)
                return false;
            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                bool digit = character >= '0' && character <= '9';
                bool lowerHex = character >= 'a' && character <= 'f';
                if (!digit && !lowerHex)
                    return false;
            }
            return true;
        }
    }

    /// <summary>
    /// Stable serialized Graph authority shared by concrete domain assets. GraphView is only an
    /// Editor projection; runtime consumers must use validated snapshots and domain-owned plans.
    /// </summary>
    public abstract class ESGraphAssetBase : ESSO
    {
        private readonly struct ConnectionEndpointKey : IEquatable<ConnectionEndpointKey>
        {
            private readonly string outputPortId;
            private readonly string inputPortId;

            public ConnectionEndpointKey(string outputPortId, string inputPortId)
            {
                this.outputPortId = outputPortId ?? string.Empty;
                this.inputPortId = inputPortId ?? string.Empty;
            }

            public bool Equals(ConnectionEndpointKey other)
            {
                return string.Equals(outputPortId, other.outputPortId, StringComparison.Ordinal)
                    && string.Equals(inputPortId, other.inputPortId, StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is ConnectionEndpointKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((outputPortId != null ? outputPortId.GetHashCode() : 0) * 397)
                        ^ (inputPortId != null ? inputPortId.GetHashCode() : 0);
                }
            }
        }

        private sealed class ConnectionValidationIndex
        {
            public readonly Dictionary<string, ESGraphNodeRecord> NodesById =
                new Dictionary<string, ESGraphNodeRecord>(StringComparer.Ordinal);
            public readonly Dictionary<string, ESGraphPortRecord> PortsById =
                new Dictionary<string, ESGraphPortRecord>(StringComparer.Ordinal);
            public readonly Dictionary<string, string> NodeIdsByPort =
                new Dictionary<string, string>(StringComparer.Ordinal);
            public readonly Dictionary<string, int> ConnectionCounts =
                new Dictionary<string, int>(StringComparer.Ordinal);
            public readonly HashSet<ConnectionEndpointKey> Endpoints =
                new HashSet<ConnectionEndpointKey>();
            public readonly Dictionary<string, List<string>> Outgoing =
                new Dictionary<string, List<string>>(StringComparer.Ordinal);
            public readonly Dictionary<string, List<string>> Incoming =
                new Dictionary<string, List<string>>(StringComparer.Ordinal);
        }

        public const int CurrentSchemaVersion = 4;
        public const int MinimumSupportedSchemaVersion = 1;

        [Min(1)] public int schemaVersion = CurrentSchemaVersion;
        [SerializeField] private string graphId = ESGraphIdentity.NewId();
        [SerializeField] private string originGraphId = string.Empty;
        public bool allowCycles;
        [SerializeField] private List<ESGraphNodeRecord> nodes = new List<ESGraphNodeRecord>();
        [SerializeField] private List<ESGraphEdgeRecord> edges = new List<ESGraphEdgeRecord>();

        public string GraphId => graphId ?? string.Empty;
        public string OriginGraphId => originGraphId ?? string.Empty;
        public abstract ESGraphDomainKey DomainKey { get; }
        public string DomainId => DomainKey.StableId;
        public ESGraphDomainKind DomainKind => ESGraphDomainCatalog.GetKind(DomainId);
        /// <summary>当前领域是否允许作者开启循环。领域禁止时，序列化字段即使残留为 true 也不会生效。</summary>
        public virtual bool CanEnableCycles => true;
        /// <summary>供连接、校验、快照和编辑器共同使用的最终循环策略。</summary>
        public bool AllowsCycles => CanEnableCycles && allowCycles;
        public IReadOnlyList<ESGraphNodeRecord> Nodes => nodes;
        public IReadOnlyList<ESGraphEdgeRecord> Edges => edges;

#if UNITY_EDITOR
        public bool EnsureGraphIdentity()
        {
            bool changed = false;
            if (!ESGraphIdentity.IsValid(graphId))
            {
                graphId = ESGraphIdentity.NewId();
                changed = true;
            }
            if (!string.IsNullOrEmpty(originGraphId) && !ESGraphIdentity.IsValid(originGraphId))
            {
                originGraphId = string.Empty;
                changed = true;
            }
            return changed;
        }

        /// <summary>
        /// Explicit, transactional Graph schema upgrade. It never changes stable graph, node,
        /// port or edge identities and it performs all checks before mutating serialized data.
        /// </summary>
        public bool TryUpgradeSchema(out bool changed, out string error)
        {
            EnsureCollections();
            changed = false;
            error = string.Empty;
            if (schemaVersion == CurrentSchemaVersion)
                return true;
            if (schemaVersion < MinimumSupportedSchemaVersion
                || schemaVersion > CurrentSchemaVersion)
            {
                error = "不支持从 Graph Schema " + schemaVersion + " 升级到 "
                    + CurrentSchemaVersion + "。";
                return false;
            }

            var meanings = new List<KeyValuePair<ESGraphPortRecord, string>>();
            for (int n = 0; n < nodes.Count; n++)
            {
                ESGraphNodeRecord node = nodes[n];
                if (node == null || node.ports == null)
                {
                    error = "图包含空节点或端口集合，不能安全升级 Schema。";
                    return false;
                }
                for (int p = 0; p < node.ports.Count; p++)
                {
                    ESGraphPortRecord port = node.ports[p];
                    if (port == null)
                    {
                        error = "图包含空端口，不能安全升级 Schema。";
                        return false;
                    }
                    string meaning = ESGraphEndpointRules.ResolveMeaning(port.meaning,
                        port.name, port.stableKey);
                    if (!ESGraphEndpointRules.IsValidMeaning(meaning))
                    {
                        error = "端点缺少可迁移的用途：" + (port.portId ?? string.Empty);
                        return false;
                    }
                    meanings.Add(new KeyValuePair<ESGraphPortRecord, string>(port, meaning));
                }
            }

            var orderedEdges = new List<ESGraphEdgeRecord>(edges.Count);
            var edgeIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < edges.Count; i++)
            {
                ESGraphEdgeRecord edge = edges[i];
                if (edge == null || !ESGraphIdentity.IsValid(edge.edgeId)
                    || !edgeIds.Add(edge.edgeId)
                    || string.IsNullOrWhiteSpace(edge.outputPortId)
                    || string.IsNullOrWhiteSpace(edge.inputPortId))
                {
                    error = "图包含无法确定顺序的空连线、重复身份或缺失端点，不能安全升级 Schema。";
                    return false;
                }
                orderedEdges.Add(edge);
            }
            orderedEdges.Sort((left, right) => string.CompareOrdinal(left.edgeId, right.edgeId));

            for (int i = 0; i < meanings.Count; i++)
                meanings[i].Key.meaning = meanings[i].Value;
            for (int i = 0; i < orderedEdges.Count; i++)
                orderedEdges[i].order = i;
            schemaVersion = CurrentSchemaVersion;
            changed = true;
            return true;
        }

        public void InitializeAsIndependentCopyOf(string sourceGraphId)
        {
            graphId = ESGraphIdentity.NewId();
            originGraphId = ESGraphIdentity.IsValid(sourceGraphId) ? sourceGraphId : string.Empty;
        }

        public ESGraphNodeRecord AddNode(string typeId, string title, Vector2 position,
            IReadOnlyList<ESGraphPortDefinition> portDefinitions)
        {
            EnsureCollections();
            if (string.IsNullOrWhiteSpace(typeId) || !ESGraphStableIdUtility.IsValid(typeId.Trim()))
                throw new ArgumentException("Graph node TypeId must be a valid stable ID.", nameof(typeId));

            ESGraphNodeRecord node = new ESGraphNodeRecord
            {
                nodeId = ESGraphIdentity.NewId(),
                typeId = typeId.Trim(),
                version = 1,
                title = string.IsNullOrWhiteSpace(title) ? typeId.Trim() : title.Trim(),
                position = position,
                ports = new List<ESGraphPortRecord>()
            };

            if (portDefinitions != null)
            {
                HashSet<string> stableKeys = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < portDefinitions.Count; i++)
                {
                    ESGraphPortDefinition definition = portDefinitions[i];
                    if (definition == null)
                        continue;
                    if (string.IsNullOrWhiteSpace(definition.stableKey))
                        throw new InvalidOperationException(
                            "每个 Graph 端点都必须声明独立的 StableKey 语义。");
                    string stableKey = definition.stableKey.Trim();
                    if (!ESGraphStableIdUtility.IsValid(stableKey)
                        || !Enum.IsDefined(typeof(ESGraphPortDirection), definition.direction)
                        || !Enum.IsDefined(typeof(ESGraphPortCapacity), definition.capacity)
                        || !Enum.IsDefined(typeof(ESGraphPortAggregation), definition.aggregation)
                        || !ESGraphPortValueCatalog.IsValidStableId(definition.valueTypeId))
                        throw new InvalidOperationException(
                            "Graph 端点 StableKey、方向、容量、聚合模式和 ValueTypeId 必须有效。");
                    if (!stableKeys.Add(stableKey))
                        throw new InvalidOperationException("Duplicate port stable key: " + stableKey);
                    string meaning = ESGraphEndpointRules.ResolveMeaning(definition.meaning,
                        definition.name, stableKey);
                    if (!ESGraphEndpointRules.IsValidMeaning(meaning))
                        throw new InvalidOperationException(
                            "每个 Graph 端点都必须声明简短、明确的用途：" + stableKey);
                    if (!ESGraphPortAggregationRules.IsCompatible(definition.direction,
                            definition.capacity, definition.aggregation))
                        throw new InvalidOperationException(
                            "端点方向、容量与聚合模式不兼容：" + stableKey);
                    node.ports.Add(new ESGraphPortRecord
                    {
                        portId = ESGraphIdentity.NewId(),
                        stableKey = stableKey,
                        name = string.IsNullOrWhiteSpace(definition.name) ? stableKey : definition.name.Trim(),
                        meaning = meaning,
                        valueTypeId = string.IsNullOrWhiteSpace(definition.valueTypeId)
                            ? ESGraphPortValueIds.Flow
                            : definition.valueTypeId.Trim(),
                        direction = definition.direction,
                        capacity = definition.capacity,
                        aggregation = definition.aggregation
                    });
                }
            }

            nodes.Add(node);
            return node;
        }

        public ESGraphNodeRecord AddNode(ESGraphNodeTypeKey type, string title, Vector2 position,
            IReadOnlyList<ESGraphPortDefinition> portDefinitions)
        {
            if (!type.IsValid)
                throw new ArgumentException("节点类型稳定标识非法。", nameof(type));
            return AddNode(type.StableId, title, position, portDefinitions);
        }

        public bool RemoveNode(string nodeId)
        {
            EnsureCollections();
            ESGraphNodeRecord node = FindNode(nodeId);
            if (node == null)
                return false;

            HashSet<string> portIds = new HashSet<string>(StringComparer.Ordinal);
            if (node.ports != null)
            {
                for (int i = 0; i < node.ports.Count; i++)
                {
                    if (node.ports[i] != null && !string.IsNullOrEmpty(node.ports[i].portId))
                        portIds.Add(node.ports[i].portId);
                }
            }

            for (int i = edges.Count - 1; i >= 0; i--)
            {
                ESGraphEdgeRecord edge = edges[i];
                if (edge == null || portIds.Contains(edge.outputPortId) || portIds.Contains(edge.inputPortId))
                    edges.RemoveAt(i);
            }

            return nodes.Remove(node);
        }

        public bool RemoveEdge(string edgeId)
        {
            EnsureCollections();
            for (int i = 0; i < edges.Count; i++)
            {
                ESGraphEdgeRecord edge = edges[i];
                if (edge != null && string.Equals(edge.edgeId, edgeId, StringComparison.Ordinal))
                {
                    edges.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        public bool SetNodePosition(string nodeId, Vector2 position)
        {
            ESGraphNodeRecord node = FindNode(nodeId);
            if (node == null || node.position == position)
                return false;
            node.position = position;
            return true;
        }

        public bool UpdateNode(string nodeId, string typeId, int version, string title, string payloadJson, out string error)
        {
            error = null;
            ESGraphNodeRecord node = FindNode(nodeId);
            if (node == null)
            {
                error = "节点不存在。";
                return false;
            }
            if (string.IsNullOrWhiteSpace(typeId)
                || !ESGraphStableIdUtility.IsValid(typeId.Trim()) || version < 1)
            {
                error = "TypeId 不能为空，节点版本必须大于 0。";
                return false;
            }
            node.typeId = typeId.Trim();
            node.version = version;
            node.title = string.IsNullOrWhiteSpace(title) ? node.typeId : title.Trim();
            node.payloadJson = payloadJson ?? string.Empty;
            return true;
        }

        public bool UpdateNode(string nodeId, ESGraphNodeTypeKey nodeType, int version, string title,
            string payloadJson, out string error)
        {
            if (!nodeType.IsValid)
            {
                error = "节点类型稳定标识非法。";
                return false;
            }
            return UpdateNode(nodeId, nodeType.StableId, version, title, payloadJson, out error);
        }

        public ESGraphPortRecord AddPort(string nodeId, ESGraphPortDefinition definition, out string error)
        {
            if (!CanAddPort(nodeId, definition, out error))
                return null;
            ESGraphNodeRecord node = FindNode(nodeId);
            if (node.ports == null)
                node.ports = new List<ESGraphPortRecord>();
            string stableKey = definition.stableKey?.Trim();
            string meaning = ESGraphEndpointRules.ResolveMeaning(definition.meaning,
                definition.name, stableKey);
            ESGraphPortRecord port = new ESGraphPortRecord
            {
                portId = ESGraphIdentity.NewId(),
                stableKey = stableKey,
                name = string.IsNullOrWhiteSpace(definition.name) ? stableKey : definition.name.Trim(),
                meaning = meaning,
                valueTypeId = definition.valueTypeId.Trim(),
                direction = definition.direction,
                capacity = definition.capacity,
                aggregation = definition.aggregation
            };
            node.ports.Add(port);
            return port;
        }

        public bool CanAddPort(string nodeId, ESGraphPortDefinition definition, out string error)
        {
            error = null;
            ESGraphNodeRecord node = FindNode(nodeId);
            if (node == null || definition == null)
            {
                error = "节点或端口定义不存在。";
                return false;
            }
            string stableKey = definition.stableKey?.Trim();
            if (string.IsNullOrEmpty(stableKey)
                || !ESGraphStableIdUtility.IsValid(stableKey)
                || !ESGraphEndpointRules.IsValidMeaning(ESGraphEndpointRules.ResolveMeaning(
                    definition.meaning, definition.name, stableKey))
                || !ESGraphPortValueCatalog.IsValidStableId(definition.valueTypeId)
                || !Enum.IsDefined(typeof(ESGraphPortDirection), definition.direction)
                || !Enum.IsDefined(typeof(ESGraphPortCapacity), definition.capacity)
                || !Enum.IsDefined(typeof(ESGraphPortAggregation), definition.aggregation))
            {
                error = "端点稳定名称、用途、数据类型、方向、容量和聚合模式必须有效。";
                return false;
            }
            if (!ESGraphPortAggregationRules.IsCompatible(definition.direction,
                    definition.capacity, definition.aggregation))
            {
                error = "端点方向、容量与聚合模式不兼容。";
                return false;
            }
            if (node.ports == null)
                return true;
            for (int i = 0; i < node.ports.Count; i++)
            {
                if (node.ports[i] != null && string.Equals(node.ports[i].stableKey, stableKey, StringComparison.Ordinal))
                {
                    error = "节点内 Port StableKey 重复：" + stableKey;
                    return false;
                }
            }
            return true;
        }

        public bool UpdatePort(string portId, string stableKey, string name, string meaning,
            string valueTypeId,
            ESGraphPortDirection direction, ESGraphPortCapacity capacity,
            ESGraphPortAggregation aggregation, out string error)
        {
            if (!CanUpdatePort(portId, stableKey, meaning, valueTypeId, direction, capacity,
                    aggregation, out error))
                return false;
            TryFindPort(portId, out _, out ESGraphPortRecord port);
            stableKey = stableKey?.Trim();
            valueTypeId = valueTypeId?.Trim();
            port.stableKey = stableKey;
            port.name = string.IsNullOrWhiteSpace(name) ? stableKey : name.Trim();
            port.meaning = meaning.Trim();
            port.valueTypeId = valueTypeId;
            port.direction = direction;
            port.capacity = capacity;
            port.aggregation = aggregation;
            return true;
        }

        public bool CanUpdatePort(string portId, string stableKey, string meaning, string valueTypeId,
            ESGraphPortDirection direction, ESGraphPortCapacity capacity,
            ESGraphPortAggregation aggregation, out string error)
        {
            error = null;
            if (!TryFindPort(portId, out ESGraphNodeRecord node, out ESGraphPortRecord port))
            {
                error = "端口不存在。";
                return false;
            }
            stableKey = stableKey?.Trim();
            meaning = meaning?.Trim();
            valueTypeId = valueTypeId?.Trim();
            if (string.IsNullOrEmpty(stableKey) || !ESGraphStableIdUtility.IsValid(stableKey)
                || !ESGraphEndpointRules.IsValidMeaning(meaning)
                || !ESGraphPortValueCatalog.IsValidStableId(valueTypeId)
                || !Enum.IsDefined(typeof(ESGraphPortDirection), direction)
                || !Enum.IsDefined(typeof(ESGraphPortCapacity), capacity)
                || !Enum.IsDefined(typeof(ESGraphPortAggregation), aggregation))
            {
                error = "端点稳定名称、用途、数据类型、方向、容量和聚合模式必须有效。";
                return false;
            }
            if (!ESGraphPortAggregationRules.IsCompatible(direction, capacity, aggregation))
            {
                error = "端点方向、容量与聚合模式不兼容。";
                return false;
            }
            for (int i = 0; i < node.ports.Count; i++)
            {
                ESGraphPortRecord sibling = node.ports[i];
                if (sibling != null && !ReferenceEquals(sibling, port)
                    && string.Equals(sibling.stableKey, stableKey, StringComparison.Ordinal))
                {
                    error = "节点内 Port StableKey 重复：" + stableKey;
                    return false;
                }
            }

            int connectionCount = 0;
            for (int i = 0; i < edges.Count; i++)
            {
                ESGraphEdgeRecord edge = edges[i];
                if (edge == null)
                    continue;
                bool usedAsOutput = string.Equals(edge.outputPortId, portId, StringComparison.Ordinal);
                bool usedAsInput = string.Equals(edge.inputPortId, portId, StringComparison.Ordinal);
                if (!usedAsOutput && !usedAsInput)
                    continue;
                connectionCount++;
                if ((usedAsOutput && direction != ESGraphPortDirection.Output)
                    || (usedAsInput && direction != ESGraphPortDirection.Input))
                {
                    error = "已连接端口不能改变为相反方向。";
                    return false;
                }
                string oppositeId = usedAsOutput ? edge.inputPortId : edge.outputPortId;
                if (!TryFindPort(oppositeId, out _, out ESGraphPortRecord opposite)
                    || !ArePortTypesCompatible(usedAsOutput ? valueTypeId : opposite.valueTypeId,
                        usedAsOutput ? opposite.valueTypeId : valueTypeId))
                {
                    error = "修改后的端口类型与现有连线不兼容。";
                    return false;
                }
            }
            if (capacity == ESGraphPortCapacity.Single && connectionCount > 1)
            {
                error = "该端口已有多条连线，不能改为 Single。";
                return false;
            }

            return true;
        }

        public bool RemovePort(string portId)
        {
            if (!TryFindPort(portId, out ESGraphNodeRecord node, out ESGraphPortRecord port))
                return false;
            for (int i = edges.Count - 1; i >= 0; i--)
            {
                ESGraphEdgeRecord edge = edges[i];
                if (edge == null || string.Equals(edge.outputPortId, portId, StringComparison.Ordinal)
                    || string.Equals(edge.inputPortId, portId, StringComparison.Ordinal))
                    edges.RemoveAt(i);
            }
            return node.ports.Remove(port);
        }

        public bool TryAddEdge(string outputPortId, string inputPortId, out ESGraphEdgeRecord edge, out string error)
        {
            edge = null;
            if (!CanConnect(outputPortId, inputPortId, null, out error))
                return false;

            edge = new ESGraphEdgeRecord
            {
                edgeId = ESGraphIdentity.NewId(),
                outputPortId = outputPortId,
                inputPortId = inputPortId,
                order = NextEdgeOrder()
            };
            edges.Add(edge);
            return true;
        }

        public bool TryReconnectEdge(string edgeId, string firstPortId, string secondPortId,
            out string error)
        {
            error = null;
            ESGraphEdgeRecord edge = FindEdge(edgeId);
            if (edge == null)
            {
                error = "需要重连的关系不存在。";
                return false;
            }

            if (!TryFindPort(firstPortId, out _, out ESGraphPortRecord firstPort)
                || !TryFindPort(secondPortId, out _, out ESGraphPortRecord secondPort))
            {
                error = "连接端口不存在。";
                return false;
            }

            string outputPortId;
            string inputPortId;
            if (firstPort.direction == ESGraphPortDirection.Output
                && secondPort.direction == ESGraphPortDirection.Input)
            {
                outputPortId = firstPort.portId;
                inputPortId = secondPort.portId;
            }
            else if (secondPort.direction == ESGraphPortDirection.Output
                && firstPort.direction == ESGraphPortDirection.Input)
            {
                outputPortId = secondPort.portId;
                inputPortId = firstPort.portId;
            }
            else
            {
                error = "连接必须从 Output 指向 Input。";
                return false;
            }

            if (string.Equals(edge.outputPortId, outputPortId, StringComparison.Ordinal)
                && string.Equals(edge.inputPortId, inputPortId, StringComparison.Ordinal))
                return false;

            if (!CanConnect(outputPortId, inputPortId, edgeId, out error))
                return false;

            edge.outputPortId = outputPortId;
            edge.inputPortId = inputPortId;
            return true;
        }

        /// <summary>
        /// Moves one relation inside its semantic order group. Ordered inputs use the target port;
        /// other multi-route relations use the source port. EdgeId and endpoints never change.
        /// </summary>
        public bool TryMoveEdge(string edgeId, int direction, out string error)
        {
            error = null;
            if (direction != -1 && direction != 1)
            {
                error = "关系顺序一次只能前移或后移一位。";
                return false;
            }
            if (!TryGetOrderedEdgeGroup(edgeId, out List<ESGraphEdgeRecord> group,
                    out int index, out error))
                return false;
            int targetIndex = index + direction;
            if (targetIndex < 0 || targetIndex >= group.Count)
            {
                error = direction < 0 ? "关系已经位于当前分组最前。" : "关系已经位于当前分组最后。";
                return false;
            }
            ESGraphEdgeRecord edge = group[index];
            ESGraphEdgeRecord target = group[targetIndex];
            int oldOrder = edge.order;
            edge.order = target.order;
            target.order = oldOrder;
            return true;
        }

        public bool TryGetEdgeOrderPosition(string edgeId, out int position, out int count)
        {
            position = -1;
            count = 0;
            if (!TryGetOrderedEdgeGroup(edgeId, out List<ESGraphEdgeRecord> group,
                    out int index, out _))
                return false;
            position = index;
            count = group.Count;
            return true;
        }

        private bool TryGetOrderedEdgeGroup(string edgeId, out List<ESGraphEdgeRecord> group,
            out int index, out string error)
        {
            group = null;
            index = -1;
            ESGraphEdgeRecord edge = FindEdge(edgeId);
            if (edge == null)
            {
                error = "需要调整的关系不存在。";
                return false;
            }
            bool orderByInput = TryFindPort(edge.inputPortId, out _, out ESGraphPortRecord input)
                && ESGraphPortAggregationRules.Resolve(input.direction, input.capacity,
                    input.aggregation) == ESGraphPortAggregation.Ordered;
            group = edges.Where(candidate => candidate != null && (orderByInput
                    ? string.Equals(candidate.inputPortId, edge.inputPortId, StringComparison.Ordinal)
                    : string.Equals(candidate.outputPortId, edge.outputPortId, StringComparison.Ordinal)))
                .OrderBy(candidate => candidate.order)
                .ThenBy(candidate => candidate.edgeId, StringComparer.Ordinal)
                .ToList();
            index = group.FindIndex(candidate => string.Equals(candidate.edgeId, edgeId,
                StringComparison.Ordinal));
            if (index < 0)
            {
                error = "关系不在可排序分组中。";
                return false;
            }
            error = null;
            return true;
        }

        private int NextEdgeOrder()
        {
            int maximum = -1;
            for (int i = 0; i < edges.Count; i++)
                if (edges[i] != null && edges[i].order > maximum)
                    maximum = edges[i].order;
            if (maximum < int.MaxValue)
                return maximum + 1;

            List<ESGraphEdgeRecord> ordered = edges.Where(edge => edge != null)
                .OrderBy(edge => edge.order).ThenBy(edge => edge.edgeId, StringComparer.Ordinal).ToList();
            for (int i = 0; i < ordered.Count; i++) ordered[i].order = i;
            return ordered.Count;
        }

        /// <summary>
        /// 一次性计算从指定端口新建关系时可用的端口集合。移动鼠标期间只读取结果；
        /// 松开鼠标后的最终提交仍由 <see cref="TryAddEdge"/> 执行权威终审。
        /// </summary>
        public bool TryBuildConnectionCompatibilityIndex(string startPortId,
            ISet<string> compatiblePortIds, out string error)
        {
            return TryBuildConnectionCompatibilityIndex(startPortId, null,
                compatiblePortIds, out error);
        }

        /// <summary>
        /// 一次性计算重连期间可用的端口集合。计算会忽略正在编辑的关系，且不会修改图模型；
        /// 松开鼠标后的最终提交仍由 <see cref="TryReconnectEdge"/> 执行权威终审。
        /// </summary>
        public bool TryBuildReconnectCompatibilityIndex(string edgeId, string fixedPortId,
            ISet<string> compatiblePortIds, out string error)
        {
            error = null;
            if (compatiblePortIds == null)
            {
                error = "兼容端口集合不能为空。";
                return false;
            }
            compatiblePortIds.Clear();
            ESGraphEdgeRecord edge = FindEdge(edgeId);
            if (edge == null)
            {
                error = "需要重连的关系不存在。";
                return false;
            }
            if (!string.Equals(edge.outputPortId, fixedPortId, StringComparison.Ordinal)
                && !string.Equals(edge.inputPortId, fixedPortId, StringComparison.Ordinal))
            {
                error = "固定端口不属于需要重连的关系。";
                return false;
            }

            return TryBuildConnectionCompatibilityIndex(fixedPortId, edgeId,
                compatiblePortIds, out error);
        }

        private bool TryBuildConnectionCompatibilityIndex(string fixedPortId, string ignoredEdgeId,
            ISet<string> compatiblePortIds, out string error)
        {
            error = null;
            if (compatiblePortIds == null)
            {
                error = "兼容端口集合不能为空。";
                return false;
            }
            compatiblePortIds.Clear();
            ConnectionValidationIndex index = BuildConnectionValidationIndex(ignoredEdgeId);
            if (!index.PortsById.TryGetValue(fixedPortId ?? string.Empty, out ESGraphPortRecord fixedPort)
                || !index.NodeIdsByPort.TryGetValue(fixedPortId ?? string.Empty, out string fixedNodeId))
            {
                error = "起始端口不存在。";
                return false;
            }

            HashSet<string> cycleBlockedNodeIds = null;
            if (!AllowsCycles)
            {
                cycleBlockedNodeIds = new HashSet<string>(StringComparer.Ordinal);
                Dictionary<string, List<string>> traversalIndex =
                    fixedPort.direction == ESGraphPortDirection.Output ? index.Incoming : index.Outgoing;
                CollectReachableNodes(traversalIndex, fixedNodeId, cycleBlockedNodeIds);
            }

            foreach (KeyValuePair<string, ESGraphPortRecord> pair in index.PortsById)
            {
                if (string.Equals(pair.Key, fixedPortId, StringComparison.Ordinal)
                    || !ValidateIndexedConnection(index, fixedPortId, pair.Key, false, out _))
                    continue;
                if (cycleBlockedNodeIds != null
                    && index.NodeIdsByPort.TryGetValue(pair.Key, out string candidateNodeId)
                    && cycleBlockedNodeIds.Contains(candidateNodeId))
                    continue;
                compatiblePortIds.Add(pair.Key);
            }
            return true;
        }
#endif

        public bool CanConnect(string firstPortId, string secondPortId, string ignoredEdgeId, out string error)
        {
            EnsureCollections();
            ConnectionValidationIndex index = BuildConnectionValidationIndex(ignoredEdgeId);
            return ValidateIndexedConnection(index, firstPortId, secondPortId, true, out error);
        }

        private bool ValidateIndexedConnection(ConnectionValidationIndex index, string firstPortId,
            string secondPortId, bool validateCycle, out string error)
        {
            error = null;
            if (!index.PortsById.TryGetValue(firstPortId ?? string.Empty, out ESGraphPortRecord firstPort)
                || !index.PortsById.TryGetValue(secondPortId ?? string.Empty, out ESGraphPortRecord secondPort)
                || !index.NodeIdsByPort.TryGetValue(firstPortId ?? string.Empty, out string firstNodeId)
                || !index.NodeIdsByPort.TryGetValue(secondPortId ?? string.Empty, out string secondNodeId))
            {
                error = "连接端口不存在。";
                return false;
            }

            string outputNodeId;
            string inputNodeId;
            ESGraphPortRecord output;
            ESGraphPortRecord input;
            if (firstPort.direction == ESGraphPortDirection.Output && secondPort.direction == ESGraphPortDirection.Input)
            {
                outputNodeId = firstNodeId;
                output = firstPort;
                inputNodeId = secondNodeId;
                input = secondPort;
            }
            else if (secondPort.direction == ESGraphPortDirection.Output && firstPort.direction == ESGraphPortDirection.Input)
            {
                outputNodeId = secondNodeId;
                output = secondPort;
                inputNodeId = firstNodeId;
                input = firstPort;
            }
            else
            {
                error = "连接必须从 Output 指向 Input。";
                return false;
            }

            if (!ArePortTypesCompatible(output.valueTypeId, input.valueTypeId))
            {
                error = "端口类型不兼容：" + output.valueTypeId + " → " + input.valueTypeId;
                return false;
            }
            if (!index.NodesById.TryGetValue(outputNodeId, out ESGraphNodeRecord outputNode)
                || !index.NodesById.TryGetValue(inputNodeId, out ESGraphNodeRecord inputNode))
            {
                error = "连接端口所属节点不存在。";
                return false;
            }
            if (!ValidateDomainConnection(outputNode, output, inputNode, input, out error))
                return false;

            if (index.Endpoints.Contains(new ConnectionEndpointKey(output.portId, input.portId)))
            {
                error = "不允许创建重复连线。";
                return false;
            }
            index.ConnectionCounts.TryGetValue(output.portId, out int outputConnections);
            index.ConnectionCounts.TryGetValue(input.portId, out int inputConnections);

            if (output.capacity == ESGraphPortCapacity.Single && outputConnections > 0)
            {
                error = "输出端口容量为 Single。";
                return false;
            }
            if (input.capacity == ESGraphPortCapacity.Single && inputConnections > 0)
            {
                error = "输入端口容量为 Single。";
                return false;
            }

            if (validateCycle && !AllowsCycles
                && CanReach(index.Outgoing, inputNodeId, outputNodeId))
            {
                error = "当前图禁止循环。";
                return false;
            }

            return true;
        }

        private ConnectionValidationIndex BuildConnectionValidationIndex(string ignoredEdgeId)
        {
            var index = new ConnectionValidationIndex();
            for (int i = 0; i < nodes.Count; i++)
            {
                ESGraphNodeRecord node = nodes[i];
                if (node == null || string.IsNullOrEmpty(node.nodeId))
                    continue;
                if (!index.NodesById.ContainsKey(node.nodeId))
                    index.NodesById.Add(node.nodeId, node);
                index.Outgoing[node.nodeId] = new List<string>();
                index.Incoming[node.nodeId] = new List<string>();
                if (node.ports == null)
                    continue;
                for (int p = 0; p < node.ports.Count; p++)
                {
                    ESGraphPortRecord port = node.ports[p];
                    if (port == null || string.IsNullOrEmpty(port.portId)
                        || index.PortsById.ContainsKey(port.portId))
                        continue;
                    index.PortsById.Add(port.portId, port);
                    index.NodeIdsByPort.Add(port.portId, node.nodeId);
                }
            }

            for (int i = 0; i < edges.Count; i++)
            {
                ESGraphEdgeRecord edge = edges[i];
                if (edge == null || string.Equals(edge.edgeId, ignoredEdgeId, StringComparison.Ordinal))
                    continue;
                index.Endpoints.Add(new ConnectionEndpointKey(edge.outputPortId, edge.inputPortId));
                Increment(index.ConnectionCounts, edge.outputPortId);
                Increment(index.ConnectionCounts, edge.inputPortId);
                if (!index.NodeIdsByPort.TryGetValue(edge.outputPortId ?? string.Empty, out string outputNodeId)
                    || !index.NodeIdsByPort.TryGetValue(edge.inputPortId ?? string.Empty, out string inputNodeId))
                    continue;
                index.Outgoing[outputNodeId].Add(inputNodeId);
                index.Incoming[inputNodeId].Add(outputNodeId);
            }
            return index;
        }

        /// <summary>
        /// 领域可在类型、容量、重复和循环规则之外收紧合法节点关系。
        /// 批量兼容索引与最终提交均调用此门禁，实现必须只读且不得分配全图索引。
        /// </summary>
        protected virtual bool ValidateDomainConnection(ESGraphNodeRecord outputNode,
            ESGraphPortRecord outputPort, ESGraphNodeRecord inputNode,
            ESGraphPortRecord inputPort, out string error)
        {
            error = null;
            return true;
        }

        private static bool CanReach(Dictionary<string, List<string>> adjacency, string startNodeId,
            string targetNodeId)
        {
            var visited = new HashSet<string>(StringComparer.Ordinal);
            var stack = new Stack<string>();
            stack.Push(startNodeId);
            while (stack.Count > 0)
            {
                string current = stack.Pop();
                if (!visited.Add(current))
                    continue;
                if (string.Equals(current, targetNodeId, StringComparison.Ordinal))
                    return true;
                if (!adjacency.TryGetValue(current, out List<string> next))
                    continue;
                for (int i = 0; i < next.Count; i++)
                    stack.Push(next[i]);
            }
            return false;
        }

        private static void CollectReachableNodes(Dictionary<string, List<string>> adjacency,
            string startNodeId, ISet<string> result)
        {
            var stack = new Stack<string>();
            stack.Push(startNodeId);
            while (stack.Count > 0)
            {
                string current = stack.Pop();
                if (!result.Add(current) || !adjacency.TryGetValue(current, out List<string> next))
                    continue;
                for (int i = 0; i < next.Count; i++)
                    stack.Push(next[i]);
            }
        }

#if UNITY_EDITOR
        public List<string> DuplicateNodes(IReadOnlyCollection<string> sourceNodeIds, Vector2 offset)
        {
            EnsureCollections();
            List<string> createdNodeIds = new List<string>();
            if (sourceNodeIds == null || sourceNodeIds.Count == 0)
                return createdNodeIds;

            HashSet<string> selected = new HashSet<string>(sourceNodeIds, StringComparer.Ordinal);
            Dictionary<string, string> portIdMap = new Dictionary<string, string>(StringComparer.Ordinal);
            List<ESGraphNodeRecord> clones = new List<ESGraphNodeRecord>();

            for (int i = 0; i < nodes.Count; i++)
            {
                ESGraphNodeRecord source = nodes[i];
                if (source == null || !selected.Contains(source.nodeId))
                    continue;
                ESGraphNodeRecord clone = source.CloneWithNewIdentity(offset, portIdMap);
                clones.Add(clone);
                createdNodeIds.Add(clone.nodeId);
            }

            nodes.AddRange(clones);

            int originalEdgeCount = edges.Count;
            for (int i = 0; i < originalEdgeCount; i++)
            {
                ESGraphEdgeRecord source = edges[i];
                if (source == null
                    || !portIdMap.TryGetValue(source.outputPortId, out string clonedOutput)
                    || !portIdMap.TryGetValue(source.inputPortId, out string clonedInput))
                    continue;
                edges.Add(new ESGraphEdgeRecord
                {
                    edgeId = ESGraphIdentity.NewId(),
                    outputPortId = clonedOutput,
                    inputPortId = clonedInput,
                    order = NextEdgeOrder()
                });
            }

            return createdNodeIds;
        }

        public List<string> PasteNodes(
            IReadOnlyList<ESGraphNodeRecord> sourceNodes,
            IReadOnlyList<ESGraphEdgeRecord> sourceEdges,
            Vector2 offset,
            out string error,
            int sourceSchemaVersion,
            string sourceDomainId,
            out int createdEdgeCount)
        {
            EnsureCollections();
            error = null;
            createdEdgeCount = 0;
            List<string> createdNodeIds = new List<string>();
            if (sourceNodes == null || sourceNodes.Count == 0)
            {
                error = "剪贴板没有可粘贴的节点。";
                return createdNodeIds;
            }
            if (sourceSchemaVersion > CurrentSchemaVersion)
            {
                error = "剪贴板图 Schema 来自未来版本：" + sourceSchemaVersion;
                return createdNodeIds;
            }
            if (sourceSchemaVersion < CurrentSchemaVersion)
            {
                error = "剪贴板图 Schema " + sourceSchemaVersion
                    + " 需要显式迁移到 Schema " + CurrentSchemaVersion
                    + "，当前粘贴入口拒绝自动升级。";
                return createdNodeIds;
            }
            if (string.IsNullOrWhiteSpace(sourceDomainId)
                || !string.Equals(sourceDomainId, DomainId, StringComparison.Ordinal))
            {
                error = "剪贴板图 Domain 与当前图不一致。";
                return createdNodeIds;
            }

            HashSet<string> sourceNodeIds = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> sourcePortIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < sourceNodes.Count; i++)
            {
                ESGraphNodeRecord source = sourceNodes[i];
                if (source == null)
                {
                    error = "剪贴板包含空节点。";
                    return createdNodeIds;
                }
                if (!ESGraphIdentity.IsValid(source.nodeId))
                {
                    error = "剪贴板节点身份非法：" + source.nodeId;
                    return createdNodeIds;
                }
                if (!sourceNodeIds.Add(source.nodeId))
                {
                    error = "剪贴板节点身份重复：" + source.nodeId;
                    return createdNodeIds;
                }
                if (string.IsNullOrWhiteSpace(source.typeId)
                    || !ESGraphNodeTypeKey.Parse(source.typeId).IsValid)
                {
                    error = "剪贴板节点类型非法：" + source.typeId;
                    return createdNodeIds;
                }
                if (source.version < 1)
                {
                    error = "剪贴板节点版本非法：" + source.nodeId;
                    return createdNodeIds;
                }
                if (source.ports == null)
                    continue;
                HashSet<string> nodeStableKeys = new HashSet<string>(StringComparer.Ordinal);
                for (int p = 0; p < source.ports.Count; p++)
                {
                    ESGraphPortRecord port = source.ports[p];
                    if (port == null)
                    {
                        error = "剪贴板节点包含空端口：" + source.nodeId;
                        return createdNodeIds;
                    }
                    if (!ESGraphIdentity.IsValid(port.portId))
                    {
                        error = "剪贴板端口身份非法：" + port.portId;
                        return createdNodeIds;
                    }
                    if (!sourcePortIds.Add(port.portId))
                    {
                        error = "剪贴板端口身份重复：" + port.portId;
                        return createdNodeIds;
                    }
                    if (string.IsNullOrWhiteSpace(port.stableKey)
                        || !ESGraphEndpointRules.IsValidMeaning(port.meaning)
                        || string.IsNullOrWhiteSpace(port.valueTypeId))
                    {
                        error = "剪贴板端点稳定名称、用途或类型缺失：" + port.portId;
                        return createdNodeIds;
                    }
                    if (!System.Enum.IsDefined(typeof(ESGraphPortDirection), port.direction)
                        || !System.Enum.IsDefined(typeof(ESGraphPortCapacity), port.capacity))
                    {
                        error = "剪贴板端口方向或容量枚举非法：" + port.portId;
                        return createdNodeIds;
                    }
                    if (!nodeStableKeys.Add(port.stableKey))
                    {
                        error = "剪贴板节点端口 StableKey 重复：" + source.nodeId;
                        return createdNodeIds;
                    }
                }
            }

            if (sourceEdges != null)
            {
                HashSet<string> sourceEdgeIds = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < sourceEdges.Count; i++)
                {
                    ESGraphEdgeRecord source = sourceEdges[i];
                    if (source == null)
                    {
                        error = "剪贴板包含空连线。";
                        return createdNodeIds;
                    }
                    if (!ESGraphIdentity.IsValid(source.edgeId))
                    {
                        error = "剪贴板连线身份非法：" + source.edgeId;
                        return createdNodeIds;
                    }
                    if (!sourceEdgeIds.Add(source.edgeId))
                    {
                        error = "剪贴板连线身份重复：" + source.edgeId;
                        return createdNodeIds;
                    }
                    if (!sourcePortIds.Contains(source.outputPortId)
                        || !sourcePortIds.Contains(source.inputPortId))
                    {
                        error = "剪贴板连线引用了未知端口：" + source.edgeId;
                        return createdNodeIds;
                    }
                    if (string.Equals(source.outputPortId, source.inputPortId, StringComparison.Ordinal))
                    {
                        error = "剪贴板连线不能连接同一端口：" + source.edgeId;
                        return createdNodeIds;
                    }
                }
            }

            Dictionary<string, string> portIdMap = new Dictionary<string, string>(StringComparer.Ordinal);
            List<ESGraphNodeRecord> clones = new List<ESGraphNodeRecord>(sourceNodes.Count);
            for (int i = 0; i < sourceNodes.Count; i++)
            {
                ESGraphNodeRecord source = sourceNodes[i];
                ESGraphNodeRecord clone = source.CloneWithNewIdentity(offset, portIdMap);
                clones.Add(clone);
                createdNodeIds.Add(clone.nodeId);
            }

            if (clones.Count == 0)
            {
                error = "剪贴板节点均不可用。";
                return createdNodeIds;
            }

            int startNodeCount = nodes.Count;
            int startEdgeCount = edges.Count;
            nodes.AddRange(clones);
            if (sourceEdges != null)
            {
                for (int i = 0; i < sourceEdges.Count; i++)
                {
                    ESGraphEdgeRecord source = sourceEdges[i];
                    portIdMap.TryGetValue(source.outputPortId, out string clonedOutput);
                    portIdMap.TryGetValue(source.inputPortId, out string clonedInput);
                    if (!TryAddEdge(clonedOutput, clonedInput, out _, out string edgeError))
                    {
                        nodes.RemoveRange(startNodeCount, nodes.Count - startNodeCount);
                        if (edges.Count > startEdgeCount)
                            edges.RemoveRange(startEdgeCount, edges.Count - startEdgeCount);
                        createdNodeIds.Clear();
                        createdEdgeCount = 0;
                        error = "剪贴板连线违反图连接契约：" + edgeError;
                        return createdNodeIds;
                    }
                    createdEdgeCount++;
                }
            }

            return createdNodeIds;
        }
#endif

        public ESGraphNodeRecord FindNode(string nodeId)
        {
            EnsureCollections();
            if (string.IsNullOrEmpty(nodeId))
                return null;
            for (int i = 0; i < nodes.Count; i++)
            {
                ESGraphNodeRecord node = nodes[i];
                if (node != null && string.Equals(node.nodeId, nodeId, StringComparison.Ordinal))
                    return node;
            }
            return null;
        }

        public ESGraphEdgeRecord FindEdge(string edgeId)
        {
            EnsureCollections();
            if (string.IsNullOrEmpty(edgeId))
                return null;
            for (int i = 0; i < edges.Count; i++)
            {
                ESGraphEdgeRecord edge = edges[i];
                if (edge != null && string.Equals(edge.edgeId, edgeId, StringComparison.Ordinal))
                    return edge;
            }
            return null;
        }

        public bool TryFindPort(string portId, out ESGraphNodeRecord node, out ESGraphPortRecord port)
        {
            EnsureCollections();
            node = null;
            port = null;
            if (string.IsNullOrEmpty(portId))
                return false;
            for (int i = 0; i < nodes.Count; i++)
            {
                ESGraphNodeRecord current = nodes[i];
                if (current?.ports == null)
                    continue;
                for (int p = 0; p < current.ports.Count; p++)
                {
                    ESGraphPortRecord candidate = current.ports[p];
                    if (candidate != null && string.Equals(candidate.portId, portId, StringComparison.Ordinal))
                    {
                        node = current;
                        port = candidate;
                        return true;
                    }
                }
            }
            return false;
        }

        public List<ESGraphValidationIssue> ValidateGraph()
        {
            EnsureCollections();
            List<ESGraphValidationIssue> issues = new List<ESGraphValidationIssue>();
            if (schemaVersion < MinimumSupportedSchemaVersion || schemaVersion > CurrentSchemaVersion)
                issues.Add(ESGraphValidationIssue.Error("Graph.Schema.Unsupported", "不支持的图 SchemaVersion：" + schemaVersion));
            else if (schemaVersion < CurrentSchemaVersion)
                issues.Add(ESGraphValidationIssue.Error("Graph.Schema.MigrationRequired",
                    "图需要显式升级到 Schema " + CurrentSchemaVersion + " 后才能校验或烘焙。"));
            if (!ESGraphIdentity.IsValid(GraphId))
                issues.Add(ESGraphValidationIssue.Error("Graph.Identity.Invalid", "GraphId 为空或格式非法。"));
            if (!string.IsNullOrEmpty(OriginGraphId) && !ESGraphIdentity.IsValid(OriginGraphId))
                issues.Add(ESGraphValidationIssue.Error("Graph.Identity.OriginInvalid", "OriginGraphId 格式非法。"));
            if (!ESGraphStableIdUtility.IsValid(DomainId))
                issues.Add(ESGraphValidationIssue.Error("Graph.Domain.Identity", "Graph DomainId 非法：" + DomainId));

            HashSet<string> nodeIds = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> portIds = new HashSet<string>(StringComparer.Ordinal);
            Dictionary<string, ESGraphPortRecord> portsById = new Dictionary<string, ESGraphPortRecord>(StringComparer.Ordinal);
            Dictionary<string, string> nodeByPort = new Dictionary<string, string>(StringComparer.Ordinal);

            for (int i = 0; i < nodes.Count; i++)
            {
                ESGraphNodeRecord node = nodes[i];
                if (node == null)
                {
                    issues.Add(ESGraphValidationIssue.Error("Graph.Node.Null", "图包含空节点记录。"));
                    continue;
                }
                if (!ESGraphIdentity.IsValid(node.nodeId) || !nodeIds.Add(node.nodeId))
                    issues.Add(ESGraphValidationIssue.Error("Graph.Node.Identity", "节点 ID 为空或重复。", node.nodeId));
                if (string.IsNullOrWhiteSpace(node.typeId)
                    || !ESGraphStableIdUtility.IsValid(node.typeId) || node.version < 1)
                    issues.Add(ESGraphValidationIssue.Error("Graph.Node.Type", "节点 TypeId 为空或版本非法。", node.nodeId));
                if (node.ports == null)
                {
                    issues.Add(ESGraphValidationIssue.Error("Graph.Node.PortsMissing", "节点 Ports 容器缺失。", node.nodeId));
                    continue;
                }

                HashSet<string> stableKeys = new HashSet<string>(StringComparer.Ordinal);
                for (int p = 0; p < node.ports.Count; p++)
                {
                    ESGraphPortRecord port = node.ports[p];
                    if (port == null)
                    {
                        issues.Add(ESGraphValidationIssue.Error("Graph.Port.Null", "节点包含空端口记录。", node.nodeId));
                        continue;
                    }
                    if (!ESGraphIdentity.IsValid(port.portId) || !portIds.Add(port.portId))
                        issues.Add(ESGraphValidationIssue.Error("Graph.Port.Identity", "端口 ID 为空或重复。", port.portId));
                    else
                    {
                        portsById[port.portId] = port;
                        nodeByPort[port.portId] = node.nodeId;
                    }
                    if (!ESGraphStableIdUtility.IsValid(port.stableKey)
                        || !stableKeys.Add(port.stableKey))
                        issues.Add(ESGraphValidationIssue.Error("Graph.Port.StableKey", "节点内端口 StableKey 为空或重复。", port.portId));
                    if (!ESGraphEndpointRules.IsValidMeaning(port.meaning))
                        issues.Add(ESGraphValidationIssue.Error("Graph.Port.Meaning",
                            "每个端点都必须提供独立、明确且不超过 "
                            + ESGraphEndpointRules.MaxMeaningLength + " 字的用途。", port.portId));
                    if (!ESGraphPortValueCatalog.IsValidStableId(port.valueTypeId))
                        issues.Add(ESGraphValidationIssue.Error("Graph.Port.Type", "端口 ValueTypeId 为空。", port.portId));
                    if (!Enum.IsDefined(typeof(ESGraphPortDirection), port.direction))
                        issues.Add(ESGraphValidationIssue.Error("Graph.Port.Direction", "端口方向非法。", port.portId));
                    if (!Enum.IsDefined(typeof(ESGraphPortCapacity), port.capacity))
                        issues.Add(ESGraphValidationIssue.Error("Graph.Port.CapacityKind", "端口容量非法。", port.portId));
                    if (!Enum.IsDefined(typeof(ESGraphPortAggregation), port.aggregation))
                        issues.Add(ESGraphValidationIssue.Error("Graph.Port.AggregationKind", "端口聚合模式非法。", port.portId));
                    else if (!ESGraphPortAggregationRules.IsCompatible(port.direction,
                            port.capacity, port.aggregation))
                        issues.Add(ESGraphValidationIssue.Error("Graph.Port.Aggregation",
                            "端点方向、容量与聚合模式不兼容。", port.portId));
                }
            }

            HashSet<string> edgeIds = new HashSet<string>(StringComparer.Ordinal);
            HashSet<int> edgeOrders = new HashSet<int>();
            HashSet<string> endpoints = new HashSet<string>(StringComparer.Ordinal);
            Dictionary<string, int> connectionCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < edges.Count; i++)
            {
                ESGraphEdgeRecord edge = edges[i];
                if (edge == null)
                {
                    issues.Add(ESGraphValidationIssue.Error("Graph.Edge.Null", "图包含空连线记录。"));
                    continue;
                }
                if (!ESGraphIdentity.IsValid(edge.edgeId) || !edgeIds.Add(edge.edgeId))
                    issues.Add(ESGraphValidationIssue.Error("Graph.Edge.Identity", "连线 ID 为空或重复。", edge.edgeId));
                if (edge.order < 0 || !edgeOrders.Add(edge.order))
                    issues.Add(ESGraphValidationIssue.Error("Graph.Edge.Order",
                        "连线顺序必须是非负且在图内唯一。", edge.edgeId));
                if (!portsById.TryGetValue(edge.outputPortId ?? string.Empty, out ESGraphPortRecord output)
                    || !portsById.TryGetValue(edge.inputPortId ?? string.Empty, out ESGraphPortRecord input))
                {
                    issues.Add(ESGraphValidationIssue.Error("Graph.Edge.MissingPort", "连线引用不存在的端口。", edge.edgeId));
                    continue;
                }
                if (output.direction != ESGraphPortDirection.Output || input.direction != ESGraphPortDirection.Input)
                    issues.Add(ESGraphValidationIssue.Error("Graph.Edge.Direction", "连线方向必须为 Output → Input。", edge.edgeId));
                if (!ArePortTypesCompatible(output.valueTypeId, input.valueTypeId))
                    issues.Add(ESGraphValidationIssue.Error("Graph.Edge.Type", "连线端口类型不兼容。", edge.edgeId));
                string endpointKey = edge.outputPortId + "\n" + edge.inputPortId;
                if (!endpoints.Add(endpointKey))
                    issues.Add(ESGraphValidationIssue.Error("Graph.Edge.Duplicate", "存在重复连线。", edge.edgeId));
                Increment(connectionCounts, edge.outputPortId);
                Increment(connectionCounts, edge.inputPortId);
            }

            foreach (KeyValuePair<string, int> pair in connectionCounts)
            {
                if (pair.Value > 1 && portsById.TryGetValue(pair.Key, out ESGraphPortRecord port))
                {
                    if (port.capacity == ESGraphPortCapacity.Single)
                        issues.Add(ESGraphValidationIssue.Error("Graph.Port.Capacity", "Single 端口存在多条连线。", pair.Key));
                    if (port.direction == ESGraphPortDirection.Input
                        && ESGraphPortAggregationRules.Resolve(port.capacity, port.aggregation)
                            == ESGraphPortAggregation.Single)
                        issues.Add(ESGraphValidationIssue.Error("Graph.Port.Aggregation",
                            "Single 聚合输入端点存在多条连线。", pair.Key));
                }
            }

            if (!AllowsCycles && ContainsCycle(nodeByPort))
                issues.Add(ESGraphValidationIssue.Error("Graph.Cycle.Forbidden", "当前图禁止循环，但检测到循环。"));
            return issues;
        }

        private bool ContainsCycle(Dictionary<string, string> nodeByPort)
        {
            Dictionary<string, List<string>> adjacency = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            for (int i = 0; i < nodes.Count; i++)
            {
                ESGraphNodeRecord node = nodes[i];
                if (node != null && !string.IsNullOrEmpty(node.nodeId))
                    adjacency[node.nodeId] = new List<string>();
            }
            for (int i = 0; i < edges.Count; i++)
            {
                ESGraphEdgeRecord edge = edges[i];
                if (edge == null || !nodeByPort.TryGetValue(edge.outputPortId ?? string.Empty, out string from)
                    || !nodeByPort.TryGetValue(edge.inputPortId ?? string.Empty, out string to))
                    continue;
                if (!adjacency.TryGetValue(from, out List<string> list))
                    adjacency[from] = list = new List<string>();
                list.Add(to);
            }
            return HasCycle(adjacency);
        }

        private static bool HasCycle(Dictionary<string, List<string>> adjacency)
        {
            Dictionary<string, int> indegree = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (string nodeId in adjacency.Keys)
                indegree[nodeId] = 0;
            foreach (KeyValuePair<string, List<string>> pair in adjacency)
            {
                List<string> next = pair.Value;
                if (next == null)
                    continue;
                for (int i = 0; i < next.Count; i++)
                {
                    string target = next[i];
                    indegree.TryGetValue(target, out int count);
                    indegree[target] = count + 1;
                }
            }
            Queue<string> ready = new Queue<string>();
            foreach (KeyValuePair<string, int> pair in indegree)
            {
                if (pair.Value == 0)
                    ready.Enqueue(pair.Key);
            }
            int visited = 0;
            while (ready.Count > 0)
            {
                string current = ready.Dequeue();
                visited++;
                if (!adjacency.TryGetValue(current, out List<string> next) || next == null)
                    continue;
                for (int i = 0; i < next.Count; i++)
                {
                    string target = next[i];
                    int count = indegree[target] - 1;
                    indegree[target] = count;
                    if (count == 0)
                        ready.Enqueue(target);
                }
            }
            return visited != indegree.Count;
        }

        private static bool ArePortTypesCompatible(string outputType, string inputType)
        {
            return ESGraphPortValueCatalog.AreCompatible(outputType, inputType);
        }

        private static void Increment(Dictionary<string, int> counts, string id)
        {
            if (string.IsNullOrEmpty(id))
                return;
            counts.TryGetValue(id, out int count);
            counts[id] = count + 1;
        }

        private void EnsureCollections()
        {
            if (nodes == null) nodes = new List<ESGraphNodeRecord>();
            if (edges == null) edges = new List<ESGraphEdgeRecord>();
        }

        protected virtual void OnValidate()
        {
            EnsureCollections();
#if UNITY_EDITOR
            EnsureGraphIdentity();
#endif
        }
    }

    /// <summary>General-purpose runtime-capable graph authoring asset.</summary>
    [ESGraphAssetDomain(ESGraphDomainIds.Generic)]
    public sealed partial class ESGenericGraphAsset : ESGraphAssetBase
    {
        public override ESGraphDomainKey DomainKey => ESGraphDomainKey.FromKind(ESGraphDomainKind.Generic);
    }

    /// <summary>Story, quest, and dialogue graph authoring asset.</summary>
    [ESGraphAssetDomain(ESGraphDomainIds.Story)]
    public sealed partial class ESStoryGraphAsset : ESGraphAssetBase
    {
        public override ESGraphDomainKey DomainKey => ESGraphDomainKey.FromKind(ESGraphDomainKind.Story);
    }

    /// <summary>Behavior-tree graph authoring asset.</summary>
    [ESGraphAssetDomain(ESGraphDomainIds.BehaviorTree)]
    public sealed partial class ESBehaviorTreeGraphAsset : ESGraphAssetBase
    {
        public override ESGraphDomainKey DomainKey => ESGraphDomainKey.FromKind(ESGraphDomainKind.BehaviorTree);
        public override bool CanEnableCycles => false;
    }

}
