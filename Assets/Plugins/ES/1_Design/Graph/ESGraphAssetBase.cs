using System;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{
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
        public string valueTypeId = ESGraphPortValueIds.Flow;
        public ESGraphPortDirection direction;
        public ESGraphPortCapacity capacity = ESGraphPortCapacity.Single;
        public ESGraphPortValueKind ValueKind => ESGraphPortValueCatalog.GetKind(valueTypeId);

        public ESGraphPortDefinition()
        {
        }

        public ESGraphPortDefinition(string name, string stableKey, ESGraphPortDirection direction,
            ESGraphPortCapacity capacity = ESGraphPortCapacity.Single,
            string valueTypeId = ESGraphPortValueIds.Flow)
        {
            this.name = name;
            this.stableKey = stableKey;
            this.direction = direction;
            this.capacity = capacity;
            this.valueTypeId = valueTypeId;
        }

        public ESGraphPortDefinition(string name, string stableKey, ESGraphPortDirection direction,
            ESGraphPortCapacity capacity, ESGraphPortValueKind valueKind, string customValueTypeId = null)
            : this(name, stableKey, direction, capacity,
                ESGraphPortValueCatalog.GetStableId(valueKind, customValueTypeId))
        {
        }
    }

    [Serializable]
    public sealed class ESGraphPortRecord
    {
        public string portId;
        public string stableKey;
        public string name;
        public string valueTypeId = ESGraphPortValueIds.Flow;
        public ESGraphPortDirection direction;
        public ESGraphPortCapacity capacity = ESGraphPortCapacity.Single;
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
                valueTypeId = valueTypeId,
                direction = direction,
                capacity = capacity
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

        public const int CurrentSchemaVersion = 2;
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

        public void InitializeAsIndependentCopyOf(string sourceGraphId)
        {
            graphId = ESGraphIdentity.NewId();
            originGraphId = ESGraphIdentity.IsValid(sourceGraphId) ? sourceGraphId : string.Empty;
        }

        public ESGraphNodeRecord AddNode(string typeId, string title, Vector2 position,
            IReadOnlyList<ESGraphPortDefinition> portDefinitions)
        {
            EnsureCollections();
            if (string.IsNullOrWhiteSpace(typeId))
                throw new ArgumentException("Graph node TypeId cannot be empty.", nameof(typeId));

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
                    string stableKey = string.IsNullOrWhiteSpace(definition.stableKey)
                        ? definition.direction.ToString().ToLowerInvariant() + "." + i
                        : definition.stableKey.Trim();
                    if (!stableKeys.Add(stableKey))
                        throw new InvalidOperationException("Duplicate port stable key: " + stableKey);
                    node.ports.Add(new ESGraphPortRecord
                    {
                        portId = ESGraphIdentity.NewId(),
                        stableKey = stableKey,
                        name = string.IsNullOrWhiteSpace(definition.name) ? stableKey : definition.name.Trim(),
                        valueTypeId = string.IsNullOrWhiteSpace(definition.valueTypeId)
                            ? ESGraphPortValueIds.Flow
                            : definition.valueTypeId.Trim(),
                        direction = definition.direction,
                        capacity = definition.capacity
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
            if (string.IsNullOrWhiteSpace(typeId) || version < 1)
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
            ESGraphPortRecord port = new ESGraphPortRecord
            {
                portId = ESGraphIdentity.NewId(),
                stableKey = stableKey,
                name = string.IsNullOrWhiteSpace(definition.name) ? stableKey : definition.name.Trim(),
                valueTypeId = definition.valueTypeId.Trim(),
                direction = definition.direction,
                capacity = definition.capacity
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
            if (string.IsNullOrEmpty(stableKey) || string.IsNullOrWhiteSpace(definition.valueTypeId))
            {
                error = "Port StableKey 和 ValueTypeId 不能为空。";
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

        public bool UpdatePort(string portId, string stableKey, string name, string valueTypeId,
            ESGraphPortDirection direction, ESGraphPortCapacity capacity, out string error)
        {
            if (!CanUpdatePort(portId, stableKey, valueTypeId, direction, capacity, out error))
                return false;
            TryFindPort(portId, out _, out ESGraphPortRecord port);
            stableKey = stableKey?.Trim();
            valueTypeId = valueTypeId?.Trim();
            port.stableKey = stableKey;
            port.name = string.IsNullOrWhiteSpace(name) ? stableKey : name.Trim();
            port.valueTypeId = valueTypeId;
            port.direction = direction;
            port.capacity = capacity;
            return true;
        }

        public bool CanUpdatePort(string portId, string stableKey, string valueTypeId,
            ESGraphPortDirection direction, ESGraphPortCapacity capacity, out string error)
        {
            error = null;
            if (!TryFindPort(portId, out ESGraphNodeRecord node, out ESGraphPortRecord port))
            {
                error = "端口不存在。";
                return false;
            }
            stableKey = stableKey?.Trim();
            valueTypeId = valueTypeId?.Trim();
            if (string.IsNullOrEmpty(stableKey) || string.IsNullOrEmpty(valueTypeId))
            {
                error = "Port StableKey 和 ValueTypeId 不能为空。";
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
                inputPortId = inputPortId
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
                    inputPortId = clonedInput
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
                        || string.IsNullOrWhiteSpace(port.valueTypeId))
                    {
                        error = "剪贴板端口 StableKey 或类型缺失：" + port.portId;
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
                if (string.IsNullOrWhiteSpace(node.nodeId) || !nodeIds.Add(node.nodeId))
                    issues.Add(ESGraphValidationIssue.Error("Graph.Node.Identity", "节点 ID 为空或重复。", node.nodeId));
                if (string.IsNullOrWhiteSpace(node.typeId) || node.version < 1)
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
                    if (string.IsNullOrWhiteSpace(port.portId) || !portIds.Add(port.portId))
                        issues.Add(ESGraphValidationIssue.Error("Graph.Port.Identity", "端口 ID 为空或重复。", port.portId));
                    else
                    {
                        portsById[port.portId] = port;
                        nodeByPort[port.portId] = node.nodeId;
                    }
                    if (string.IsNullOrWhiteSpace(port.stableKey) || !stableKeys.Add(port.stableKey))
                        issues.Add(ESGraphValidationIssue.Error("Graph.Port.StableKey", "节点内端口 StableKey 为空或重复。", port.portId));
                    if (string.IsNullOrWhiteSpace(port.valueTypeId))
                        issues.Add(ESGraphValidationIssue.Error("Graph.Port.Type", "端口 ValueTypeId 为空。", port.portId));
                }
            }

            HashSet<string> edgeIds = new HashSet<string>(StringComparer.Ordinal);
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
                if (string.IsNullOrWhiteSpace(edge.edgeId) || !edgeIds.Add(edge.edgeId))
                    issues.Add(ESGraphValidationIssue.Error("Graph.Edge.Identity", "连线 ID 为空或重复。", edge.edgeId));
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
                if (pair.Value > 1 && portsById.TryGetValue(pair.Key, out ESGraphPortRecord port)
                    && port.capacity == ESGraphPortCapacity.Single)
                    issues.Add(ESGraphValidationIssue.Error("Graph.Port.Capacity", "Single 端口存在多条连线。", pair.Key));
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
            if (schemaVersion < 1)
                schemaVersion = CurrentSchemaVersion;
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

#if UNITY_EDITOR
    public enum ESAgentRelationKind : byte
    {
        ProvidesContext = 0,
        AppliesConstraint = 1,
        RequiresValidation = 2,
        SelectsBranch = 3,
        TraversesItems = 4,
        ExecutesNext = 5,
        BindsValue = 6
    }

    /// <summary>Stable string identities owned exclusively by Agent authoring in the Unity Editor.</summary>
    public static class ESAgentGraphStableIds
    {
        public const string DomainId = "es.agent-authoring";
        public const string GoalNode = "es.agent-authoring.goal";
        public const string ReferenceNode = "es.agent-authoring.reference";
        public const string ConstraintNode = "es.agent-authoring.constraint";
        public const string BranchNode = "es.agent-authoring.branch";
        public const string TraverseNode = "es.agent-authoring.traverse";
        public const string AICommandOutputNode = "es.agent-authoring.output.ai-command";
        public const string AISkillOutputNode = "es.agent-authoring.output.agent-skill";
        public const string ValidationNode = "es.agent-authoring.validation";
        public const string ContextPort = "es.agent-authoring.context";
        public const string RequirementPort = "es.agent-authoring.requirement";
        public const string ArtifactPort = "es.agent-authoring.artifact";
        public const string BranchMatchedPortKey = "agent.branch.matched";
        public const string BranchDefaultPortKey = "agent.branch.default";
        public const string BranchFailurePortKey = "agent.branch.failure";
        public const string TraverseItemPortKey = "agent.traverse.item";
        public const string TraverseCompletedPortKey = "agent.traverse.completed";
        public const string TraverseFailurePortKey = "agent.traverse.failure";
        public const string AICommandArtifact = "es.agent.ai-command";
        public const string AISkillArtifact = "es.agent.ai-skill";

        // AISkill execution authoring. These remain Editor-only even though the owning file is in Design.
        public const string SkillInputNode = "es.agent.ai-skill.input";
        public const string SkillTaskNode = "es.agent.ai-skill.task";
        public const string SkillBranchNode = "es.agent.ai-skill.branch";
        public const string SkillForEachNode = "es.agent.ai-skill.for-each";
        public const string SkillApprovalNode = "es.agent.ai-skill.approval";
        public const string SkillOutputNode = "es.agent.ai-skill.output";
        public const string SkillControlPort = "es.agent.ai-skill.control";
        public const string SkillTextListPort = "es.agent.ai-skill.text-list";
        public const string SkillProjectPathPort = "es.agent.ai-skill.project-path";
        public const string SkillProjectPathListPort = "es.agent.ai-skill.project-path-list";
        public const string SkillRunResultPort = "es.agent.ai-skill.run-result";
        public const string SkillArtifactListPort = "es.agent.ai-skill.artifact-list";
        public const string SkillControlInputKey = "skill.control.input";
        public const string SkillNextPortKey = "skill.control.next";
        public const string SkillSuccessPortKey = "skill.control.success";
        public const string SkillFailurePortKey = "skill.control.failure";
        public const string SkillTimeoutPortKey = "skill.control.timeout";
        public const string SkillCancelledPortKey = "skill.control.cancelled";
        public const string SkillMatchedPortKey = "skill.control.matched";
        public const string SkillDefaultPortKey = "skill.control.default";
        public const string SkillItemPortKey = "skill.control.item";
        public const string SkillCompletedPortKey = "skill.control.completed";
        public const string SkillEmptyPortKey = "skill.control.empty";
        public const string SkillApprovedPortKey = "skill.control.approved";
        public const string SkillRejectedPortKey = "skill.control.rejected";

        public static ESGraphDomainKey Domain => ESGraphDomainKey.Parse(DomainId);
        public static ESGraphNodeTypeKey Node(string stableId) => ESGraphNodeTypeKey.Parse(stableId);
    }

    /// <summary>AI 节点关系的唯一语义表，供连接门禁、Graph 校验与 Bake 共同使用。</summary>
    public static class ESAgentRelationSemantics
    {
        public static bool TryResolve(string fromTypeId, string toTypeId, string fromPortStableKey,
            out ESAgentRelationKind relationKind)
        {
            if (IsSkillExecutionNode(fromTypeId) && IsSkillExecutionNode(toTypeId))
            {
                relationKind = IsSkillControlPort(fromPortStableKey)
                    ? ESAgentRelationKind.ExecutesNext
                    : ESAgentRelationKind.BindsValue;
                return true;
            }
            if ((Is(fromTypeId, ESAgentGraphStableIds.GoalNode)
                    || Is(fromTypeId, ESAgentGraphStableIds.ReferenceNode))
                && IsContextDestination(toTypeId))
            {
                relationKind = ESAgentRelationKind.ProvidesContext;
                return true;
            }
            if (Is(fromTypeId, ESAgentGraphStableIds.BranchNode)
                && IsBranchRoute(fromPortStableKey) && IsContextDestination(toTypeId))
            {
                relationKind = ESAgentRelationKind.SelectsBranch;
                return true;
            }
            if (Is(fromTypeId, ESAgentGraphStableIds.TraverseNode)
                && IsTraversalRoute(fromPortStableKey) && IsContextDestination(toTypeId))
            {
                relationKind = ESAgentRelationKind.TraversesItems;
                return true;
            }
            if (Is(fromTypeId, ESAgentGraphStableIds.ConstraintNode)
                && (Is(toTypeId, ESAgentGraphStableIds.AICommandOutputNode)
                    || Is(toTypeId, ESAgentGraphStableIds.AISkillOutputNode)))
            {
                relationKind = ESAgentRelationKind.AppliesConstraint;
                return true;
            }
            if ((Is(fromTypeId, ESAgentGraphStableIds.AICommandOutputNode)
                    || Is(fromTypeId, ESAgentGraphStableIds.AISkillOutputNode))
                && Is(toTypeId, ESAgentGraphStableIds.ValidationNode))
            {
                relationKind = ESAgentRelationKind.RequiresValidation;
                return true;
            }
            relationKind = default;
            return false;
        }

        public static string ExpectedSemanticType(ESAgentRelationKind relationKind)
        {
            switch (relationKind)
            {
                case ESAgentRelationKind.ProvidesContext:
                case ESAgentRelationKind.SelectsBranch:
                case ESAgentRelationKind.TraversesItems:
                    return ESAgentGraphStableIds.ContextPort;
                case ESAgentRelationKind.AppliesConstraint:
                    return ESAgentGraphStableIds.RequirementPort;
                case ESAgentRelationKind.RequiresValidation:
                    return ESAgentGraphStableIds.ArtifactPort;
                case ESAgentRelationKind.ExecutesNext:
                    return ESAgentGraphStableIds.SkillControlPort;
                default:
                    return string.Empty;
            }
        }

        public static bool IsSkillExecutionNode(string typeId)
        {
            return Is(typeId, ESAgentGraphStableIds.SkillInputNode)
                || Is(typeId, ESAgentGraphStableIds.SkillTaskNode)
                || Is(typeId, ESAgentGraphStableIds.SkillBranchNode)
                || Is(typeId, ESAgentGraphStableIds.SkillForEachNode)
                || Is(typeId, ESAgentGraphStableIds.SkillApprovalNode)
                || Is(typeId, ESAgentGraphStableIds.SkillOutputNode);
        }

        public static bool IsSkillControlPort(string stableKey)
        {
            return Is(stableKey, ESAgentGraphStableIds.SkillNextPortKey)
                || Is(stableKey, ESAgentGraphStableIds.SkillSuccessPortKey)
                || Is(stableKey, ESAgentGraphStableIds.SkillFailurePortKey)
                || Is(stableKey, ESAgentGraphStableIds.SkillTimeoutPortKey)
                || Is(stableKey, ESAgentGraphStableIds.SkillCancelledPortKey)
                || Is(stableKey, ESAgentGraphStableIds.SkillMatchedPortKey)
                || Is(stableKey, ESAgentGraphStableIds.SkillDefaultPortKey)
                || Is(stableKey, ESAgentGraphStableIds.SkillItemPortKey)
                || Is(stableKey, ESAgentGraphStableIds.SkillCompletedPortKey)
                || Is(stableKey, ESAgentGraphStableIds.SkillEmptyPortKey)
                || Is(stableKey, ESAgentGraphStableIds.SkillApprovedPortKey)
                || Is(stableKey, ESAgentGraphStableIds.SkillRejectedPortKey);
        }

        public static bool IsBranchRoute(string stableKey)
        {
            return Is(stableKey, ESAgentGraphStableIds.BranchMatchedPortKey)
                || Is(stableKey, ESAgentGraphStableIds.BranchDefaultPortKey)
                || Is(stableKey, ESAgentGraphStableIds.BranchFailurePortKey);
        }

        public static bool IsTraversalRoute(string stableKey)
        {
            return Is(stableKey, ESAgentGraphStableIds.TraverseItemPortKey)
                || Is(stableKey, ESAgentGraphStableIds.TraverseCompletedPortKey)
                || Is(stableKey, ESAgentGraphStableIds.TraverseFailurePortKey);
        }

        private static bool IsContextDestination(string typeId)
        {
            return Is(typeId, ESAgentGraphStableIds.ReferenceNode)
                || Is(typeId, ESAgentGraphStableIds.ConstraintNode)
                || Is(typeId, ESAgentGraphStableIds.BranchNode)
                || Is(typeId, ESAgentGraphStableIds.TraverseNode);
        }

        private static bool Is(string left, string right)
        {
            return string.Equals(left, right, StringComparison.Ordinal);
        }
    }

    /// <summary>Marks an Editor-only handler for one generated Agent artifact kind.</summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class ESAgentArtifactAttribute : Attribute
    {
        public string StableId { get; }

        public ESAgentArtifactAttribute(string stableId)
        {
            if (!ESGraphStableIdUtility.IsValid(stableId))
                throw new ArgumentException("Agent 产物稳定标识非法。", nameof(stableId));
            StableId = stableId;
        }
    }

    /// <summary>Domain-reload-safe metadata registry populated by ES AssemblyStream.</summary>
    public static class ESAgentArtifactTypeRegistry
    {
        private static readonly Dictionary<string, Type> Types =
            new Dictionary<string, Type>(StringComparer.Ordinal);

        public static bool TryGet(string stableId, out Type type)
        {
            return Types.TryGetValue(stableId ?? string.Empty, out type);
        }

        public static IReadOnlyList<KeyValuePair<string, Type>> CopyEntries()
        {
            var result = new List<KeyValuePair<string, Type>>(Types);
            result.Sort((left, right) => StringComparer.Ordinal.Compare(left.Key, right.Key));
            return result;
        }

        internal static void Register(ESAgentArtifactAttribute attribute, Type type)
        {
            if (attribute == null || type == null || type.IsAbstract || type.IsInterface)
                return;
            if (Types.TryGetValue(attribute.StableId, out Type existing))
            {
                if (existing == type)
                    return;
                throw new InvalidOperationException("Agent 产物稳定标识重复：" + attribute.StableId
                    + "，类型 " + existing.FullName + " 与 " + type.FullName + " 冲突。");
            }
            Types.Add(attribute.StableId, type);
        }
    }

    /// <summary>Lightweight, idempotent Agent artifact metadata registration.</summary>
    public sealed class ESAgentArtifactAttributeRegister
        : EditorRegister_FOR_ClassAttribute<ESAgentArtifactAttribute>
    {
        public override void Handle(ESAgentArtifactAttribute attribute, Type type)
        {
            ESAgentArtifactTypeRegistry.Register(attribute, type);
        }
    }

    /// <summary>
    /// Unified Editor-only authoring graph. AICommand and AISkill are output capabilities of the
    /// same requirement graph, not separate ScriptableObject lifecycles.
    /// </summary>
    [ESGraphAssetDomain(ESAgentGraphStableIds.DomainId, editorOnly: true)]
    public sealed partial class ESAgentAuthoringGraphAsset : ESGraphAssetBase
    {
        public override ESGraphDomainKey DomainKey => ESAgentGraphStableIds.Domain;
        public override bool CanEnableCycles => false;

        protected override bool ValidateDomainConnection(ESGraphNodeRecord outputNode,
            ESGraphPortRecord outputPort, ESGraphNodeRecord inputNode,
            ESGraphPortRecord inputPort, out string error)
        {
            if (ESAgentRelationSemantics.TryResolve(outputNode?.typeId, inputNode?.typeId,
                    outputPort?.stableKey, out ESAgentRelationKind relationKind))
            {
                if (relationKind == ESAgentRelationKind.ExecutesNext
                    && (!string.Equals(outputPort.valueTypeId, ESAgentGraphStableIds.SkillControlPort,
                            StringComparison.Ordinal)
                        || !string.Equals(inputPort.valueTypeId, ESAgentGraphStableIds.SkillControlPort,
                            StringComparison.Ordinal)))
                {
                    error = "AISkill 控制流只能连接到控制输入。";
                    return false;
                }
                if (relationKind == ESAgentRelationKind.BindsValue
                    && (string.Equals(outputPort.valueTypeId, ESAgentGraphStableIds.SkillControlPort,
                            StringComparison.Ordinal)
                        || string.Equals(inputPort.valueTypeId, ESAgentGraphStableIds.SkillControlPort,
                            StringComparison.Ordinal)))
                {
                    error = "AISkill 值流不能连接到控制端口。";
                    return false;
                }
                error = null;
                return true;
            }
            error = "当前 AI 节点阶段不允许该关系：" + (outputNode?.title ?? "输出节点")
                + " → " + (inputNode?.title ?? "输入节点");
            return false;
        }
    }
#endif
}
