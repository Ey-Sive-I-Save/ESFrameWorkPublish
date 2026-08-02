using System;
using System.Collections.Generic;

namespace ES
{
    public static class ESStoryDefinitionCatalog
    {
        private static readonly ESStoryConfigKeyTable table = new ESStoryConfigKeyTable();
        public static ESStoryConfigKeyTable Table => table;

        public static void Inject(ESStoryDefinitionDataInfo info)
        {
            if (info == null) throw new ArgumentNullException(nameof(info));
            if (!ESStoryDefinitionSnapshot.TryBake(info, out ESStoryDefinitionSnapshot snapshot, out string error))
                throw new InvalidOperationException("Story 定义无效：" + info.name + "，" + error);

            bool ownsBuild = !table.IsBuilding;
            if (ownsBuild) table.BeginBuild();
            try
            {
                if (table.TryGet(info.definitionId, out ESStoryDefinitionRuntimeData existing))
                {
                    if (existing.contentVersion == snapshot.ContentVersion
                        && string.Equals(existing.contentSignature, snapshot.ContentSignature, StringComparison.Ordinal))
                        return;
                    throw new InvalidOperationException("同一 DefinitionId 注入了不同版本或签名：" + snapshot.DefinitionId);
                }

                ESStoryDefinitionRuntimeData data = table.AcquireRetained(info.definitionId);
                try
                {
                    data.definitionId = snapshot.DefinitionId;
                    data.contentVersion = snapshot.ContentVersion;
                    data.contentSignature = snapshot.ContentSignature;
                    data.snapshot = snapshot;
                    if (table.CommitRetained(info.definitionId, data, info.name) == 0)
                        throw new InvalidOperationException("Story Definition Catalog 注入失败：" + snapshot.DefinitionId);
                }
                catch
                {
                    table.AbandonRetained(data);
                    throw;
                }
            }
            finally
            {
                if (ownsBuild) table.EndBuild();
            }
        }

        public static bool TryResolve(ESStoryConfigKey key, int contentVersion, string contentSignature, out ESStoryDefinitionSnapshot snapshot)
        {
            snapshot = null;
            if (key == null || !table.TryGet(key, out ESStoryDefinitionRuntimeData data) || data?.snapshot == null)
                return false;
            if (data.contentVersion != contentVersion
                || !string.Equals(data.contentSignature, contentSignature, StringComparison.Ordinal))
                return false;
            snapshot = data.snapshot;
            return true;
        }
    }

    public static class ESStoryDefinitionValidator
    {
        public static List<ESStoryValidationIssue> Validate(ESStoryDefinitionDataInfo definition)
        {
            List<ESStoryValidationIssue> issues = new List<ESStoryValidationIssue>();
            if (definition == null)
            {
                Add(issues, ESStoryValidationSeverity.Error, "Definition.Null", "Definition 不能为空。", null);
                return issues;
            }
            if (definition.definitionId == null || string.IsNullOrWhiteSpace(definition.definitionId.StringKey))
                Add(issues, ESStoryValidationSeverity.Error, "Definition.Id", "DefinitionId.StringKey 不能为空。", null);
            if (definition.definitionId != null && definition.definitionId.HasEnumKey)
                Add(issues, ESStoryValidationSeverity.Error, "Definition.EnumId", "切片 A 的 Story 身份只允许稳定 StringKey。", null);
            if (definition.contentVersion < 1)
                Add(issues, ESStoryValidationSeverity.Error, "Definition.Version", "ContentVersion 必须大于 0。", null);
            if (definition.storyKind == ESStoryKind.Story)
                Add(issues, ESStoryValidationSeverity.Error, "Definition.KindUnsupported", "切片 A 尚未提供长期 Story 专用进度记录。", null);
            if (definition.nodes == null || definition.nodes.Count == 0)
            {
                Add(issues, ESStoryValidationSeverity.Error, "Graph.Empty", "至少需要一个节点。", null);
                return issues;
            }

            Dictionary<string, ESStoryNodeDefinition> byId = new Dictionary<string, ESStoryNodeDefinition>(StringComparer.Ordinal);
            for (int i = 0; i < definition.nodes.Count; i++)
            {
                ESStoryNodeDefinition node = definition.nodes[i];
                if (node == null) { Add(issues, ESStoryValidationSeverity.Error, "Node.Null", "节点不能为空。", null); continue; }
                if (string.IsNullOrWhiteSpace(node.nodeId)) { Add(issues, ESStoryValidationSeverity.Error, "Node.Id", "NodeId 不能为空。", null); continue; }
                if (!byId.TryAdd(node.nodeId, node)) Add(issues, ESStoryValidationSeverity.Error, "Node.Duplicate", "重复 NodeId：" + node.nodeId, node.nodeId);
            }

            if (string.IsNullOrWhiteSpace(definition.entryNodeId) || !byId.ContainsKey(definition.entryNodeId))
                Add(issues, ESStoryValidationSeverity.Error, "Graph.Entry", "EntryNodeId 不存在。", definition.entryNodeId);

            foreach (KeyValuePair<string, ESStoryNodeDefinition> pair in byId)
                ValidateNode(pair.Value, byId, issues);

            if (byId.ContainsKey(definition.entryNodeId))
            {
                HashSet<string> reachable = new HashSet<string>(StringComparer.Ordinal);
                Visit(definition.entryNodeId, byId, reachable);
                foreach (string nodeId in byId.Keys)
                    if (!reachable.Contains(nodeId)) Add(issues, ESStoryValidationSeverity.Error, "Graph.Unreachable", "节点不可从入口到达。", nodeId);
                DetectCycles(definition.entryNodeId, byId, issues);
            }
            return issues;
        }

        private static void ValidateNode(ESStoryNodeDefinition node, Dictionary<string, ESStoryNodeDefinition> byId, List<ESStoryValidationIssue> issues)
        {
            if (node.nodeKind == ESStoryNodeKind.Start || node.nodeKind == ESStoryNodeKind.Dialogue || node.nodeKind == ESStoryNodeKind.Action)
                RequireTarget(node.nextNodeId, "NextNode", node.nodeId, byId, issues);
            if (node.nodeKind == ESStoryNodeKind.Condition)
            {
                RequireTarget(node.trueNodeId, "TrueNode", node.nodeId, byId, issues);
                RequireTarget(node.falseNodeId, "FalseNode", node.nodeId, byId, issues);
            }
            if (node.nodeKind == ESStoryNodeKind.Action)
            {
                if (string.IsNullOrWhiteSpace(node.actionId)) Add(issues, ESStoryValidationSeverity.Error, "Action.Id", "ActionId 不能为空。", node.nodeId);
                if (node.setTag.IsEmpty) Add(issues, ESStoryValidationSeverity.Error, "Action.Tag", "SetTag 必须选择稳定 Tag。", node.nodeId);
            }
            if (node.nodeKind != ESStoryNodeKind.Choice) return;
            if (node.options == null || node.options.Count == 0) Add(issues, ESStoryValidationSeverity.Error, "Choice.Empty", "Choice 至少需要一个选项。", node.nodeId);
            HashSet<string> optionIds = new HashSet<string>(StringComparer.Ordinal);
            int count = node.options != null ? node.options.Count : 0;
            for (int i = 0; i < count; i++)
            {
                ESStoryOptionDefinition option = node.options[i];
                if (option == null || string.IsNullOrWhiteSpace(option.optionId)) { Add(issues, ESStoryValidationSeverity.Error, "Option.Id", "OptionId 不能为空。", node.nodeId); continue; }
                if (!optionIds.Add(option.optionId)) Add(issues, ESStoryValidationSeverity.Error, "Option.Duplicate", "重复 OptionId：" + option.optionId, node.nodeId);
                RequireTarget(option.nextNodeId, "Option.NextNode", node.nodeId, byId, issues);
            }
        }

        private static void RequireTarget(string target, string code, string nodeId, Dictionary<string, ESStoryNodeDefinition> byId, List<ESStoryValidationIssue> issues)
        {
            if (string.IsNullOrWhiteSpace(target) || !byId.ContainsKey(target)) Add(issues, ESStoryValidationSeverity.Error, code, "目标节点不存在：" + target, nodeId);
        }

        private static IEnumerable<string> Outgoing(ESStoryNodeDefinition node)
        {
            if (node.nodeKind == ESStoryNodeKind.Start || node.nodeKind == ESStoryNodeKind.Dialogue || node.nodeKind == ESStoryNodeKind.Action) yield return node.nextNodeId;
            else if (node.nodeKind == ESStoryNodeKind.Condition) { yield return node.trueNodeId; yield return node.falseNodeId; }
            else if (node.nodeKind == ESStoryNodeKind.Choice && node.options != null)
                for (int i = 0; i < node.options.Count; i++) if (node.options[i] != null) yield return node.options[i].nextNodeId;
        }

        private static void Visit(string id, Dictionary<string, ESStoryNodeDefinition> byId, HashSet<string> visited)
        {
            if (!visited.Add(id) || !byId.TryGetValue(id, out ESStoryNodeDefinition node)) return;
            foreach (string next in Outgoing(node)) Visit(next, byId, visited);
        }

        private static void DetectCycles(string entry, Dictionary<string, ESStoryNodeDefinition> byId, List<ESStoryValidationIssue> issues)
        {
            HashSet<string> active = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> done = new HashSet<string>(StringComparer.Ordinal);
            Detect(entry, byId, active, done, new List<string>(), issues);
        }

        private static void Detect(string id, Dictionary<string, ESStoryNodeDefinition> byId, HashSet<string> active, HashSet<string> done, List<string> path, List<ESStoryValidationIssue> issues)
        {
            if (done.Contains(id) || !byId.TryGetValue(id, out ESStoryNodeDefinition node)) return;
            if (active.Contains(id))
            {
                bool hasWait = false;
                for (int i = path.IndexOf(id); i >= 0 && i < path.Count; i++)
                {
                    ESStoryNodeKind kind = byId[path[i]].nodeKind;
                    if (kind == ESStoryNodeKind.Dialogue || kind == ESStoryNodeKind.Choice) { hasWait = true; break; }
                }
                Add(issues, hasWait ? ESStoryValidationSeverity.Info : ESStoryValidationSeverity.Warning,
                    hasWait ? "Graph.LoopAllowed" : "Graph.NoProgressLoop", hasWait ? "检测到包含等待节点的允许循环。" : "检测到可能无进展的同步循环，运行时步数门禁会阻断。", id);
                return;
            }
            active.Add(id); path.Add(id);
            foreach (string next in Outgoing(node)) Detect(next, byId, active, done, path, issues);
            path.RemoveAt(path.Count - 1); active.Remove(id); done.Add(id);
        }

        private static void Add(List<ESStoryValidationIssue> issues, ESStoryValidationSeverity severity, string code, string message, string nodeId)
            => issues.Add(new ESStoryValidationIssue { severity = severity, code = code, message = message, nodeId = nodeId });
    }
}
