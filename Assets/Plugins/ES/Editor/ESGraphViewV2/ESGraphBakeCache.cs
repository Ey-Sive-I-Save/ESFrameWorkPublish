using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using GraphAsset = global::ES.ESGraphAssetBase;

namespace ES.EditorInternal
{
    /// <summary>
    /// Editor-only cache for successful immutable bake results. Cache hits are guarded by an exact,
    /// allocation-free comparison against the current authoring content; node positions are excluded
    /// because they are not part of the Graph content signature.
    /// </summary>
    internal static class ESGraphBakeCache
    {
        private sealed class CacheState
        {
            public CacheEntry strict;
            public CacheEntry forceable;
        }

        private sealed class CacheEntry
        {
            public ESBakedGraphSnapshot snapshot;
            public IESBakedGraphPlan plan;
            public List<ESGraphValidationIssue> issues;
        }

        private static readonly ConditionalWeakTable<GraphAsset, CacheState> States =
            new ConditionalWeakTable<GraphAsset, CacheState>();

        public static void NotifyChanged(GraphAsset asset, ESGraphChange change)
        {
            if (asset == null || !change.AffectsBake)
                return;
            Invalidate(asset);
        }

        public static void Invalidate(GraphAsset asset)
        {
            if (asset == null || !States.TryGetValue(asset, out CacheState state))
                return;
            state.strict = null;
            state.forceable = null;
        }

        public static bool TryGet(GraphAsset asset, bool acceptForceableErrors,
            out ESBakedGraphSnapshot snapshot, out IESBakedGraphPlan plan,
            out List<ESGraphValidationIssue> issues)
        {
            snapshot = null;
            plan = null;
            issues = null;
            if (asset == null || !States.TryGetValue(asset, out CacheState state))
                return false;

            CacheEntry entry = acceptForceableErrors ? state.forceable ?? state.strict : state.strict;
            if (entry == null || !MatchesCurrentContent(asset, entry.snapshot))
            {
                state.strict = null;
                state.forceable = null;
                return false;
            }

            snapshot = entry.snapshot;
            plan = entry.plan;
            issues = entry.issues != null
                ? new List<ESGraphValidationIssue>(entry.issues)
                : new List<ESGraphValidationIssue>();
            return true;
        }

        public static void Store(GraphAsset asset, bool acceptForceableErrors,
            ESBakedGraphSnapshot snapshot, IESBakedGraphPlan plan,
            IReadOnlyList<ESGraphValidationIssue> issues)
        {
            if (asset == null || snapshot == null || !MatchesCurrentContent(asset, snapshot))
                return;
            CacheState state = States.GetValue(asset, _ => new CacheState());
            var entry = new CacheEntry
            {
                snapshot = snapshot,
                plan = plan,
                issues = issues != null
                    ? new List<ESGraphValidationIssue>(issues)
                    : new List<ESGraphValidationIssue>()
            };
            if (acceptForceableErrors)
                state.forceable = entry;
            else
                state.strict = entry;
        }

        private static bool MatchesCurrentContent(GraphAsset asset, ESBakedGraphSnapshot snapshot)
        {
            if (asset == null || snapshot == null
                || asset.schemaVersion != snapshot.SchemaVersion
                || asset.AllowsCycles != snapshot.AllowCycles
                || !string.Equals(asset.GraphId, snapshot.GraphId, StringComparison.Ordinal)
                || !string.Equals(asset.OriginGraphId, snapshot.OriginGraphId, StringComparison.Ordinal)
                || !string.Equals(asset.DomainId, snapshot.DomainId, StringComparison.Ordinal)
                || asset.Nodes.Count != snapshot.Nodes.Count
                || asset.Edges.Count != snapshot.Edges.Count)
                return false;

            for (int i = 0; i < asset.Nodes.Count; i++)
            {
                ESGraphNodeRecord node = asset.Nodes[i];
                if (node == null || !snapshot.TryGetNode(node.nodeId, out ESGraphNodeSnapshot baked)
                    || node.version != baked.Version
                    || !string.Equals(node.typeId, baked.TypeId, StringComparison.Ordinal)
                    || !string.Equals(node.title, baked.Title, StringComparison.Ordinal)
                    || !string.Equals(node.payloadJson ?? string.Empty,
                        baked.PayloadJson ?? string.Empty, StringComparison.Ordinal))
                    return false;

                int portCount = node.ports?.Count ?? 0;
                if (portCount != baked.Ports.Count)
                    return false;
                for (int p = 0; p < portCount; p++)
                {
                    ESGraphPortRecord port = node.ports[p];
                    if (port == null || !snapshot.TryGetPort(port.portId, out ESGraphPortSnapshot bakedPort)
                        || port.direction != bakedPort.Direction
                        || port.capacity != bakedPort.Capacity
                        || ESGraphPortAggregationRules.Resolve(port.direction, port.capacity,
                            port.aggregation)
                            != bakedPort.Aggregation
                        || !string.Equals(port.stableKey, bakedPort.StableKey, StringComparison.Ordinal)
                        || !string.Equals(port.name, bakedPort.Name, StringComparison.Ordinal)
                        || !string.Equals(port.meaning, bakedPort.Meaning, StringComparison.Ordinal)
                        || !string.Equals(port.valueTypeId, bakedPort.ValueTypeId, StringComparison.Ordinal))
                        return false;
                }
            }

            for (int i = 0; i < asset.Edges.Count; i++)
            {
                ESGraphEdgeRecord edge = asset.Edges[i];
                if (edge == null || !snapshot.TryGetEdge(edge.edgeId, out ESGraphEdgeSnapshot baked)
                    || !string.Equals(edge.outputPortId, baked.OutputPortId, StringComparison.Ordinal)
                    || !string.Equals(edge.inputPortId, baked.InputPortId, StringComparison.Ordinal)
                    || edge.order != baked.Order)
                    return false;
            }
            return true;
        }
    }
}
