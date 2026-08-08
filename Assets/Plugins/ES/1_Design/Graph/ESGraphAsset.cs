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

        public static ESGraphValidationIssue Error(string code, string message, string elementId = null)
        {
            return new ESGraphValidationIssue
            {
                severity = ESGraphValidationSeverity.Error,
                code = code,
                message = message,
                elementId = elementId
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
    /// Stable serialized graph authority. GraphView is only an editor projection of this asset.
    /// Runtime systems must bake a validated immutable snapshot instead of executing this asset.
    /// </summary>
    [CreateAssetMenu(fileName = "ESGraphAsset", menuName = "【ES】/内容制作/图与流程/稳定图资产 V2")]
    public sealed class ESGraphAsset : ESSO
    {
        public const int CurrentSchemaVersion = 2;
        public const int MinimumSupportedSchemaVersion = 1;

        [Min(1)] public int schemaVersion = CurrentSchemaVersion;
        [SerializeField] private string graphId = ESGraphIdentity.NewId();
        [SerializeField] private string originGraphId = string.Empty;
        [SerializeField] private string domainId = ESGraphDomainIds.Generic;
        public bool allowCycles;
        [SerializeField] private List<ESGraphNodeRecord> nodes = new List<ESGraphNodeRecord>();
        [SerializeField] private List<ESGraphEdgeRecord> edges = new List<ESGraphEdgeRecord>();

        public string GraphId => graphId ?? string.Empty;
        public string OriginGraphId => originGraphId ?? string.Empty;
        public string DomainId => string.IsNullOrWhiteSpace(domainId) ? ESGraphDomainIds.Generic : domainId;
        public ESGraphDomainKind DomainKind => ESGraphDomainCatalog.GetKind(DomainId);
        public ESGraphDomainKey DomainKey => ESGraphDomainKey.Parse(DomainId);
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

        public bool TrySetDomain(ESGraphDomainKey value, out string error)
        {
            return TrySetDomainId(value.StableId, out error);
        }

        public bool TrySetDomainId(string value, out string error)
        {
            EnsureCollections();
            value = value?.Trim();
            if (!ESGraphStableIdUtility.IsValid(value))
            {
                error = "DomainId 必须以小写英文字母开头，且只能包含小写字母、数字、点、横线和下划线。";
                return false;
            }
            if (string.Equals(DomainId, value, StringComparison.Ordinal))
            {
                error = null;
                return true;
            }
            if (nodes.Count > 0 || edges.Count > 0)
            {
                error = "已有内容的 Graph 不能直接切换 DomainId；必须通过领域迁移器处理。";
                return false;
            }
            domainId = value;
            schemaVersion = CurrentSchemaVersion;
            error = null;
            return true;
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
#endif

        public bool CanConnect(string firstPortId, string secondPortId, string ignoredEdgeId, out string error)
        {
            EnsureCollections();
            error = null;
            if (!TryFindPort(firstPortId, out ESGraphNodeRecord firstNode, out ESGraphPortRecord firstPort)
                || !TryFindPort(secondPortId, out ESGraphNodeRecord secondNode, out ESGraphPortRecord secondPort))
            {
                error = "连接端口不存在。";
                return false;
            }

            ESGraphNodeRecord outputNode;
            ESGraphNodeRecord inputNode;
            ESGraphPortRecord output;
            ESGraphPortRecord input;
            if (firstPort.direction == ESGraphPortDirection.Output && secondPort.direction == ESGraphPortDirection.Input)
            {
                outputNode = firstNode;
                output = firstPort;
                inputNode = secondNode;
                input = secondPort;
            }
            else if (secondPort.direction == ESGraphPortDirection.Output && firstPort.direction == ESGraphPortDirection.Input)
            {
                outputNode = secondNode;
                output = secondPort;
                inputNode = firstNode;
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

            int outputConnections = 0;
            int inputConnections = 0;
            for (int i = 0; i < edges.Count; i++)
            {
                ESGraphEdgeRecord existing = edges[i];
                if (existing == null || string.Equals(existing.edgeId, ignoredEdgeId, StringComparison.Ordinal))
                    continue;
                if (string.Equals(existing.outputPortId, output.portId, StringComparison.Ordinal)
                    && string.Equals(existing.inputPortId, input.portId, StringComparison.Ordinal))
                {
                    error = "不允许创建重复连线。";
                    return false;
                }
                if (string.Equals(existing.outputPortId, output.portId, StringComparison.Ordinal)) outputConnections++;
                if (string.Equals(existing.inputPortId, input.portId, StringComparison.Ordinal)) inputConnections++;
            }

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

            if (!allowCycles && WouldIntroduceCycle(outputNode.nodeId, inputNode.nodeId, ignoredEdgeId))
            {
                error = "当前图禁止循环。";
                return false;
            }

            return true;
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

            if (!allowCycles && ContainsCycle(nodeByPort))
                issues.Add(ESGraphValidationIssue.Error("Graph.Cycle.Forbidden", "当前图禁止循环，但检测到循环。"));
            return issues;
        }

        private bool WouldIntroduceCycle(string outputNodeId, string inputNodeId, string ignoredEdgeId)
        {
            if (string.Equals(outputNodeId, inputNodeId, StringComparison.Ordinal))
                return true;
            Dictionary<string, List<string>> adjacency = BuildAdjacency(ignoredEdgeId);
            if (!adjacency.TryGetValue(inputNodeId, out List<string> _))
                adjacency[inputNodeId] = new List<string>();
            HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal);
            Stack<string> stack = new Stack<string>();
            stack.Push(inputNodeId);
            while (stack.Count > 0)
            {
                string current = stack.Pop();
                if (!visited.Add(current))
                    continue;
                if (string.Equals(current, outputNodeId, StringComparison.Ordinal))
                    return true;
                if (!adjacency.TryGetValue(current, out List<string> next))
                    continue;
                for (int i = 0; i < next.Count; i++)
                    stack.Push(next[i]);
            }
            return false;
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

        private Dictionary<string, List<string>> BuildAdjacency(string ignoredEdgeId)
        {
            Dictionary<string, string> nodeByPort = new Dictionary<string, string>(StringComparer.Ordinal);
            Dictionary<string, List<string>> adjacency = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            for (int i = 0; i < nodes.Count; i++)
            {
                ESGraphNodeRecord node = nodes[i];
                if (node == null || string.IsNullOrEmpty(node.nodeId))
                    continue;
                adjacency[node.nodeId] = new List<string>();
                if (node.ports == null)
                    continue;
                for (int p = 0; p < node.ports.Count; p++)
                {
                    ESGraphPortRecord port = node.ports[p];
                    if (port != null && !string.IsNullOrEmpty(port.portId))
                        nodeByPort[port.portId] = node.nodeId;
                }
            }
            for (int i = 0; i < edges.Count; i++)
            {
                ESGraphEdgeRecord edge = edges[i];
                if (edge == null || string.Equals(edge.edgeId, ignoredEdgeId, StringComparison.Ordinal)
                    || !nodeByPort.TryGetValue(edge.outputPortId ?? string.Empty, out string from)
                    || !nodeByPort.TryGetValue(edge.inputPortId ?? string.Empty, out string to))
                    continue;
                adjacency[from].Add(to);
            }
            return adjacency;
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

        private void OnValidate()
        {
            EnsureCollections();
            EnsureGraphIdentity();
            if (schemaVersion < 1)
                schemaVersion = CurrentSchemaVersion;
        }
    }
}
