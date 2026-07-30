using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ES.EditorInternal
{
    /// <summary>Statistics produced by one serialized dependency expansion.</summary>
    public readonly struct ESSerializedDependencyStatistics
    {
        public readonly int RootCount;
        public readonly int VisitedObjectCount;
        public readonly int VisitedPropertyCount;
        public readonly int MatchedPropertyCount;
        public readonly int DependencyCount;
        public readonly int TraversalCount;
        public readonly int CycleOrDuplicateCount;
        public readonly int MaximumDepth;

        internal ESSerializedDependencyStatistics(
            int rootCount,
            int visitedObjectCount,
            int visitedPropertyCount,
            int matchedPropertyCount,
            int dependencyCount,
            int traversalCount,
            int cycleOrDuplicateCount,
            int maximumDepth)
        {
            RootCount = rootCount;
            VisitedObjectCount = visitedObjectCount;
            VisitedPropertyCount = visitedPropertyCount;
            MatchedPropertyCount = matchedPropertyCount;
            DependencyCount = dependencyCount;
            TraversalCount = traversalCount;
            CycleOrDuplicateCount = cycleOrDuplicateCount;
            MaximumDepth = maximumDepth;
        }
    }

    /// <summary>Dependencies and diagnostics returned by the detailed expansion API.</summary>
    public sealed class ESSerializedDependencyExpansion<TDependency>
    {
        public List<TDependency> Dependencies { get; }
        public ESSerializedDependencyStatistics Statistics { get; }

        internal ESSerializedDependencyExpansion(
            List<TDependency> dependencies,
            ESSerializedDependencyStatistics statistics)
        {
            Dependencies = dependencies;
            Statistics = statistics;
        }
    }

    /// <summary>Reusable traversal settings. Keep the callback allocation-free when possible.</summary>
    public sealed class ESSerializedDependencyOptions
    {
        public int MaxDepth = 4;
        public int ResultCapacity = 32;
        public int NodeCapacity = 16;
        public Func<bool> IsCancellationRequested;
    }

    /// <summary>
    /// A rule result for one serialized property. Rules may emit a dependency, continue into
    /// another ScriptableObject, do both, or simply consume the property.
    /// </summary>
    public readonly struct ESSerializedDependencyVisit<TDependency>
    {
        public readonly bool HasDependency;
        public readonly TDependency Dependency;
        public readonly ScriptableObject NextRoot;
        public readonly string EdgeDescription;
        public readonly bool VisitChildren;

        public ESSerializedDependencyVisit(
            bool hasDependency,
            TDependency dependency,
            ScriptableObject nextRoot,
            string edgeDescription,
            bool visitChildren = false)
        {
            HasDependency = hasDependency;
            Dependency = dependency;
            NextRoot = nextRoot;
            EdgeDescription = edgeDescription ?? string.Empty;
            VisitChildren = visitChildren;
        }

        public static ESSerializedDependencyVisit<TDependency> Consume()
            => default;

        public static ESSerializedDependencyVisit<TDependency> Emit(TDependency dependency)
            => new ESSerializedDependencyVisit<TDependency>(true, dependency, null, null);

        public static ESSerializedDependencyVisit<TDependency> ContinueChildren()
            => new ESSerializedDependencyVisit<TDependency>(false, default, null, null, true);

        public static ESSerializedDependencyVisit<TDependency> EmitAndContinueChildren(TDependency dependency)
            => new ESSerializedDependencyVisit<TDependency>(true, dependency, null, null, true);

        public static ESSerializedDependencyVisit<TDependency> Traverse(
            ScriptableObject nextRoot,
            string edgeDescription)
            => new ESSerializedDependencyVisit<TDependency>(false, default, nextRoot, edgeDescription);

        public static ESSerializedDependencyVisit<TDependency> EmitAndTraverse(
            TDependency dependency,
            ScriptableObject nextRoot,
            string edgeDescription)
            => new ESSerializedDependencyVisit<TDependency>(true, dependency, nextRoot, edgeDescription);
    }

    public delegate ESSerializedDependencyVisit<TDependency> ESSerializedDependencyRule<TContext, TDependency>(
        TContext context,
        ScriptableObject root,
        SerializedProperty property,
        int depth,
        string traversalPath);

    /// <summary>
    /// Validated, read-only rule table. Build it once and share it across all expansion calls.
    /// </summary>
    public sealed class ESSerializedDependencyRuleSet<TContext, TDependency>
    {
        internal readonly Dictionary<string, ESSerializedDependencyRule<TContext, TDependency>> Rules;
        public int Count => Rules.Count;

        private ESSerializedDependencyRuleSet(
            Dictionary<string, ESSerializedDependencyRule<TContext, TDependency>> rules)
        {
            Rules = rules;
        }

        public static Builder CreateBuilder(int capacity = 16)
            => new Builder(capacity);

        public sealed class Builder
        {
            private readonly Dictionary<string, ESSerializedDependencyRule<TContext, TDependency>> rules;

            internal Builder(int capacity)
            {
                rules = new Dictionary<string, ESSerializedDependencyRule<TContext, TDependency>>(
                    Math.Max(0, capacity),
                    StringComparer.Ordinal);
            }

            public Builder Add<TSerialized>(ESSerializedDependencyRule<TContext, TDependency> rule)
                => Add(typeof(TSerialized).Name, rule);

            public Builder Add(
                string serializedPropertyType,
                ESSerializedDependencyRule<TContext, TDependency> rule)
            {
                if (string.IsNullOrWhiteSpace(serializedPropertyType))
                    throw new ArgumentException("Serialized property type cannot be empty.", nameof(serializedPropertyType));
                if (rule == null)
                    throw new ArgumentNullException(nameof(rule));
                if (rules.ContainsKey(serializedPropertyType))
                    throw new InvalidOperationException(
                        "[ES][SerializedDependency] Duplicate rule for serialized type: " + serializedPropertyType);
                rules.Add(serializedPropertyType, rule);
                return this;
            }

            public ESSerializedDependencyRuleSet<TContext, TDependency> Build()
                => new ESSerializedDependencyRuleSet<TContext, TDependency>(
                    new Dictionary<string, ESSerializedDependencyRule<TContext, TDependency>>(rules, StringComparer.Ordinal));
        }
    }

    /// <summary>
    /// Preconfigured reusable entry point. Editor tools normally keep one static pipeline and
    /// only supply roots plus their per-run context.
    /// </summary>
    public sealed class ESSerializedDependencyPipeline<TContext, TDependency>
    {
        private readonly ESSerializedDependencyRuleSet<TContext, TDependency> rules;
        private readonly ESSerializedDependencyOptions options;

        public ESSerializedDependencyPipeline(
            ESSerializedDependencyRuleSet<TContext, TDependency> rules,
            ESSerializedDependencyOptions options = null)
        {
            this.rules = rules ?? throw new ArgumentNullException(nameof(rules));
            this.options = options ?? new ESSerializedDependencyOptions();
        }

        public List<TDependency> Expand(IEnumerable<ScriptableObject> roots, TContext context)
            => ESSerializedDependencyExpander.Expand(roots, rules, context, options);

        public ESSerializedDependencyExpansion<TDependency> ExpandDetailed(
            IEnumerable<ScriptableObject> roots,
            TContext context)
            => ESSerializedDependencyExpander.ExpandDetailed(roots, rules, context, options);
    }

    /// <summary>
    /// Generic serialized dependency graph walker. Consumers provide exact property-type
    /// rules; unmatched properties perform one dictionary lookup and no delegate dispatch.
    /// </summary>
    public static class ESSerializedDependencyExpander
    {
        private readonly struct PendingRoot
        {
            public readonly ScriptableObject Root;
            public readonly int Depth;
            public readonly string TraversalPath;

            public PendingRoot(ScriptableObject root, int depth, string traversalPath)
            {
                Root = root;
                Depth = depth;
                TraversalPath = traversalPath ?? string.Empty;
            }
        }

        public static List<TDependency> Expand<TContext, TDependency>(
            IEnumerable<ScriptableObject> roots,
            Dictionary<string, ESSerializedDependencyRule<TContext, TDependency>> rulesBySerializedType,
            TContext context,
            int maxDepth,
            int resultCapacity = 32,
            int nodeCapacity = 16)
            => ExpandDetailed(
                roots,
                rulesBySerializedType,
                context,
                new ESSerializedDependencyOptions
                {
                    MaxDepth = maxDepth,
                    ResultCapacity = resultCapacity,
                    NodeCapacity = nodeCapacity
                }).Dependencies;

        public static List<TDependency> Expand<TContext, TDependency>(
            IEnumerable<ScriptableObject> roots,
            ESSerializedDependencyRuleSet<TContext, TDependency> rules,
            TContext context,
            ESSerializedDependencyOptions options = null)
            => ExpandDetailed(roots, rules, context, options).Dependencies;

        public static ESSerializedDependencyExpansion<TDependency> ExpandDetailed<TContext, TDependency>(
            IEnumerable<ScriptableObject> roots,
            ESSerializedDependencyRuleSet<TContext, TDependency> rules,
            TContext context,
            ESSerializedDependencyOptions options = null)
        {
            if (rules == null)
                throw new ArgumentNullException(nameof(rules));
            return ExpandDetailed(roots, rules.Rules, context, options);
        }

        public static ESSerializedDependencyExpansion<TDependency> ExpandDetailed<TContext, TDependency>(
            IEnumerable<ScriptableObject> roots,
            Dictionary<string, ESSerializedDependencyRule<TContext, TDependency>> rulesBySerializedType,
            TContext context,
            ESSerializedDependencyOptions options = null)
        {
            if (rulesBySerializedType == null)
                throw new ArgumentNullException(nameof(rulesBySerializedType));
            options = options ?? new ESSerializedDependencyOptions();
            if (options.MaxDepth < 0)
                throw new ArgumentOutOfRangeException(nameof(options.MaxDepth));
            if (options.ResultCapacity < 0)
                throw new ArgumentOutOfRangeException(nameof(options.ResultCapacity));
            if (options.NodeCapacity < 0)
                throw new ArgumentOutOfRangeException(nameof(options.NodeCapacity));

            var result = new List<TDependency>(Math.Max(0, options.ResultCapacity));
            var pending = new List<PendingRoot>(Math.Max(0, options.NodeCapacity));
            var visited = new HashSet<string>(Math.Max(0, options.NodeCapacity), StringComparer.Ordinal);
            var identityCache = new Dictionary<int, string>(Math.Max(0, options.NodeCapacity));
            int rootCount = 0;
            int visitedPropertyCount = 0;
            int matchedPropertyCount = 0;
            int traversalCount = 0;
            int cycleOrDuplicateCount = 0;
            int maximumDepth = 0;

            if (roots != null)
                foreach (ScriptableObject root in roots)
                    if (root != null && visited.Add(GetCachedIdentity(root, identityCache)))
                    {
                        pending.Add(new PendingRoot(root, 0, string.Empty));
                        rootCount++;
                    }

            for (int rootIndex = 0; rootIndex < pending.Count; rootIndex++)
            {
                ThrowIfCancellationRequested(options);
                PendingRoot current = pending[rootIndex];
                if (current.Depth > maximumDepth)
                    maximumDepth = current.Depth;
                var serialized = new SerializedObject(current.Root);
                SerializedProperty iterator = serialized.GetIterator();
                bool enterChildren = true;
                while (iterator.Next(enterChildren))
                {
                    visitedPropertyCount++;
                    if ((visitedPropertyCount & 255) == 0)
                        ThrowIfCancellationRequested(options);
                    enterChildren = true;
                    if (!TryGetRule(rulesBySerializedType, iterator, out ESSerializedDependencyRule<TContext, TDependency> rule))
                        continue;

                    matchedPropertyCount++;
                    // A matched rule owns the complete property. Its internal serialized fields
                    // are implementation details and must not be visited as independent nodes.
                    enterChildren = false;
                    ESSerializedDependencyVisit<TDependency> visit;
                    try
                    {
                        visit = rule(context, current.Root, iterator, current.Depth, current.TraversalPath);
                    }
                    catch (Exception exception) when (!(exception is OperationCanceledException))
                    {
                        throw new InvalidOperationException(
                            "[ES][SerializedDependency] Rule failed: " + current.Root.name + "." + iterator.propertyPath
                            + " (Type=" + iterator.type + ", Depth=" + current.Depth + ")",
                            exception);
                    }
                    enterChildren = visit.VisitChildren;

                    if (visit.HasDependency)
                        result.Add(visit.Dependency);

                    ScriptableObject nextRoot = visit.NextRoot;
                    if (nextRoot == null)
                        continue;

                    string identity = GetCachedIdentity(nextRoot, identityCache);
                    if (visited.Contains(identity))
                    {
                        cycleOrDuplicateCount++;
                        continue;
                    }
                    if (current.Depth >= options.MaxDepth)
                        throw new InvalidOperationException(
                            "[ES][SerializedDependency] 依赖穿透超过最大深度 " + options.MaxDepth + "："
                            + BuildTraversalPath(current.TraversalPath, visit.EdgeDescription, nextRoot));

                    visited.Add(identity);
                    traversalCount++;
                    pending.Add(new PendingRoot(
                        nextRoot,
                        current.Depth + 1,
                        BuildTraversalPath(current.TraversalPath, visit.EdgeDescription, nextRoot)));
                }
            }

            return new ESSerializedDependencyExpansion<TDependency>(
                result,
                new ESSerializedDependencyStatistics(
                    rootCount,
                    pending.Count,
                    visitedPropertyCount,
                    matchedPropertyCount,
                    result.Count,
                    traversalCount,
                    cycleOrDuplicateCount,
                    maximumDepth));
        }

        public static string GetStableObjectIdentity(UnityEngine.Object asset)
        {
            if (asset != null
                && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long localFileId)
                && !string.IsNullOrEmpty(guid))
                return guid + ":" + localFileId;
            return "instance:" + (asset != null ? asset.GetInstanceID() : 0);
        }

        private static string GetCachedIdentity(
            UnityEngine.Object asset,
            Dictionary<int, string> identityCache)
        {
            int instanceId = asset != null ? asset.GetInstanceID() : 0;
            if (identityCache.TryGetValue(instanceId, out string identity))
                return identity;

            identity = GetStableObjectIdentity(asset);
            identityCache.Add(instanceId, identity);
            return identity;
        }

        private static string BuildTraversalPath(
            string currentPath,
            string edgeDescription,
            ScriptableObject dependency)
        {
            string step = (edgeDescription ?? string.Empty) + " -> " + dependency.name;
            return string.IsNullOrEmpty(currentPath) ? step : currentPath + " -> " + step;
        }

        private static void ThrowIfCancellationRequested(ESSerializedDependencyOptions options)
        {
            if (options.IsCancellationRequested != null && options.IsCancellationRequested())
                throw new OperationCanceledException("Serialized dependency expansion was cancelled.");
        }

        private static bool TryGetRule<TContext, TDependency>(
            Dictionary<string, ESSerializedDependencyRule<TContext, TDependency>> rules,
            SerializedProperty property,
            out ESSerializedDependencyRule<TContext, TDependency> rule)
        {
            if (rules.TryGetValue(property.type, out rule))
                return true;
            if (property.propertyType != SerializedPropertyType.ManagedReference)
                return false;

            string fullTypeName = property.managedReferenceFullTypename;
            if (string.IsNullOrEmpty(fullTypeName))
                return false;

            int assemblySeparator = fullTypeName.IndexOf(' ');
            string qualifiedTypeName = assemblySeparator >= 0
                ? fullTypeName.Substring(assemblySeparator + 1)
                : fullTypeName;
            if (rules.TryGetValue(qualifiedTypeName, out rule))
                return true;

            int namespaceSeparator = qualifiedTypeName.LastIndexOf('.');
            string simpleTypeName = namespaceSeparator >= 0
                ? qualifiedTypeName.Substring(namespaceSeparator + 1)
                : qualifiedTypeName;
            return rules.TryGetValue(simpleTypeName, out rule);
        }
    }
}
