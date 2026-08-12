using System;
using System.Collections.Generic;

namespace ES
{
    public enum ESGraphDomainKind : byte
    {
        Generic = 0,
        Story = 1,
        BehaviorTree = 2,
        // Value 3 was used by the old Agent enum API. It remains intentionally unassigned.
        Custom = byte.MaxValue
    }

    public enum ESGraphBuiltInNodeKind : byte
    {
        Custom = 0,
        GenericFlow = 1,
        GenericSource = 2,
        GenericSink = 3,
        GenericBranch = 4,
        GenericMerge = 5,
        StoryStart = 6,
        StoryDialogue = 7,
        StoryChoice = 8,
        StoryCondition = 9,
        StoryAction = 10,
        StoryComplete = 11,
        StoryFail = 12,
        BehaviorRoot = 13,
        BehaviorSequence = 14,
        BehaviorSelector = 15,
        BehaviorParallel = 16,
        BehaviorDecorator = 17,
        BehaviorCondition = 18,
        BehaviorAction = 19
        // Values 20-25 were used by the old Agent enum API and remain intentionally unassigned.
    }

    public enum ESGraphNodeCategory : byte
    {
        General,
        Entry,
        Exit,
        Flow,
        Branch,
        Merge,
        Dialogue,
        Choice,
        Condition,
        Action,
        Composite,
        Decorator,
        Reference,
        Constraint,
        Output,
        Validation,
        Custom = byte.MaxValue
    }

    public enum ESGraphNodeTheme : byte
    {
        Neutral = 0,
        Primary = 1,
        Entry = 2,
        Exit = 3,
        Success = 4,
        Failure = 5,
        Decision = 6,
        Merge = 7,
        Dialogue = 8,
        Composite = 9,
        Reference = 10,
        Constraint = 11,
#if UNITY_EDITOR
        // Reserved serialized numeric identities. Player builds do not expose these members.
        CommandOutput = 12,
        SkillOutput = 13,
#endif
        Validation = 14,
        Custom = byte.MaxValue
    }

    public enum ESGraphPortValueKind : byte
    {
        Custom = 0,
        Flow = 1,
        Any = 2,
        Boolean = 3,
        Number = 4,
        Text = 5,
        Object = 6
        // Values 7-9 were used by the old Agent enum API and remain intentionally unassigned.
    }

    /// <summary>
    /// Declares the immutable domain owned by a concrete Graph asset type. The attribute is metadata
    /// only; platform compilation boundaries remain the responsibility of asmdef or conditional code.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class ESGraphAssetDomainAttribute : Attribute
    {
        public string StableId { get; }
        public bool EditorOnly { get; }

        public ESGraphAssetDomainAttribute(string stableId, bool editorOnly = false)
        {
            if (!ESGraphStableIdUtility.IsValid(stableId))
                throw new ArgumentException("Graph 资产类型的 DomainId 非法。", nameof(stableId));
            StableId = stableId;
            EditorOnly = editorOnly;
        }
    }

    public static class ESGraphDomainIds
    {
        public const string Generic = "es.graph.generic";
        public const string Story = "es.story";
        public const string BehaviorTree = "es.behavior-tree";
    }

    public static class ESGraphNodeTypeIds
    {
        public const string GenericFlow = "es.graph.flow";
        public const string GenericSource = "es.graph.source";
        public const string GenericSink = "es.graph.sink";
        public const string GenericBranch = "es.graph.branch";
        public const string GenericMerge = "es.graph.merge";
        public const string StoryStart = "es.story.start";
        public const string StoryDialogue = "es.story.dialogue";
        public const string StoryChoice = "es.story.choice";
        public const string StoryCondition = "es.story.condition";
        public const string StoryAction = "es.story.action";
        public const string StoryComplete = "es.story.complete";
        public const string StoryFail = "es.story.fail";
        public const string BehaviorRoot = "es.behavior.root";
        public const string BehaviorSequence = "es.behavior.sequence";
        public const string BehaviorSelector = "es.behavior.selector";
        public const string BehaviorParallel = "es.behavior.parallel";
        public const string BehaviorDecorator = "es.behavior.decorator";
        public const string BehaviorCondition = "es.behavior.condition";
        public const string BehaviorAction = "es.behavior.action";
    }

    public static class ESGraphPortValueIds
    {
        public const string Flow = "flow";
        public const string Any = "*";
        public const string Boolean = "bool";
        public const string Number = "number";
        public const string Text = "text";
        public const string Object = "object";
    }

    public readonly struct ESGraphDomainKey : IEquatable<ESGraphDomainKey>
    {
        public ESGraphDomainKind Kind { get; }
        public string StableId { get; }
        public bool IsValid => ESGraphStableIdUtility.IsValid(StableId);

        private ESGraphDomainKey(ESGraphDomainKind kind, string stableId)
        {
            Kind = kind;
            StableId = stableId ?? string.Empty;
        }

        public static ESGraphDomainKey FromKind(ESGraphDomainKind kind)
        {
            if (kind == ESGraphDomainKind.Custom)
                throw new ArgumentException("Custom 图领域必须提供稳定标识。", nameof(kind));
            return new ESGraphDomainKey(kind, ESGraphDomainCatalog.GetStableId(kind));
        }

        public static ESGraphDomainKey Custom(string stableId)
        {
            stableId = stableId?.Trim();
            if (!ESGraphStableIdUtility.IsValid(stableId))
                throw new ArgumentException("自定义图领域稳定标识非法。", nameof(stableId));
            if (ESGraphDomainCatalog.GetKind(stableId) != ESGraphDomainKind.Custom)
                throw new ArgumentException("该稳定标识已被内置图领域占用。", nameof(stableId));
            return new ESGraphDomainKey(ESGraphDomainKind.Custom, stableId);
        }

        public static ESGraphDomainKey Parse(string stableId)
        {
            ESGraphDomainKind kind = ESGraphDomainCatalog.GetKind(stableId);
            if (!ESGraphStableIdUtility.IsValid(stableId))
                return new ESGraphDomainKey(ESGraphDomainKind.Custom, stableId);
            return kind == ESGraphDomainKind.Custom
                ? Custom(stableId)
                : FromKind(kind);
        }

        public bool Equals(ESGraphDomainKey other)
        {
            return string.Equals(StableId, other.StableId, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is ESGraphDomainKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return StableId == null ? 0 : StringComparer.Ordinal.GetHashCode(StableId);
        }

        public static bool operator ==(ESGraphDomainKey left, ESGraphDomainKey right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ESGraphDomainKey left, ESGraphDomainKey right)
        {
            return !left.Equals(right);
        }

        public override string ToString()
        {
            return StableId ?? string.Empty;
        }
    }

    public readonly struct ESGraphNodeTypeKey : IEquatable<ESGraphNodeTypeKey>
    {
        public ESGraphBuiltInNodeKind Kind { get; }
        public string StableId { get; }
        public bool IsBuiltIn => Kind != ESGraphBuiltInNodeKind.Custom;
        public bool IsValid => ESGraphStableIdUtility.IsValid(StableId);

        private ESGraphNodeTypeKey(ESGraphBuiltInNodeKind kind, string stableId)
        {
            Kind = kind;
            StableId = stableId ?? string.Empty;
        }

        public static ESGraphNodeTypeKey FromKind(ESGraphBuiltInNodeKind kind)
        {
            if (kind == ESGraphBuiltInNodeKind.Custom)
                throw new ArgumentException("Custom 节点类型必须提供稳定标识。", nameof(kind));
            return new ESGraphNodeTypeKey(kind, ESGraphNodeTypeCatalog.GetStableId(kind));
        }

        public static ESGraphNodeTypeKey Custom(string stableId)
        {
            stableId = stableId?.Trim();
            if (!ESGraphStableIdUtility.IsValid(stableId))
                throw new ArgumentException("自定义节点类型稳定标识非法。", nameof(stableId));
            if (ESGraphNodeTypeCatalog.GetKind(stableId) != ESGraphBuiltInNodeKind.Custom)
                throw new ArgumentException("该稳定标识已被内置节点类型占用。", nameof(stableId));
            return new ESGraphNodeTypeKey(ESGraphBuiltInNodeKind.Custom, stableId);
        }

        public static ESGraphNodeTypeKey Parse(string stableId)
        {
            ESGraphBuiltInNodeKind kind = ESGraphNodeTypeCatalog.GetKind(stableId);
            if (!ESGraphStableIdUtility.IsValid(stableId))
                return new ESGraphNodeTypeKey(ESGraphBuiltInNodeKind.Custom, stableId);
            return kind == ESGraphBuiltInNodeKind.Custom
                ? Custom(stableId)
                : FromKind(kind);
        }

        public bool Equals(ESGraphNodeTypeKey other)
        {
            return string.Equals(StableId, other.StableId, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is ESGraphNodeTypeKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return StableId == null ? 0 : StringComparer.Ordinal.GetHashCode(StableId);
        }

        public static bool operator ==(ESGraphNodeTypeKey left, ESGraphNodeTypeKey right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ESGraphNodeTypeKey left, ESGraphNodeTypeKey right)
        {
            return !left.Equals(right);
        }

        public override string ToString()
        {
            return StableId ?? string.Empty;
        }
    }

    public static class ESGraphDomainCatalog
    {
        public static string GetStableId(ESGraphDomainKind kind)
        {
            switch (kind)
            {
                case ESGraphDomainKind.Generic: return ESGraphDomainIds.Generic;
                case ESGraphDomainKind.Story: return ESGraphDomainIds.Story;
                case ESGraphDomainKind.BehaviorTree: return ESGraphDomainIds.BehaviorTree;
                default: return string.Empty;
            }
        }

        public static ESGraphDomainKind GetKind(string stableId)
        {
            switch (stableId)
            {
                case ESGraphDomainIds.Generic: return ESGraphDomainKind.Generic;
                case ESGraphDomainIds.Story: return ESGraphDomainKind.Story;
                case ESGraphDomainIds.BehaviorTree: return ESGraphDomainKind.BehaviorTree;
                default: return ESGraphDomainKind.Custom;
            }
        }
    }

    public static class ESGraphNodeTypeCatalog
    {
        public static string GetStableId(ESGraphBuiltInNodeKind kind)
        {
            switch (kind)
            {
                case ESGraphBuiltInNodeKind.GenericFlow: return ESGraphNodeTypeIds.GenericFlow;
                case ESGraphBuiltInNodeKind.GenericSource: return ESGraphNodeTypeIds.GenericSource;
                case ESGraphBuiltInNodeKind.GenericSink: return ESGraphNodeTypeIds.GenericSink;
                case ESGraphBuiltInNodeKind.GenericBranch: return ESGraphNodeTypeIds.GenericBranch;
                case ESGraphBuiltInNodeKind.GenericMerge: return ESGraphNodeTypeIds.GenericMerge;
                case ESGraphBuiltInNodeKind.StoryStart: return ESGraphNodeTypeIds.StoryStart;
                case ESGraphBuiltInNodeKind.StoryDialogue: return ESGraphNodeTypeIds.StoryDialogue;
                case ESGraphBuiltInNodeKind.StoryChoice: return ESGraphNodeTypeIds.StoryChoice;
                case ESGraphBuiltInNodeKind.StoryCondition: return ESGraphNodeTypeIds.StoryCondition;
                case ESGraphBuiltInNodeKind.StoryAction: return ESGraphNodeTypeIds.StoryAction;
                case ESGraphBuiltInNodeKind.StoryComplete: return ESGraphNodeTypeIds.StoryComplete;
                case ESGraphBuiltInNodeKind.StoryFail: return ESGraphNodeTypeIds.StoryFail;
                case ESGraphBuiltInNodeKind.BehaviorRoot: return ESGraphNodeTypeIds.BehaviorRoot;
                case ESGraphBuiltInNodeKind.BehaviorSequence: return ESGraphNodeTypeIds.BehaviorSequence;
                case ESGraphBuiltInNodeKind.BehaviorSelector: return ESGraphNodeTypeIds.BehaviorSelector;
                case ESGraphBuiltInNodeKind.BehaviorParallel: return ESGraphNodeTypeIds.BehaviorParallel;
                case ESGraphBuiltInNodeKind.BehaviorDecorator: return ESGraphNodeTypeIds.BehaviorDecorator;
                case ESGraphBuiltInNodeKind.BehaviorCondition: return ESGraphNodeTypeIds.BehaviorCondition;
                case ESGraphBuiltInNodeKind.BehaviorAction: return ESGraphNodeTypeIds.BehaviorAction;
                default: return string.Empty;
            }
        }

        public static ESGraphBuiltInNodeKind GetKind(string stableId)
        {
            switch (stableId)
            {
                case ESGraphNodeTypeIds.GenericFlow: return ESGraphBuiltInNodeKind.GenericFlow;
                case ESGraphNodeTypeIds.GenericSource: return ESGraphBuiltInNodeKind.GenericSource;
                case ESGraphNodeTypeIds.GenericSink: return ESGraphBuiltInNodeKind.GenericSink;
                case ESGraphNodeTypeIds.GenericBranch: return ESGraphBuiltInNodeKind.GenericBranch;
                case ESGraphNodeTypeIds.GenericMerge: return ESGraphBuiltInNodeKind.GenericMerge;
                case ESGraphNodeTypeIds.StoryStart: return ESGraphBuiltInNodeKind.StoryStart;
                case ESGraphNodeTypeIds.StoryDialogue: return ESGraphBuiltInNodeKind.StoryDialogue;
                case ESGraphNodeTypeIds.StoryChoice: return ESGraphBuiltInNodeKind.StoryChoice;
                case ESGraphNodeTypeIds.StoryCondition: return ESGraphBuiltInNodeKind.StoryCondition;
                case ESGraphNodeTypeIds.StoryAction: return ESGraphBuiltInNodeKind.StoryAction;
                case ESGraphNodeTypeIds.StoryComplete: return ESGraphBuiltInNodeKind.StoryComplete;
                case ESGraphNodeTypeIds.StoryFail: return ESGraphBuiltInNodeKind.StoryFail;
                case ESGraphNodeTypeIds.BehaviorRoot: return ESGraphBuiltInNodeKind.BehaviorRoot;
                case ESGraphNodeTypeIds.BehaviorSequence: return ESGraphBuiltInNodeKind.BehaviorSequence;
                case ESGraphNodeTypeIds.BehaviorSelector: return ESGraphBuiltInNodeKind.BehaviorSelector;
                case ESGraphNodeTypeIds.BehaviorParallel: return ESGraphBuiltInNodeKind.BehaviorParallel;
                case ESGraphNodeTypeIds.BehaviorDecorator: return ESGraphBuiltInNodeKind.BehaviorDecorator;
                case ESGraphNodeTypeIds.BehaviorCondition: return ESGraphBuiltInNodeKind.BehaviorCondition;
                case ESGraphNodeTypeIds.BehaviorAction: return ESGraphBuiltInNodeKind.BehaviorAction;
                default: return ESGraphBuiltInNodeKind.Custom;
            }
        }
    }

    public static class ESGraphPortValueCatalog
    {
        public static string GetStableId(ESGraphPortValueKind kind, string customStableId = null)
        {
            switch (kind)
            {
                case ESGraphPortValueKind.Flow: return ESGraphPortValueIds.Flow;
                case ESGraphPortValueKind.Any: return ESGraphPortValueIds.Any;
                case ESGraphPortValueKind.Boolean: return ESGraphPortValueIds.Boolean;
                case ESGraphPortValueKind.Number: return ESGraphPortValueIds.Number;
                case ESGraphPortValueKind.Text: return ESGraphPortValueIds.Text;
                case ESGraphPortValueKind.Object: return ESGraphPortValueIds.Object;
                default: return customStableId?.Trim() ?? string.Empty;
            }
        }

        public static ESGraphPortValueKind GetKind(string stableId)
        {
            switch (stableId)
            {
                case ESGraphPortValueIds.Flow: return ESGraphPortValueKind.Flow;
                case ESGraphPortValueIds.Any: return ESGraphPortValueKind.Any;
                case ESGraphPortValueIds.Boolean: return ESGraphPortValueKind.Boolean;
                case ESGraphPortValueIds.Number: return ESGraphPortValueKind.Number;
                case ESGraphPortValueIds.Text: return ESGraphPortValueKind.Text;
                case ESGraphPortValueIds.Object: return ESGraphPortValueKind.Object;
                default: return ESGraphPortValueKind.Custom;
            }
        }

        public static bool AreCompatible(string outputStableId, string inputStableId)
        {
            ESGraphPortValueKind output = GetKind(outputStableId);
            ESGraphPortValueKind input = GetKind(inputStableId);
            if (output == ESGraphPortValueKind.Any || input == ESGraphPortValueKind.Any)
                return true;
            if (output != ESGraphPortValueKind.Custom || input != ESGraphPortValueKind.Custom)
                return output == input;
            return string.Equals(outputStableId, inputStableId, StringComparison.Ordinal);
        }
    }

    public static class ESGraphStableIdUtility
    {
        public static bool IsValid(string domainId)
        {
            if (string.IsNullOrWhiteSpace(domainId))
                return false;
            domainId = domainId.Trim();
            if (domainId.Length > 96 || !IsAsciiLetter(domainId[0]))
                return false;
            for (int i = 1; i < domainId.Length; i++)
            {
                char value = domainId[i];
                if (!IsAsciiLetter(value) && (value < '0' || value > '9') && value != '.' && value != '-' && value != '_')
                    return false;
            }
            return true;
        }

        private static bool IsAsciiLetter(char value)
        {
            return value >= 'a' && value <= 'z';
        }
    }

    public interface IESBakedGraphPlan
    {
        ESGraphDomainKey Domain { get; }
        string DomainId { get; }
        string SourceContentSignature { get; }
    }

    /// <summary>
    /// Domain-owned adapter from the common baked graph into a strongly typed plan.
    /// This contract does not execute or schedule the plan.
    /// </summary>
    public interface IESGraphPlanBaker<TPlan> where TPlan : IESBakedGraphPlan
    {
        ESGraphDomainKey Domain { get; }

        bool TryBake(ESBakedGraphSnapshot source, out TPlan plan,
            out IReadOnlyList<ESGraphValidationIssue> issues);
    }

    public static class ESGraphPlanBakeGuard
    {
        public static bool TryValidateSource(ESBakedGraphSnapshot source, ESGraphDomainKey expectedDomain,
            out IReadOnlyList<ESGraphValidationIssue> issues)
        {
            return TryValidateSource(source, expectedDomain.StableId, out issues);
        }

        public static bool TryValidateSource(ESBakedGraphSnapshot source, string expectedDomainId,
            out IReadOnlyList<ESGraphValidationIssue> issues)
        {
            List<ESGraphValidationIssue> failures = new List<ESGraphValidationIssue>();
            if (source == null)
            {
                failures.Add(ESGraphValidationIssue.Error("Graph.Plan.SourceNull", "Baked Graph Snapshot 不能为空。"));
            }
            else if (!ESGraphStableIdUtility.IsValid(expectedDomainId))
            {
                failures.Add(ESGraphValidationIssue.Error("Graph.Plan.DomainInvalid",
                    "Plan Baker 的 DomainId 非法：" + expectedDomainId));
            }
            else if (!string.Equals(source.DomainId, expectedDomainId, StringComparison.Ordinal))
            {
                failures.Add(ESGraphValidationIssue.Error("Graph.Plan.DomainMismatch",
                    "Baked Graph Domain 不匹配。Expected=" + expectedDomainId + ", Actual=" + source.DomainId));
            }
            issues = failures;
            return failures.Count == 0;
        }
    }
}
