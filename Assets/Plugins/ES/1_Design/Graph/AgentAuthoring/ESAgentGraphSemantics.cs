#if UNITY_EDITOR
using System;
namespace ES
{
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
        public const string ContextOutputPortKey = "agent.context.out";
        public const string RequirementOutputPortKey = "agent.requirement.out";
        public const string ArtifactOutputPortKey = "agent.artifact.out";
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
        public const string SkillCallNode = "es.agent.ai-skill.call";
        public const string SkillBranchNode = "es.agent.ai-skill.branch";
        public const string SkillForEachNode = "es.agent.ai-skill.for-each";
        public const string SkillApprovalNode = "es.agent.ai-skill.approval";
        public const string SkillFanOutNode = "es.agent.ai-skill.fan-out";
        public const string SkillJoinNode = "es.agent.ai-skill.join";
        public const string SkillOutputNode = "es.agent.ai-skill.output";
        public const string SkillControlPort = "es.agent.ai-skill.control";
        public const string SkillTextListPort = "es.agent.ai-skill.text-list";
        public const string SkillProjectPathPort = "es.agent.ai-skill.project-path";
        public const string SkillProjectPathListPort = "es.agent.ai-skill.project-path-list";
        public const string SkillRunResultPort = "es.agent.ai-skill.run-result";
        public const string SkillArtifactListPort = "es.agent.ai-skill.artifact-list";
        public const string SkillParametersPortKey = "skill.value.parameters";
        public const string SkillInputPortKey = "skill.value.input";
        public const string SkillItemsPortKey = "skill.value.items";
        public const string SkillItemValuePortKey = "skill.value.item";
        public const string SkillRunResultPortKey = "skill.value.run-result";
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
        public const string SkillFanOutPortKey = "skill.control.fan-out";
        public const string SkillJoinPortKey = "skill.control.join";

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
                || Is(typeId, ESAgentGraphStableIds.SkillCallNode)
                || Is(typeId, ESAgentGraphStableIds.SkillBranchNode)
                || Is(typeId, ESAgentGraphStableIds.SkillForEachNode)
                || Is(typeId, ESAgentGraphStableIds.SkillApprovalNode)
                || Is(typeId, ESAgentGraphStableIds.SkillFanOutNode)
                || Is(typeId, ESAgentGraphStableIds.SkillJoinNode)
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
                || Is(stableKey, ESAgentGraphStableIds.SkillRejectedPortKey)
                || Is(stableKey, ESAgentGraphStableIds.SkillFanOutPortKey)
                || Is(stableKey, ESAgentGraphStableIds.SkillJoinPortKey);
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
}
#endif
