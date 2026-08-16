using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using GraphAsset = global::ES.ESGraphAssetBase;

namespace ES.EditorInternal
{
    public sealed class ESAgentArtifactGenerationSpec : IESBakedGraphPlan
    {
        public const int CurrentContractSchemaVersion = 9;

        public int contractSchemaVersion = CurrentContractSchemaVersion;
        public string sourceGraphId;
        public string sourceOriginGraphId;
        public string sourceContentSignature;
        public ESAgentGenerationGoal goal;
        public ESAgentGenerationReference[] references = Array.Empty<ESAgentGenerationReference>();
        public ESAgentGenerationConstraint[] constraints = Array.Empty<ESAgentGenerationConstraint>();
        public ESAgentGenerationBranch[] branches = Array.Empty<ESAgentGenerationBranch>();
        public ESAgentGenerationTraversal[] traversals = Array.Empty<ESAgentGenerationTraversal>();
        public ESAgentGenerationOutput[] outputs = Array.Empty<ESAgentGenerationOutput>();
        public ESAgentGenerationValidation[] validations = Array.Empty<ESAgentGenerationValidation>();
        public ESAgentGenerationRelation[] relations = Array.Empty<ESAgentGenerationRelation>();
        /// <summary>AICommand + AISkill 的共享 Skill 能力包身份与边界；当前合同必须显式提供。</summary>
        public ESAgentSkillBundleContract skillBundle;
        /// <summary>仅在用户明确承担可恢复的质量风险后存在；绑定 Graph 签名、问题集合和操作者。</summary>
        public ESGraphRiskAcceptance riskAcceptance;

        public ESGraphDomainKey Domain => ESAgentGraphStableIds.Domain;
        public string DomainId => Domain.StableId;
        public string SourceContentSignature => sourceContentSignature ?? string.Empty;
    }

    public static class ESAgentGenerationRiskValidator
    {
        public static bool TryValidate(ESAgentArtifactGenerationSpec spec, out string error)
        {
            if (spec == null)
            {
                error = "GenerationSpec 不能为空。";
                return false;
            }
            if (ESAgentGenerationSemanticValidator.TryValidate(spec, out string semanticError))
            {
                if (spec.riskAcceptance != null)
                {
                    error = "GenerationSpec 已无需要承担的语义风险，但仍携带旧风险确认；请重新烘焙。";
                    return false;
                }
                error = string.Empty;
                return true;
            }

            var issues = new List<ESGraphValidationIssue>
            {
                ESGraphValidationIssue.Error("AgentAuthoring.SemanticAlignment", semanticError,
                    spec.goal?.nodeId, true)
            };
            if (spec.riskAcceptance == null)
            {
                error = "目标与输出语义不一致，且缺少绑定当前 Graph 的风险确认：" + semanticError;
                return false;
            }
            if (!spec.riskAcceptance.TryValidate(spec.sourceGraphId, spec.SourceContentSignature,
                    issues, out string acceptanceError))
            {
                error = "目标与输出语义不一致，风险确认无效：" + acceptanceError;
                return false;
            }
            error = string.Empty;
            return true;
        }
    }

    public static class ESAgentGenerationIntentValidator
    {
        public static bool TryValidate(ESAgentArtifactGenerationSpec spec, out string error)
        {
            if (spec == null || spec.contractSchemaVersion != ESAgentArtifactGenerationSpec.CurrentContractSchemaVersion)
            {
                error = "GenerationSpec 语义契约版本无效。";
                return false;
            }
            if (spec.goal == null || string.IsNullOrWhiteSpace(spec.goal.nodeId))
            {
                error = "GenerationSpec 缺少带稳定 NodeId 的 Goal。";
                return false;
            }

            var references = IndexNodeIds(spec.references, item => item?.nodeId, "Reference", out error);
            if (references == null) return false;
            var constraints = IndexNodeIds(spec.constraints, item => item?.nodeId, "Constraint", out error);
            if (constraints == null) return false;
            var branches = IndexNodeIds(spec.branches, item => item?.nodeId, "Branch", out error);
            if (branches == null) return false;
            var traversals = IndexNodeIds(spec.traversals, item => item?.nodeId, "Traversal", out error);
            if (traversals == null) return false;
            var outputs = IndexNodeIds(spec.outputs, item => item?.nodeId, "Output", out error);
            if (outputs == null || outputs.Count == 0)
            {
                if (string.IsNullOrEmpty(error)) error = "GenerationSpec 至少需要一个 Output。";
                return false;
            }
            var validations = IndexNodeIds(spec.validations, item => item?.nodeId, "Validation", out error);
            if (validations == null || validations.Count == 0)
            {
                if (string.IsNullOrEmpty(error)) error = "GenerationSpec 至少需要一个 Validation。";
                return false;
            }
            if (!TryValidateSkillBundle(spec.skillBundle, spec.goal, spec.references,
                    spec.constraints, spec.branches, spec.traversals, spec.outputs,
                    spec.validations, out error))
                return false;

            var allNodeIds = new HashSet<string>(StringComparer.Ordinal) { spec.goal.nodeId };
            if (!AddUniqueNodeIds(allNodeIds, references)
                || !AddUniqueNodeIds(allNodeIds, constraints)
                || !AddUniqueNodeIds(allNodeIds, branches)
                || !AddUniqueNodeIds(allNodeIds, traversals)
                || !AddUniqueNodeIds(allNodeIds, outputs)
                || !AddUniqueNodeIds(allNodeIds, validations))
            {
                error = "GenerationSpec 的不同节点类别之间存在重复 NodeId。";
                return false;
            }

            foreach (ESAgentGenerationConstraint constraint in spec.constraints ?? Array.Empty<ESAgentGenerationConstraint>())
                if (!TryValidateConstraint(constraint, out error)) return false;
            foreach (ESAgentGenerationBranch branch in spec.branches ?? Array.Empty<ESAgentGenerationBranch>())
                if (!TryValidateBranch(branch, out error)) return false;
            foreach (ESAgentGenerationTraversal traversal in spec.traversals ?? Array.Empty<ESAgentGenerationTraversal>())
                if (!TryValidateTraversal(traversal, out error)) return false;

            var nodeTypes = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [spec.goal.nodeId] = ESAgentGraphStableIds.GoalNode
            };
            AddNodeTypes(nodeTypes, references, ESAgentGraphStableIds.ReferenceNode);
            AddNodeTypes(nodeTypes, constraints, ESAgentGraphStableIds.ConstraintNode);
            AddNodeTypes(nodeTypes, branches, ESAgentGraphStableIds.BranchNode);
            AddNodeTypes(nodeTypes, traversals, ESAgentGraphStableIds.TraverseNode);
            foreach (ESAgentGenerationOutput output in spec.outputs ?? Array.Empty<ESAgentGenerationOutput>())
            {
                if (!Enum.IsDefined(typeof(ESAgentArtifactKind), output.artifactKind))
                {
                    error = "GenerationSpec 包含非法 OutputArtifact 类型：" + output.nodeId;
                    return false;
                }
                nodeTypes[output.nodeId] = output.artifactKind == ESAgentArtifactKind.AICommand
                    ? ESAgentGraphStableIds.AICommandOutputNode : ESAgentGraphStableIds.AISkillOutputNode;
            }
            AddNodeTypes(nodeTypes, validations, ESAgentGraphStableIds.ValidationNode);

            var constraintTargets = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            var logicRoutes = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            var outputConstraintCount = new Dictionary<string, int>(StringComparer.Ordinal);
            var outputValidationCount = new Dictionary<string, int>(StringComparer.Ordinal);
            var outgoing = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (string nodeId in allNodeIds) outgoing[nodeId] = new List<string>();
            foreach (string outputId in outputs)
            {
                outputConstraintCount[outputId] = 0;
                outputValidationCount[outputId] = 0;
            }

            ESAgentGenerationRelation[] relations = spec.relations ?? Array.Empty<ESAgentGenerationRelation>();
            if (relations.Length == 0)
            {
                error = "GenerationSpec 缺少关系数据，无法确定意图归属。";
                return false;
            }
            var edgeIds = new HashSet<string>(StringComparer.Ordinal);
            var edgeOrders = new HashSet<int>();
            var relationKeys = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < relations.Length; i++)
            {
                ESAgentGenerationRelation relation = relations[i];
                if (relation == null || string.IsNullOrWhiteSpace(relation.edgeId)
                    || !edgeIds.Add(relation.edgeId)
                    || relation.order < 0 || !edgeOrders.Add(relation.order)
                    || string.IsNullOrWhiteSpace(relation.fromNodeId)
                    || string.IsNullOrWhiteSpace(relation.toNodeId)
                    || !ESGraphStableIdUtility.IsValid(relation.fromPortStableKey)
                    || !ESGraphStableIdUtility.IsValid(relation.toPortStableKey)
                    || !ESGraphEndpointRules.IsValidMeaning(relation.fromPortMeaning)
                    || !ESGraphEndpointRules.IsValidMeaning(relation.toPortMeaning)
                    || !ESGraphPortValueCatalog.IsValidStableId(relation.sourceValueTypeId)
                    || !ESGraphPortValueCatalog.IsValidStableId(relation.targetValueTypeId)
                    || !Enum.IsDefined(typeof(ESGraphPortAggregation), relation.sourceAggregation)
                    || !Enum.IsDefined(typeof(ESGraphPortAggregation), relation.targetAggregation)
                    || relation.sourceAggregation == ESGraphPortAggregation.Auto
                    || relation.targetAggregation == ESGraphPortAggregation.Auto
                    || !Enum.IsDefined(typeof(ESAgentRelationKind), relation.relationKind))
                {
                    error = "GenerationSpec 包含无效关系。";
                    return false;
                }
                string relationKey = relation.fromNodeId + "\n" + relation.fromPortStableKey + "\n"
                    + relation.toNodeId + "\n" + relation.toPortStableKey + "\n"
                    + relation.relationKind;
                if (!relationKeys.Add(relationKey))
                {
                    error = "GenerationSpec 包含重复关系：" + relation.edgeId;
                    return false;
                }
                if (!nodeTypes.TryGetValue(relation.fromNodeId, out string expectedFromType)
                    || !nodeTypes.TryGetValue(relation.toNodeId, out string expectedToType)
                    || !string.Equals(relation.fromNodeTypeId, expectedFromType, StringComparison.Ordinal)
                    || !string.Equals(relation.toNodeTypeId, expectedToType, StringComparison.Ordinal))
                {
                    error = "GenerationSpec 包含跨阶段或未知节点关系："
                        + relation.fromNodeId + " -> " + relation.toNodeId;
                    return false;
                }
                if (!TryValidateRelationEndpoint(expectedFromType, relation.fromPortStableKey,
                        ESGraphPortDirection.Output, relation.fromPortMeaning,
                        relation.sourceValueTypeId, relation.sourceAggregation, out error)
                    || !TryValidateRelationEndpoint(expectedToType, relation.toPortStableKey,
                        ESGraphPortDirection.Input, relation.toPortMeaning,
                        relation.targetValueTypeId, relation.targetAggregation, out error))
                    return false;
                if (!ESAgentRelationSemantics.TryResolve(expectedFromType, expectedToType,
                        relation.fromPortStableKey, out ESAgentRelationKind expected))
                {
                    error = "GenerationSpec 包含未知端点关系：" + relation.edgeId;
                    return false;
                }
                if (relation.relationKind != expected)
                {
                    error = "GenerationSpec 关系语义与端点不一致：" + relation.edgeId;
                    return false;
                }
                string expectedSemanticType = ESAgentRelationSemantics.ExpectedSemanticType(expected);
                if (!string.Equals(relation.semanticType, expectedSemanticType, StringComparison.Ordinal))
                {
                    error = "GenerationSpec 关系的数据语义与关系类型不一致：" + relation.edgeId;
                    return false;
                }
                outgoing[relation.fromNodeId].Add(relation.toNodeId);
                if (expected == ESAgentRelationKind.AppliesConstraint)
                {
                    if (!constraintTargets.TryGetValue(relation.fromNodeId, out HashSet<string> targets))
                    {
                        targets = new HashSet<string>(StringComparer.Ordinal);
                        constraintTargets.Add(relation.fromNodeId, targets);
                    }
                    if (targets.Add(relation.toNodeId)) outputConstraintCount[relation.toNodeId]++;
                }
                else if (expected == ESAgentRelationKind.RequiresValidation)
                {
                    outputValidationCount[relation.fromNodeId]++;
                }
                else if (expected == ESAgentRelationKind.SelectsBranch
                    || expected == ESAgentRelationKind.TraversesItems)
                {
                    if (!logicRoutes.TryGetValue(relation.fromNodeId, out HashSet<string> routes))
                    {
                        routes = new HashSet<string>(StringComparer.Ordinal);
                        logicRoutes.Add(relation.fromNodeId, routes);
                    }
                    // A semantic branch endpoint may fan out to multiple targets. The endpoint
                    // key remains unique in the route set while each relation is preserved in
                    // spec.relations for deterministic downstream consumption.
                    routes.Add(relation.fromPortStableKey);
                }
            }

            if (!ValidateRouteCoverage(branches, logicRoutes, new[]
                {
                    ESAgentGraphStableIds.BranchMatchedPortKey,
                    ESAgentGraphStableIds.BranchDefaultPortKey,
                    ESAgentGraphStableIds.BranchFailurePortKey
                }, "Branch", out error)
                || !ValidateRouteCoverage(traversals, logicRoutes, new[]
                {
                    ESAgentGraphStableIds.TraverseItemPortKey,
                    ESAgentGraphStableIds.TraverseCompletedPortKey,
                    ESAgentGraphStableIds.TraverseFailurePortKey
                }, "Traversal", out error))
                return false;

            foreach (string constraintId in constraints)
            {
                if (!constraintTargets.TryGetValue(constraintId, out HashSet<string> targets) || targets.Count == 0)
                {
                    error = "Constraint 没有明确作用到任何 Output：" + constraintId;
                    return false;
                }
            }
            foreach (string outputId in outputs)
            {
                if (outputConstraintCount[outputId] == 0 || outputValidationCount[outputId] == 0)
                {
                    error = "每个 Output 必须有明确 Constraint 和 Validation：" + outputId;
                    return false;
                }
            }
            var visited = new HashSet<string>(StringComparer.Ordinal) { spec.goal.nodeId };
            var queue = new Queue<string>();
            queue.Enqueue(spec.goal.nodeId);
            while (queue.Count > 0)
            {
                foreach (string next in outgoing[queue.Dequeue()])
                    if (visited.Add(next)) queue.Enqueue(next);
            }
            if (visited.Count != allNodeIds.Count)
            {
                error = "GenerationSpec 包含无法从 Goal 到达的节点。";
                return false;
            }
            return ValidateAnyOfGroups(spec.constraints, constraintTargets, out error);
        }

        private static bool TryValidateConstraint(ESAgentGenerationConstraint constraint, out string error)
        {
            if (constraint == null || !Enum.IsDefined(typeof(ESAgentConstraintKind), constraint.kind)
                || !Enum.IsDefined(typeof(ESAgentConstraintScope), constraint.scope)
                || !Enum.IsDefined(typeof(ESAgentConstraintCombinationMode), constraint.combinationMode))
            {
                error = "GenerationSpec 包含非法 Constraint 语义。";
                return false;
            }
            if (constraint.priority < 0 || constraint.priority > 100
                || string.IsNullOrWhiteSpace(constraint.statement)
                || string.IsNullOrWhiteSpace(constraint.rationale)
                || string.IsNullOrWhiteSpace(constraint.verification))
            {
                error = "GenerationSpec Constraint 的优先级、规则、原因或验证不完整：" + constraint.nodeId;
                return false;
            }
            string group = (constraint.combinationGroup ?? string.Empty).Trim();
            if (constraint.combinationMode == ESAgentConstraintCombinationMode.AnyOf)
            {
                if (!System.Text.RegularExpressions.Regex.IsMatch(group, "^[a-z0-9][a-z0-9._-]{0,63}$"))
                {
                    error = "AnyOf Constraint 缺少合法组合组：" + constraint.nodeId;
                    return false;
                }
            }
            else if (!string.IsNullOrEmpty(group))
            {
                error = "AllOf Constraint 不得声明组合组：" + constraint.nodeId;
                return false;
            }
            error = string.Empty;
            return true;
        }

        private static bool TryValidateBranch(ESAgentGenerationBranch branch, out string error)
        {
            if (branch == null || string.IsNullOrWhiteSpace(branch.nodeId)
                || string.IsNullOrWhiteSpace(branch.condition)
                || string.IsNullOrWhiteSpace(branch.matchedPath)
                || string.IsNullOrWhiteSpace(branch.defaultPath)
                || string.IsNullOrWhiteSpace(branch.failurePath)
                || branch.matchedTargetNodeIds == null || branch.matchedTargetNodeIds.Length == 0
                || branch.defaultTargetNodeIds == null || branch.defaultTargetNodeIds.Length == 0
                || !HasValidTargetIds(branch.matchedTargetNodeIds)
                || !HasValidTargetIds(branch.defaultTargetNodeIds)
                || !HasValidTargetIds(branch.failureTargetNodeIds))
            {
                error = "GenerationSpec Branch 缺少条件、路径文本或完整目标端点集合。";
                return false;
            }
            error = string.Empty;
            return true;
        }

        private static bool TryValidateRelationEndpoint(string nodeTypeId, string portKey,
            ESGraphPortDirection direction, string meaning, string valueTypeId,
            ESGraphPortAggregation aggregation, out string error)
        {
            error = string.Empty;
            if (!ESGraphAuthoringRegistry.TryGetNodeDefinition(ESAgentGraphStableIds.DomainId,
                    nodeTypeId, out IESGraphNodeDefinition definition))
            {
                error = "GenerationSpec 关系端点节点类型未注册：" + nodeTypeId;
                return false;
            }
            ESGraphPortDefinition[] matches = (definition.Ports ?? Array.Empty<ESGraphPortDefinition>())
                .Where(port => port != null
                    && string.Equals(port.stableKey, portKey, StringComparison.Ordinal)).ToArray();
            if (matches.Length != 1 || matches[0].direction != direction)
            {
                error = "GenerationSpec 关系端点不存在或方向错误：" + nodeTypeId + "/" + portKey;
                return false;
            }
            ESGraphPortDefinition expected = matches[0];
            string expectedMeaning = ESGraphEndpointRules.ResolveMeaning(expected.meaning,
                expected.name, expected.stableKey);
            ESGraphPortAggregation expectedAggregation = ESGraphPortAggregationRules.Resolve(
                expected.direction, expected.capacity, expected.aggregation);
            if (!string.Equals(expectedMeaning, meaning, StringComparison.Ordinal)
                || !string.Equals(expected.valueTypeId, valueTypeId, StringComparison.Ordinal)
                || expectedAggregation != aggregation)
            {
                error = "GenerationSpec 关系端点定义已失配：" + nodeTypeId + "/" + portKey;
                return false;
            }
            return true;
        }

        private static string[] RouteTargetIds(ESBakedGraphSnapshot source, string nodeId,
            string portKey)
        {
            return (source?.GetOutgoingRoutes(nodeId, portKey)
                    ?? Array.Empty<ESGraphRouteSnapshot>())
                .Select(route => route.TargetNodeId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        private static bool HasValidTargetIds(IEnumerable<string> targetNodeIds)
        {
            string[] ids = (targetNodeIds ?? Array.Empty<string>()).ToArray();
            return ids.Length > 0 && ids.All(id => ESGraphIdentity.IsValid(id))
                && ids.Distinct(StringComparer.Ordinal).Count() == ids.Length;
        }

        private static bool TryValidateTraversal(ESAgentGenerationTraversal traversal, out string error)
        {
            if (traversal == null || string.IsNullOrWhiteSpace(traversal.nodeId)
                || !Enum.IsDefined(typeof(ESAgentTraversalOrder), traversal.order)
                || traversal.maxDepth < 1 || traversal.maxDepth > 32
                || traversal.maxItems < 1 || traversal.maxItems > 512
                || string.IsNullOrWhiteSpace(traversal.target)
                || string.IsNullOrWhiteSpace(traversal.itemAlias)
                || string.IsNullOrWhiteSpace(traversal.stopCondition)
                || string.IsNullOrWhiteSpace(traversal.emptyResultAction)
                || string.IsNullOrWhiteSpace(traversal.failureAction))
            {
                error = "GenerationSpec Traversal 的目标、顺序、安全上限、停止或恢复语义无效。";
                return false;
            }
            error = string.Empty;
            return true;
        }

        private static bool ValidateAnyOfGroups(IEnumerable<ESAgentGenerationConstraint> source,
            Dictionary<string, HashSet<string>> constraintTargets, out string error)
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (ESAgentGenerationConstraint constraint in source ?? Array.Empty<ESAgentGenerationConstraint>())
            {
                if (constraint == null || constraint.combinationMode != ESAgentConstraintCombinationMode.AnyOf
                    || !constraintTargets.TryGetValue(constraint.nodeId, out HashSet<string> targets))
                    continue;
                foreach (string target in targets)
                {
                    string key = target + "\n" + constraint.scope + "\n" + constraint.combinationGroup;
                    counts.TryGetValue(key, out int count);
                    counts[key] = count + 1;
                }
            }
            foreach (KeyValuePair<string, int> pair in counts)
            {
                if (pair.Value >= 2) continue;
                error = "AnyOf 组合组在同一 Output 和作用域内至少需要两条 Constraint："
                    + pair.Key.Replace('\n', '/');
                return false;
            }
            error = string.Empty;
            return true;
        }

        private static void AddNodeTypes(Dictionary<string, string> destination,
            IEnumerable<string> nodeIds, string nodeTypeId)
        {
            foreach (string nodeId in nodeIds ?? Array.Empty<string>())
                destination[nodeId] = nodeTypeId;
        }

        private static bool ValidateRouteCoverage(IEnumerable<string> nodeIds,
            Dictionary<string, HashSet<string>> actualRoutes, IEnumerable<string> requiredRoutes,
            string label, out string error)
        {
            var required = new HashSet<string>(requiredRoutes, StringComparer.Ordinal);
            foreach (string nodeId in nodeIds ?? Array.Empty<string>())
            {
                if (!actualRoutes.TryGetValue(nodeId, out HashSet<string> actual)
                    || !actual.SetEquals(required))
                {
                    error = label + " 必须且只能连接每个声明出口：" + nodeId;
                    return false;
                }
            }
            error = string.Empty;
            return true;
        }

        private static bool AddUniqueNodeIds(HashSet<string> destination, IEnumerable<string> source)
        {
            foreach (string nodeId in source)
                if (!destination.Add(nodeId)) return false;
            return true;
        }

        private static bool TryValidateSkillBundle(ESAgentSkillBundleContract bundle,
            ESAgentGenerationGoal goal, IEnumerable<ESAgentGenerationReference> references,
            IEnumerable<ESAgentGenerationConstraint> constraints,
            IEnumerable<ESAgentGenerationBranch> branches,
            IEnumerable<ESAgentGenerationTraversal> traversals,
            IEnumerable<ESAgentGenerationOutput> outputs,
            IEnumerable<ESAgentGenerationValidation> validations, out string error)
        {
            if (bundle == null)
            {
                error = "GenerationSpec 缺少 AICommand + AISkill 共享 Skill 能力包合同。";
                return false;
            }
            if (bundle.schemaVersion != ESAgentSkillBundleContract.CurrentSchemaVersion
                || string.IsNullOrWhiteSpace(bundle.bundleId)
                || string.IsNullOrWhiteSpace(bundle.displayName)
                || goal == null
                || !string.Equals(bundle.goalNodeId, goal.nodeId, StringComparison.Ordinal))
            {
                error = "Skill 能力包缺少稳定身份，或没有绑定当前 Goal。";
                return false;
            }

            var outputById = (outputs ?? Array.Empty<ESAgentGenerationOutput>())
                .Where(item => item != null && !string.IsNullOrWhiteSpace(item.nodeId))
                .ToDictionary(item => item.nodeId, StringComparer.Ordinal);
            string[] commandIds = bundle.commandOutputNodeIds ?? Array.Empty<string>();
            string[] skillIds = bundle.aiSkillOutputNodeIds ?? Array.Empty<string>();
            if (commandIds.Length == 0 && skillIds.Length == 0)
            {
                error = "Skill 能力包至少需要一个 AICommand 或 AISkill Output。";
                return false;
            }
            if (HasDuplicates(commandIds) || HasDuplicates(skillIds)
                || HasDuplicates(bundle.referenceNodeIds)
                || HasDuplicates(bundle.constraintNodeIds)
                || HasDuplicates(bundle.branchNodeIds)
                || HasDuplicates(bundle.traversalNodeIds)
                || HasDuplicates(bundle.validationNodeIds))
            {
                error = "Skill 能力包的节点绑定不能重复。";
                return false;
            }
            if (commandIds.Any(id => !outputById.TryGetValue(id, out ESAgentGenerationOutput output)
                    || output.artifactKind != ESAgentArtifactKind.AICommand)
                || skillIds.Any(id => !outputById.TryGetValue(id, out ESAgentGenerationOutput output)
                    || output.artifactKind != ESAgentArtifactKind.AgentSkill))
            {
                error = "Skill 能力包引用了不存在或类型不匹配的 Output。";
                return false;
            }
            ESAgentSkillBundleKind expectedKind = commandIds.Length > 0 && skillIds.Length > 0
                ? ESAgentSkillBundleKind.CommandAndAISkill
                : commandIds.Length > 0 ? ESAgentSkillBundleKind.CommandOnly : ESAgentSkillBundleKind.AISkillOnly;
            if (bundle.kind != expectedKind)
            {
                error = "Skill 能力包类型与 AICommand/AISkill 组成不一致。";
                return false;
            }
            if (!SetEquals(commandIds, outputById.Values
                    .Where(item => item.artifactKind == ESAgentArtifactKind.AICommand)
                    .Select(item => item.nodeId))
                || !SetEquals(skillIds, outputById.Values
                    .Where(item => item.artifactKind == ESAgentArtifactKind.AgentSkill)
                    .Select(item => item.nodeId)))
            {
                error = "Skill 能力包没有完整绑定当前 Graph 的 AICommand/AISkill Output。";
                return false;
            }

            if (!SetEquals(bundle.referenceNodeIds, NodeIds(references, item => item?.nodeId))
                || !SetEquals(bundle.constraintNodeIds, NodeIds(constraints, item => item?.nodeId))
                || !SetEquals(bundle.branchNodeIds, NodeIds(branches, item => item?.nodeId))
                || !SetEquals(bundle.traversalNodeIds, NodeIds(traversals, item => item?.nodeId))
                || !SetEquals(bundle.validationNodeIds, NodeIds(validations, item => item?.nodeId)))
            {
                error = "Skill 能力包必须完整绑定当前 Graph 的上下文、逻辑、约束和验证节点。";
                return false;
            }
            if (bundle.IsPaired && !(validations ?? Array.Empty<ESAgentGenerationValidation>())
                    .Any(item => item != null && item.validateAICommand && item.validateAgentSkill
                        && item.requireDiffReview && item.requireHumanApproval))
            {
                error = "AICommand + AISkill 能力包必须共享至少一个 Diff Review 与人工批准门禁。";
                return false;
            }
            error = string.Empty;
            return true;
        }

        private static bool AllKnownNodeIds<T>(IEnumerable<string> ids, IEnumerable<T> items,
            Func<T, string> getNodeId)
        {
            var known = new HashSet<string>((items ?? Enumerable.Empty<T>())
                .Select(getNodeId).Where(id => !string.IsNullOrWhiteSpace(id)), StringComparer.Ordinal);
            return (ids ?? Array.Empty<string>()).All(known.Contains);
        }

        private static IEnumerable<string> NodeIds<T>(IEnumerable<T> items, Func<T, string> getNodeId)
        {
            return (items ?? Enumerable.Empty<T>()).Select(getNodeId)
                .Where(id => !string.IsNullOrWhiteSpace(id));
        }

        private static bool HasDuplicates(IEnumerable<string> values)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            return (values ?? Array.Empty<string>()).Any(value =>
                !string.IsNullOrWhiteSpace(value) && !seen.Add(value));
        }

        private static bool SetEquals(IEnumerable<string> left, IEnumerable<string> right)
        {
            return new HashSet<string>(left ?? Array.Empty<string>(), StringComparer.Ordinal)
                .SetEquals(right ?? Array.Empty<string>());
        }

        private static HashSet<string> IndexNodeIds<T>(IEnumerable<T> source, Func<T, string> getNodeId,
            string label, out string error)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            foreach (T item in source ?? Enumerable.Empty<T>())
            {
                string nodeId = getNodeId(item);
                if (string.IsNullOrWhiteSpace(nodeId) || !result.Add(nodeId))
                {
                    error = label + " 缺少唯一稳定 NodeId。";
                    return null;
                }
            }
            error = string.Empty;
            return result;
        }
    }

    internal static class ESAgentArtifactIdentity
    {
        public static string Create(string graphId, string outputNodeId)
        {
            if (!ESGraphIdentity.IsValid(graphId) || !ESGraphIdentity.IsValid(outputNodeId))
                return string.Empty;
            return "es." + graphId + "." + outputNodeId;
        }

        public static string CreateBundle(string graphId)
        {
            return ESGraphIdentity.IsValid(graphId) ? "es." + graphId + ".skill-bundle" : string.Empty;
        }
    }

    public static class ESAgentAuthoringGraphValidator
    {
        public static void Validate(GraphAsset asset, List<ESGraphValidationIssue> issues)
        {
            if (asset == null || issues == null)
                return;
            if (asset.allowCycles)
                issues.Add(ESGraphValidationIssue.Error("AgentAuthoring.CyclePolicy", "智能助手编排图禁止循环。"));

            int goalCount = 0;
            int outputCount = 0;
            int validationCount = 0;
            var definitions = ESGraphAuthoringRegistry
                .GetNodeDefinitions(ESAgentGraphStableIds.Domain)
                .ToDictionary(definition => definition.NodeType);
            for (int i = 0; i < asset.Nodes.Count; i++)
            {
                ESGraphNodeRecord node = asset.Nodes[i];
                if (node == null)
                    continue;
                ValidateNodeSchema(node, definitions, issues);
                if (definitions.TryGetValue(node.TypeKey, out IESGraphNodeDefinition definition))
                {
                    if (definition.Category == ESGraphNodeCategory.Output)
                        outputCount++;
                    else if (definition.Category == ESGraphNodeCategory.Validation)
                        validationCount++;
                }
                switch (node.typeId)
                {
                    case ESAgentGraphStableIds.GoalNode:
                        goalCount++;
                        ValidateGoal(node, issues);
                        break;
                    case ESAgentGraphStableIds.ReferenceNode:
                        ValidateReference(node, issues);
                        break;
                    case ESAgentGraphStableIds.ConstraintNode:
                        ValidateConstraint(node, issues);
                        break;
                    case ESAgentGraphStableIds.BranchNode:
                        ValidateBranch(node, issues);
                        break;
                    case ESAgentGraphStableIds.TraverseNode:
                        ValidateTraversal(node, issues);
                        break;
                    case ESAgentGraphStableIds.AICommandOutputNode:
                        ValidateAICommandOutput(node, issues);
                        break;
                    case ESAgentGraphStableIds.AISkillOutputNode:
                        ValidateAgentSkillOutput(node, issues);
                        break;
                    case ESAgentGraphStableIds.ValidationNode:
                        ValidateValidation(node, issues);
                        break;
                }
            }
            if (goalCount != 1)
                issues.Add(ESGraphValidationIssue.Error("AgentAuthoring.GoalCount",
                    "智能助手编排图必须且只能有一个 Goal，当前为 " + goalCount + " 个。"));
            if (outputCount == 0)
                issues.Add(ESGraphValidationIssue.Error("AgentAuthoring.OutputMissing", "至少需要一个 OutputArtifact 节点。"));
            if (validationCount == 0)
                issues.Add(ESGraphValidationIssue.Error("AgentAuthoring.ValidationMissing", "至少需要一个 Validation 节点。"));

            ESGraphNodeRecord goal = goalCount == 1 ? FindSingle(asset, ESAgentGraphStableIds.GoalNode) : null;
            ValidateTransitions(asset, definitions, issues);
            ValidateLogicRoutes(asset, issues);
            if (goal != null)
            {
                ValidateSemanticAlignment(asset, goal, issues);
            }
        }

        internal static bool TryRead<T>(string json, out T payload, out string error) where T : class
        {
            payload = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                error = "Payload 不能为空。";
                return false;
            }
            try { payload = JsonUtility.FromJson<T>(json); }
            catch (ArgumentException exception)
            {
                error = "Payload JSON 无法解析：" + exception.Message;
                return false;
            }
            error = payload == null ? "Payload JSON 无法解析。" : string.Empty;
            return payload != null;
        }

        private static void ValidateGoal(ESGraphNodeRecord node, List<ESGraphValidationIssue> issues)
        {
            if (!TryRead(node.payloadJson, out ESAgentGoalPayload payload, out string error)
                || payload.schemaVersion != 1)
            {
                issues.Add(ESGraphValidationIssue.Error("AgentAuthoring.Goal", string.IsNullOrEmpty(error)
                    ? "Goal SchemaVersion 无效。" : error, node.nodeId));
                return;
            }
            if (string.IsNullOrWhiteSpace(payload.title))
                issues.Add(ESGraphValidationIssue.Error("AgentAuthoring.Goal.Title", "Goal 标题不能为空。", node.nodeId));
            if (string.IsNullOrWhiteSpace(payload.objective))
                issues.Add(ESGraphValidationIssue.Error("AgentAuthoring.Goal.Objective", "最终目的不能为空。", node.nodeId));
            if (string.IsNullOrWhiteSpace(payload.successCriteria))
                issues.Add(ESGraphValidationIssue.Error("AgentAuthoring.Goal.SuccessCriteria",
                    "成功标准必须明确最终结果如何被验收。", node.nodeId));
        }

        public static bool TryGetFinalPurpose(GraphAsset asset, out string objective, out string successCriteria)
        {
            objective = string.Empty;
            successCriteria = string.Empty;
            if (asset == null)
                return false;
            ESGraphNodeRecord goal = null;
            int count = 0;
            for (int i = 0; i < asset.Nodes.Count; i++)
            {
                ESGraphNodeRecord node = asset.Nodes[i];
                if (node == null || !string.Equals(node.typeId, ESAgentGraphStableIds.GoalNode,
                        StringComparison.Ordinal))
                    continue;
                goal = node;
                count++;
            }
            if (count != 1 || !TryRead(goal.payloadJson, out ESAgentGoalPayload payload, out _))
                return false;
            objective = payload.objective?.Trim() ?? string.Empty;
            successCriteria = payload.successCriteria?.Trim() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(objective) && !string.IsNullOrWhiteSpace(successCriteria);
        }

        private static void ValidateNodeSchema(ESGraphNodeRecord node,
            Dictionary<ESGraphNodeTypeKey, IESGraphNodeDefinition> definitions,
            List<ESGraphValidationIssue> issues)
        {
            if (!definitions.TryGetValue(node.TypeKey, out IESGraphNodeDefinition definition)) return;
            List<ESGraphPortRecord> ports = node.ports ?? new List<ESGraphPortRecord>();
            if (ports.Count != definition.Ports.Count)
            {
                issues.Add(ESGraphValidationIssue.Error("AgentAuthoring.PortSchema",
                    "节点端口数量与领域 Profile 不一致，请重新创建该节点。", node.nodeId));
                return;
            }
            for (int i = 0; i < definition.Ports.Count; i++)
            {
                ESGraphPortDefinition expected = definition.Ports[i];
                node.TryGetPort(expected.stableKey, out ESGraphPortRecord actual);
                string expectedMeaning = ESGraphEndpointRules.ResolveMeaning(expected.meaning,
                    expected.name, expected.stableKey);
                if (actual == null || actual.direction != expected.direction || actual.capacity != expected.capacity
                    || actual.aggregation != expected.aggregation
                    || !string.Equals(actual.meaning, expectedMeaning, StringComparison.Ordinal)
                    || !string.Equals(actual.valueTypeId, expected.valueTypeId, StringComparison.Ordinal))
                {
                    issues.Add(ESGraphValidationIssue.Error("AgentAuthoring.PortSchema",
                        "节点端口规则与领域 Profile 不一致：" + expected.stableKey, node.nodeId));
                }
            }
        }

        private static void ValidateReference(ESGraphNodeRecord node, List<ESGraphValidationIssue> issues)
        {
            if (!TryRead(node.payloadJson, out ESAgentReferencePayload payload, out string error)
                || payload.schemaVersion != 1 || string.IsNullOrWhiteSpace(payload.projectPath))
            {
                issues.Add(ESGraphValidationIssue.Error("AgentAuthoring.Reference", string.IsNullOrEmpty(error)
                    ? "引用资料必须提供项目内文件路径。" : error, node.nodeId));
                return;
            }
            if (Path.IsPathRooted(payload.projectPath) || payload.projectPath.Contains(".."))
                issues.Add(ESGraphValidationIssue.Error("AgentAuthoring.ReferencePath", "Reference 只能保存安全的项目相对路径。", node.nodeId));
            else if (payload.required)
            {
                string fullPath = ESAgentArtifactGenerationWorkspace.ResolveProjectPath(payload.projectPath);
                if (!File.Exists(fullPath) && !Directory.Exists(fullPath))
                    issues.Add(ESGraphValidationIssue.Error("AgentAuthoring.ReferenceMissing",
                        "必须读取的 Reference 不存在：" + payload.projectPath, node.nodeId));
            }
        }

        private static void ValidateConstraint(ESGraphNodeRecord node, List<ESGraphValidationIssue> issues)
        {
            string contractError = string.Empty;
            if (!TryRead(node.payloadJson, out ESAgentConstraintPayload payload, out string error)
                || !ESAgentConstraintContractValidator.TryValidate(payload, out contractError))
                issues.Add(ESGraphValidationIssue.Error("AgentAuthoring.Constraint", string.IsNullOrEmpty(error)
                    ? contractError : error, node.nodeId));
        }

        private static void ValidateBranch(ESGraphNodeRecord node, List<ESGraphValidationIssue> issues)
        {
            if (!TryRead(node.payloadJson, out ESAgentBranchPayload payload, out string error)
                || payload.schemaVersion != 1 || string.IsNullOrWhiteSpace(payload.condition)
                || string.IsNullOrWhiteSpace(payload.matchedPath)
                || string.IsNullOrWhiteSpace(payload.defaultPath)
                || string.IsNullOrWhiteSpace(payload.failurePath))
                issues.Add(ESGraphValidationIssue.Error("AgentAuthoring.Branch", string.IsNullOrEmpty(error)
                    ? "分支节点必须完整声明条件、命中、默认和失败路径。" : error, node.nodeId));
        }

        private static void ValidateTraversal(ESGraphNodeRecord node, List<ESGraphValidationIssue> issues)
        {
            if (!TryRead(node.payloadJson, out ESAgentTraversePayload payload, out string error)
                || payload.schemaVersion != 1
                || !Enum.IsDefined(typeof(ESAgentTraversalOrder), payload.order)
                || payload.maxDepth < 1 || payload.maxDepth > 32
                || payload.maxItems < 1 || payload.maxItems > 512
                || string.IsNullOrWhiteSpace(payload.target)
                || string.IsNullOrWhiteSpace(payload.itemAlias)
                || string.IsNullOrWhiteSpace(payload.stopCondition)
                || string.IsNullOrWhiteSpace(payload.emptyResultAction)
                || string.IsNullOrWhiteSpace(payload.failureAction))
                issues.Add(ESGraphValidationIssue.Error("AgentAuthoring.Traversal", string.IsNullOrEmpty(error)
                    ? "遍历节点必须声明目标、顺序、硬上限、停止、空结果和失败行为。" : error, node.nodeId));
        }

        private static void ValidateAICommandOutput(ESGraphNodeRecord node, List<ESGraphValidationIssue> issues)
        {
            string contractError = string.Empty;
            if (!TryRead(node.payloadJson, out ESAgentAICommandOutputPayload payload, out string error)
                || !ESAgentOutputContractValidator.TryValidate(payload, out contractError))
            {
                issues.Add(ESGraphValidationIssue.Error("AgentAuthoring.AICommandOutput", string.IsNullOrEmpty(error)
                    ? contractError : error, node.nodeId));
                return;
            }
            if (!ESAgentArtifactPathPolicy.IsAllowedTarget(ESAgentArtifactKind.AICommand, payload.targetProjectPath, out string pathError))
                issues.Add(ESGraphValidationIssue.Error("AgentAuthoring.OutputPath", pathError, node.nodeId));
            string commandName = (payload.commandName ?? string.Empty).Trim();
            if (commandName.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                commandName = commandName.Substring(0, commandName.Length - 3);
            string expectedPath = "Assets/Plugins/ES/AICommands/" + commandName + ".md";
            if (!string.Equals(payload.targetProjectPath?.Replace('\\', '/'), expectedPath,
                    StringComparison.Ordinal))
                issues.Add(ESGraphValidationIssue.Error("AgentAuthoring.CommandTarget",
                    "AICommand 名称与目标路径必须一致：" + expectedPath, node.nodeId));
        }

        private static void ValidateSemanticAlignment(GraphAsset asset, ESGraphNodeRecord goalNode,
            List<ESGraphValidationIssue> issues)
        {
            if (!TryRead(goalNode.payloadJson, out ESAgentGoalPayload goalPayload, out _)) return;
            var goal = new ESAgentGenerationGoal
            {
                nodeId = goalNode.nodeId, title = goalPayload.title, objective = goalPayload.objective,
                context = goalPayload.context, targetUsers = goalPayload.targetUsers,
                successCriteria = goalPayload.successCriteria
            };
            var outputs = new List<ESAgentGenerationOutput>();
            foreach (ESGraphNodeRecord node in asset.Nodes)
            {
                if (node == null) continue;
                if (string.Equals(node.typeId, ESAgentGraphStableIds.AICommandOutputNode,
                        StringComparison.Ordinal)
                    && TryRead(node.payloadJson, out ESAgentAICommandOutputPayload command, out _))
                    outputs.Add(new ESAgentGenerationOutput
                    {
                        nodeId = node.nodeId, artifactKind = ESAgentArtifactKind.AICommand,
                        artifactName = command.commandName, requirements = command.purpose,
                        acceptanceCriteria = command.acceptanceCriteria, executionOutline = command.executionOutline
                    });
                else if (string.Equals(node.typeId, ESAgentGraphStableIds.AISkillOutputNode,
                             StringComparison.Ordinal)
                    && TryRead(node.payloadJson, out ESAgentSkillOutputPayload skill, out _))
                    outputs.Add(new ESAgentGenerationOutput
                    {
                        nodeId = node.nodeId, artifactKind = ESAgentArtifactKind.AgentSkill,
                        artifactName = skill.skillName, requirements = skill.description,
                        skillDescription = skill.description, skillTriggerScenarios = skill.triggerScenarios,
                        skillWorkflow = skill.workflow, skillOutputContract = skill.outputContract
                    });
            }
            if (!ESAgentGenerationSemanticValidator.TryValidate(goal, outputs, out string error))
                issues.Add(ESGraphValidationIssue.Error("AgentAuthoring.SemanticAlignment", error,
                    goalNode.nodeId, true));
        }

        private static void ValidateAgentSkillOutput(ESGraphNodeRecord node, List<ESGraphValidationIssue> issues)
        {
            string contractError = string.Empty;
            if (!TryRead(node.payloadJson, out ESAgentSkillOutputPayload payload, out string error)
                || !ESAgentOutputContractValidator.TryValidate(payload, out contractError))
            {
                issues.Add(ESGraphValidationIssue.Error("AgentAuthoring.AgentSkillOutput", string.IsNullOrEmpty(error)
                    ? contractError : error, node.nodeId));
                return;
            }
            if (!string.Equals(payload.skillName, payload.skillName.ToLowerInvariant(), StringComparison.Ordinal)
                || !System.Text.RegularExpressions.Regex.IsMatch(payload.skillName, "^[a-z0-9-]+$"))
                issues.Add(ESGraphValidationIssue.Error("AgentAuthoring.SkillName", "Skill 名称只允许小写字母、数字和连字符。", node.nodeId));
            if (!ESAgentArtifactPathPolicy.IsAllowedTarget(ESAgentArtifactKind.AgentSkill, payload.targetProjectPath, out string pathError))
                issues.Add(ESGraphValidationIssue.Error("AgentAuthoring.OutputPath", pathError, node.nodeId));
            string expected = ".agents/skills/" + payload.skillName + "/";
            if (!string.Equals(payload.targetProjectPath.Replace('\\', '/'), expected, StringComparison.Ordinal))
                issues.Add(ESGraphValidationIssue.Error("AgentAuthoring.SkillTarget", "Skill 名称与目标目录必须一致：" + expected, node.nodeId));
        }

        private static void ValidateValidation(ESGraphNodeRecord node, List<ESGraphValidationIssue> issues)
        {
            if (!TryRead(node.payloadJson, out ESAgentValidationPayload payload, out string error)
                || payload.schemaVersion != 1)
            {
                issues.Add(ESGraphValidationIssue.Error("AgentAuthoring.Validation", string.IsNullOrEmpty(error)
                    ? "Validation SchemaVersion 无效。" : error, node.nodeId));
                return;
            }
            if (!payload.requireDiffReview || !payload.requireHumanApproval)
                issues.Add(ESGraphValidationIssue.Error("AgentAuthoring.ApprovalPolicy", "Diff Review 与人工批准不得关闭。", node.nodeId));
        }

        private static ESGraphNodeRecord FindSingle(GraphAsset asset, string nodeTypeId)
        {
            ESGraphNodeRecord result = null;
            for (int i = 0; i < asset.Nodes.Count; i++)
                if (asset.Nodes[i] != null && string.Equals(asset.Nodes[i].typeId, nodeTypeId,
                        StringComparison.Ordinal))
                    result = asset.Nodes[i];
            return result;
        }

        private static void ValidateTransitions(GraphAsset asset,
            Dictionary<ESGraphNodeTypeKey, IESGraphNodeDefinition> definitions,
            List<ESGraphValidationIssue> issues)
        {
            var nodeByPort = new Dictionary<string, ESGraphNodeRecord>(StringComparer.Ordinal);
            var portById = new Dictionary<string, ESGraphPortRecord>(StringComparer.Ordinal);
            foreach (ESGraphNodeRecord node in asset.Nodes)
            {
                if (node == null) continue;
                foreach (ESGraphPortRecord port in node.ports ?? new List<ESGraphPortRecord>())
                    if (port != null && !string.IsNullOrEmpty(port.portId))
                    {
                        nodeByPort[port.portId] = node;
                        portById[port.portId] = port;
                    }
            }

            foreach (ESGraphEdgeRecord edge in asset.Edges)
            {
                if (edge == null || !nodeByPort.TryGetValue(edge.outputPortId, out ESGraphNodeRecord from)
                    || !nodeByPort.TryGetValue(edge.inputPortId, out ESGraphNodeRecord to)
                    || !portById.TryGetValue(edge.outputPortId, out ESGraphPortRecord output)) continue;
                if (!definitions.TryGetValue(from.TypeKey, out IESGraphNodeDefinition fromDefinition)
                    || !definitions.TryGetValue(to.TypeKey, out IESGraphNodeDefinition toDefinition))
                    continue;
                if (!ESAgentRelationSemantics.TryResolve(from.typeId, to.typeId, output.stableKey, out _))
                    issues.Add(ESGraphValidationIssue.Error("AgentAuthoring.Transition",
                        "不允许的节点关系："
                        + ESGraphChinesePresentation.GetNodeCategoryName(fromDefinition.Category) + " → "
                        + ESGraphChinesePresentation.GetNodeCategoryName(toDefinition.Category), edge.edgeId));
            }
        }

        private static void ValidateLogicRoutes(GraphAsset asset, List<ESGraphValidationIssue> issues)
        {
            var edgeCountByOutput = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (ESGraphEdgeRecord edge in asset.Edges)
            {
                if (edge == null || string.IsNullOrEmpty(edge.outputPortId)) continue;
                edgeCountByOutput.TryGetValue(edge.outputPortId, out int count);
                edgeCountByOutput[edge.outputPortId] = count + 1;
            }
            foreach (ESGraphNodeRecord node in asset.Nodes)
            {
                if (node == null) continue;
                string[] requiredKeys;
                if (string.Equals(node.typeId, ESAgentGraphStableIds.BranchNode, StringComparison.Ordinal))
                    requiredKeys = new[] { ESAgentGraphStableIds.BranchMatchedPortKey,
                        ESAgentGraphStableIds.BranchDefaultPortKey, ESAgentGraphStableIds.BranchFailurePortKey };
                else if (string.Equals(node.typeId, ESAgentGraphStableIds.TraverseNode, StringComparison.Ordinal))
                    requiredKeys = new[] { ESAgentGraphStableIds.TraverseItemPortKey,
                        ESAgentGraphStableIds.TraverseCompletedPortKey, ESAgentGraphStableIds.TraverseFailurePortKey };
                else
                    continue;

                for (int i = 0; i < requiredKeys.Length; i++)
                {
                    node.TryGetPort(requiredKeys[i], out ESGraphPortRecord port);
                    int count = port != null && edgeCountByOutput.TryGetValue(port.portId, out int value) ? value : 0;
                    if (count < 1)
                        issues.Add(ESGraphValidationIssue.Error("AgentAuthoring.LogicRoute",
                            "逻辑节点出口至少需要连接一次：" + requiredKeys[i], node.nodeId));
                }
            }
        }
    }

    public sealed class ESAgentArtifactGenerationBaker : IESGraphPlanBaker<ESAgentArtifactGenerationSpec>
    {
        public ESGraphDomainKey Domain => ESAgentGraphStableIds.Domain;

        public bool TryBake(ESBakedGraphSnapshot source, out ESAgentArtifactGenerationSpec plan,
            out IReadOnlyList<ESGraphValidationIssue> issues)
        {
            plan = null;
            if (!ESGraphPlanBakeGuard.TryValidateSource(source, Domain, out issues)) return false;
            var failures = new List<ESGraphValidationIssue>();
            if (source.AllowCycles)
                failures.Add(ESGraphValidationIssue.Error("AgentAuthoring.CyclePolicy", "智能助手编排图禁止循环。"));
            var references = new List<ESAgentGenerationReference>();
            var constraints = new List<ESAgentGenerationConstraint>();
            var branches = new List<ESAgentGenerationBranch>();
            var traversals = new List<ESAgentGenerationTraversal>();
            var outputs = new List<ESAgentGenerationOutput>();
            var validations = new List<ESAgentGenerationValidation>();
            var relations = new List<ESAgentGenerationRelation>();
            ESAgentGenerationGoal goal = null;
            foreach (ESGraphNodeSnapshot node in source.Nodes)
            {
                switch (node.TypeId)
                {
                    case ESAgentGraphStableIds.GoalNode:
                        if (goal != null)
                        {
                            failures.Add(ESGraphValidationIssue.Error("AgentAuthoring.Goal.Bake", "只能烘焙一个 Goal。", node.NodeId));
                            break;
                        }
                        if (!ESAgentAuthoringGraphValidator.TryRead(node.PayloadJson, out ESAgentGoalPayload gp, out string ge)
                            || gp.schemaVersion != 1 || string.IsNullOrWhiteSpace(gp.title)
                            || string.IsNullOrWhiteSpace(gp.objective) || string.IsNullOrWhiteSpace(gp.successCriteria))
                            failures.Add(ESGraphValidationIssue.Error("AgentAuthoring.Goal.Bake", string.IsNullOrEmpty(ge) ? "Goal 无法烘焙。" : ge, node.NodeId));
                        else goal = new ESAgentGenerationGoal { nodeId = node.NodeId, title = gp.title, objective = gp.objective,
                            context = gp.context, targetUsers = gp.targetUsers, successCriteria = gp.successCriteria };
                        break;
                    case ESAgentGraphStableIds.ReferenceNode:
                        if (!ESAgentAuthoringGraphValidator.TryRead(node.PayloadJson, out ESAgentReferencePayload rp, out string re)
                            || rp.schemaVersion != 1 || string.IsNullOrWhiteSpace(rp.projectPath)
                            || Path.IsPathRooted(rp.projectPath) || rp.projectPath.Contains(".."))
                            failures.Add(ESGraphValidationIssue.Error("AgentAuthoring.Reference.Bake", string.IsNullOrEmpty(re) ? "Reference 无法烘焙。" : re, node.NodeId));
                        else
                        {
                            string referenceFullPath = ESAgentArtifactGenerationWorkspace.ResolveProjectPath(rp.projectPath);
                            if (rp.required && !File.Exists(referenceFullPath) && !Directory.Exists(referenceFullPath))
                                failures.Add(ESGraphValidationIssue.Error("AgentAuthoring.Reference.Bake", "必须读取的 Reference 不存在：" + rp.projectPath, node.NodeId));
                            else references.Add(new ESAgentGenerationReference { nodeId = node.NodeId, referenceKind = rp.referenceKind,
                                projectPath = rp.projectPath, purpose = rp.purpose, required = rp.required });
                        }
                        break;
                    case ESAgentGraphStableIds.ConstraintNode:
                        string constraintContractError = string.Empty;
                        if (!ESAgentAuthoringGraphValidator.TryRead(node.PayloadJson, out ESAgentConstraintPayload cp, out string ce)
                            || !ESAgentConstraintContractValidator.TryValidate(cp, out constraintContractError))
                            failures.Add(ESGraphValidationIssue.Error("AgentAuthoring.Constraint.Bake", string.IsNullOrEmpty(ce)
                                ? constraintContractError : ce, node.NodeId));
                        else constraints.Add(new ESAgentGenerationConstraint { nodeId = node.NodeId, kind = cp.kind,
                            scope = cp.scope, combinationMode = cp.combinationMode, priority = cp.priority,
                            combinationGroup = cp.combinationGroup, statement = cp.statement,
                            rationale = cp.rationale, verification = cp.verification });
                        break;
                    case ESAgentGraphStableIds.BranchNode:
                        if (!ESAgentAuthoringGraphValidator.TryRead(node.PayloadJson,
                                out ESAgentBranchPayload branch, out string branchError)
                            || branch.schemaVersion != 1 || string.IsNullOrWhiteSpace(branch.condition)
                            || string.IsNullOrWhiteSpace(branch.matchedPath)
                            || string.IsNullOrWhiteSpace(branch.defaultPath)
                            || string.IsNullOrWhiteSpace(branch.failurePath))
                            failures.Add(ESGraphValidationIssue.Error("AgentAuthoring.Branch.Bake",
                                string.IsNullOrEmpty(branchError) ? "Branch 无法烘焙。" : branchError,
                                node.NodeId));
                        else branches.Add(new ESAgentGenerationBranch { nodeId = node.NodeId,
                            condition = branch.condition, matchedPath = branch.matchedPath,
                            defaultPath = branch.defaultPath, failurePath = branch.failurePath,
                            matchedTargetNodeIds = RouteTargetIds(source, node.NodeId,
                                ESAgentGraphStableIds.BranchMatchedPortKey),
                            defaultTargetNodeIds = RouteTargetIds(source, node.NodeId,
                                ESAgentGraphStableIds.BranchDefaultPortKey),
                            failureTargetNodeIds = RouteTargetIds(source, node.NodeId,
                                ESAgentGraphStableIds.BranchFailurePortKey) });
                        break;
                    case ESAgentGraphStableIds.TraverseNode:
                        if (!ESAgentAuthoringGraphValidator.TryRead(node.PayloadJson,
                                out ESAgentTraversePayload traversal, out string traversalError)
                            || traversal.schemaVersion != 1
                            || !Enum.IsDefined(typeof(ESAgentTraversalOrder), traversal.order)
                            || traversal.maxDepth < 1 || traversal.maxDepth > 32
                            || traversal.maxItems < 1 || traversal.maxItems > 512
                            || string.IsNullOrWhiteSpace(traversal.target)
                            || string.IsNullOrWhiteSpace(traversal.itemAlias)
                            || string.IsNullOrWhiteSpace(traversal.stopCondition)
                            || string.IsNullOrWhiteSpace(traversal.emptyResultAction)
                            || string.IsNullOrWhiteSpace(traversal.failureAction))
                            failures.Add(ESGraphValidationIssue.Error("AgentAuthoring.Traversal.Bake",
                                string.IsNullOrEmpty(traversalError) ? "Traversal 无法烘焙。" : traversalError,
                                node.NodeId));
                        else traversals.Add(new ESAgentGenerationTraversal { nodeId = node.NodeId,
                            target = traversal.target, itemAlias = traversal.itemAlias, order = traversal.order,
                            maxDepth = traversal.maxDepth, maxItems = traversal.maxItems,
                            stopCondition = traversal.stopCondition,
                            emptyResultAction = traversal.emptyResultAction,
                            failureAction = traversal.failureAction });
                        break;
                    case ESAgentGraphStableIds.AICommandOutputNode:
                        string commandContractError = string.Empty;
                        if (!ESAgentAuthoringGraphValidator.TryRead(node.PayloadJson, out ESAgentAICommandOutputPayload command, out string commandError)
                            || !ESAgentOutputContractValidator.TryValidate(command, out commandContractError))
                        {
                            failures.Add(ESGraphValidationIssue.Error("AgentAuthoring.Output.Bake", string.IsNullOrEmpty(commandError)
                                ? commandContractError : commandError, node.NodeId));
                            break;
                        }
                        if (!ESAgentArtifactPathPolicy.IsAllowedTarget(ESAgentArtifactKind.AICommand, command.targetProjectPath, out string commandPathError))
                        {
                            failures.Add(ESGraphValidationIssue.Error("AgentAuthoring.Output.Bake", commandPathError, node.NodeId));
                            break;
                        }
                        outputs.Add(new ESAgentGenerationOutput { nodeId = node.NodeId, artifactKind = ESAgentArtifactKind.AICommand,
                            artifactId = ESAgentArtifactIdentity.Create(source.GraphId, node.NodeId),
                            artifactName = command.commandName, targetProjectPath = command.targetProjectPath,
                            operationMode = command.operationMode,
                            requirements = command.purpose + "\nRequired sections:\n" + command.requiredSections,
                            commandIntent = command.commandIntent, writeAuthorization = command.writeAuthorization,
                            commandRiskLevel = command.riskLevel, failurePolicy = command.failurePolicy,
                            commandType = ESAgentSemanticPresentation.CommandIntent(command.commandIntent),
                            defaultWrite = ESAgentSemanticPresentation.WriteAuthorization(command.writeAuthorization)
                                + "；" + command.allowedWriteScopes,
                            riskLevel = ESAgentSemanticPresentation.RiskLevel(command.riskLevel),
                            expectedInputs = command.expectedInputs, executionOutline = command.executionOutline,
                            preconditions = command.preconditions, allowedWriteScopes = command.allowedWriteScopes,
                            forbiddenOperations = command.forbiddenOperations,
                            acceptanceCriteria = command.acceptanceCriteria,
                            requiredEvidence = command.requiredEvidence, blockedHandling = command.blockedHandling,
                            rollbackStrategy = command.rollbackStrategy });
                        break;
                    case ESAgentGraphStableIds.AISkillOutputNode:
                        string skillContractError = string.Empty;
                        if (!ESAgentAuthoringGraphValidator.TryRead(node.PayloadJson, out ESAgentSkillOutputPayload skill, out string skillError)
                            || !ESAgentOutputContractValidator.TryValidate(skill, out skillContractError))
                        {
                            failures.Add(ESGraphValidationIssue.Error("AgentAuthoring.Output.Bake", string.IsNullOrEmpty(skillError)
                                ? skillContractError : skillError, node.NodeId));
                            break;
                        }
                        if (!ESAgentArtifactPathPolicy.IsAllowedTarget(ESAgentArtifactKind.AgentSkill, skill.targetProjectPath, out string skillPathError))
                        {
                            failures.Add(ESGraphValidationIssue.Error("AgentAuthoring.Output.Bake", skillPathError, node.NodeId));
                            break;
                        }
                        string expectedSkillTarget = ".agents/skills/" + skill.skillName + "/";
                        if (!System.Text.RegularExpressions.Regex.IsMatch(skill.skillName ?? string.Empty, "^[a-z0-9-]+$")
                            || !string.Equals(skill.targetProjectPath.Replace('\\', '/'), expectedSkillTarget, StringComparison.Ordinal))
                        {
                            failures.Add(ESGraphValidationIssue.Error("AgentAuthoring.Output.Bake",
                                "Agent Skill 名称与目标目录必须一致：" + expectedSkillTarget, node.NodeId));
                            break;
                        }
                        outputs.Add(new ESAgentGenerationOutput { nodeId = node.NodeId, artifactKind = ESAgentArtifactKind.AgentSkill,
                            artifactId = ESAgentArtifactIdentity.Create(source.GraphId, node.NodeId),
                            artifactName = skill.skillName, targetProjectPath = skill.targetProjectPath,
                            operationMode = skill.operationMode,
                            requirements = skill.description, skillEffectKind = skill.effectKind,
                            skillIdempotency = skill.idempotency, skillDescription = skill.description,
                            skillTriggerScenarios = skill.triggerScenarios,
                            skillNonTriggerScenarios = skill.nonTriggerScenarios,
                            skillPreconditions = skill.preconditions,
                            skillRequiredDependencies = skill.requiredDependencies,
                            skillInputContract = skill.inputContract, skillWorkflow = skill.workflow,
                            skillOutputContract = skill.outputContract, skillSideEffects = skill.sideEffects,
                            skillNonGoals = skill.nonGoals, skillFailureRecovery = skill.failureRecovery,
                            skillValidationSteps = skill.validationSteps,
                            skillPermissionBoundary = skill.permissionBoundary,
                            defaultPrompt = skill.defaultPrompt, includeAgentsMetadata = skill.includeAgentsMetadata,
                            includeReferences = skill.includeReferences, includeScripts = skill.includeScripts });
                        break;
                    case ESAgentGraphStableIds.ValidationNode:
                        if (!ESAgentAuthoringGraphValidator.TryRead(node.PayloadJson, out ESAgentValidationPayload vp, out string ve)
                            || vp.schemaVersion != 1 || !vp.requireDiffReview || !vp.requireHumanApproval)
                            failures.Add(ESGraphValidationIssue.Error("AgentAuthoring.Validation.Bake", string.IsNullOrEmpty(ve) ? "检查节点必须保留候选差异检查和人工批准。" : ve, node.NodeId));
                        else validations.Add(new ESAgentGenerationValidation { nodeId = node.NodeId, validateAICommand = vp.validateAICommand,
                            validateAgentSkill = vp.validateAgentSkill, validateUtf8 = vp.validateUtf8, requireDiffReview = true,
                            requireHumanApproval = true, additionalRequirements = vp.additionalRequirements,
                            reviewChecklist = vp.reviewChecklist });
                        break;
                    default:
                        failures.Add(ESGraphValidationIssue.Error("AgentAuthoring.NodeType.Bake", "未知节点类型：" + node.TypeId, node.NodeId));
                        break;
                }
            }
            if (goal == null) failures.Add(ESGraphValidationIssue.Error("AgentAuthoring.GoalMissing", "必须且只能烘焙一个 Goal。"));
            if (outputs.Count == 0) failures.Add(ESGraphValidationIssue.Error("AgentAuthoring.OutputMissing", "至少需要一个输出。"));
            if (validations.Count == 0) failures.Add(ESGraphValidationIssue.Error("AgentAuthoring.ValidationMissing", "至少需要一个验证策略。"));
            BakeRelations(source, relations, failures);
            if (failures.Count > 0) { issues = failures; return false; }
            var candidate = new ESAgentArtifactGenerationSpec { sourceGraphId = source.GraphId,
                sourceOriginGraphId = source.OriginGraphId, sourceContentSignature = source.ContentSignature, goal = goal,
                references = references.ToArray(), constraints = constraints.ToArray(), outputs = outputs.ToArray(),
                branches = branches.ToArray(), traversals = traversals.ToArray(),
                validations = validations.ToArray(), relations = relations
                    .OrderBy(relation => relation.order)
                    .ThenBy(relation => relation.edgeId, StringComparer.Ordinal).ToArray() };
            candidate.skillBundle = ESAgentSkillBundleContract.Create(candidate.sourceGraphId,
                goal.title, goal.nodeId, candidate.references, candidate.constraints,
                candidate.branches, candidate.traversals,
                candidate.outputs, candidate.validations);
            if (!ESAgentGenerationIntentValidator.TryValidate(candidate, out string intentError))
            {
                failures.Add(ESGraphValidationIssue.Error("AgentAuthoring.Intent.Bake", intentError));
                issues = failures;
                return false;
            }
            plan = candidate;
            issues = failures;
            return true;
        }

        private static void BakeRelations(ESBakedGraphSnapshot source, List<ESAgentGenerationRelation> relations,
            List<ESGraphValidationIssue> failures)
        {
            foreach (ESGraphRouteSnapshot route in source.Routes)
            {
                if (route.SourceNode == null || route.TargetNode == null
                    || route.SourcePort == null || route.TargetPort == null)
                {
                    failures.Add(ESGraphValidationIssue.Error("AgentAuthoring.Relation.Bake",
                        "无法解析思路图关系。", route.EdgeId));
                    continue;
                }
                if (!ESAgentRelationSemantics.TryResolve(route.SourceNode.TypeId,
                        route.TargetNode.TypeId, route.SourcePortKey,
                        out ESAgentRelationKind relationKind))
                {
                    failures.Add(ESGraphValidationIssue.Error("AgentAuthoring.Relation.Semantics",
                        "无法确定思路图关系语义。", route.EdgeId));
                    continue;
                }
                relations.Add(new ESAgentGenerationRelation
                {
                    edgeId = route.EdgeId,
                    order = route.Order,
                    fromNodeId = route.SourceNodeId,
                    fromNodeTypeId = route.SourceNode.TypeId,
                    fromNodeTitle = route.SourceNode.Title,
                    fromPortStableKey = route.SourcePortKey,
                    fromPortMeaning = route.SourceMeaning,
                    toNodeId = route.TargetNodeId,
                    toNodeTypeId = route.TargetNode.TypeId,
                    toNodeTitle = route.TargetNode.Title,
                    toPortStableKey = route.TargetPortKey,
                    toPortMeaning = route.TargetMeaning,
                    relationKind = relationKind,
                    semanticType = route.SourceValueTypeId,
                    sourceValueTypeId = route.SourceValueTypeId,
                    targetValueTypeId = route.TargetValueTypeId,
                    sourceAggregation = route.SourceAggregation,
                    targetAggregation = route.TargetAggregation
                });
            }
        }

        private static string[] RouteTargetIds(ESBakedGraphSnapshot source, string nodeId,
            string portKey)
        {
            return (source?.GetOutgoingRoutes(nodeId, portKey)
                    ?? Array.Empty<ESGraphRouteSnapshot>())
                .Select(route => route.TargetNodeId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

    }

    public static class ESAgentArtifactPathPolicy
    {
        public static bool IsAllowedTarget(ESAgentArtifactKind kind, string path, out string error)
        {
            string normalized = (path ?? string.Empty).Replace('\\', '/').Trim();
            if (string.IsNullOrEmpty(normalized) || Path.IsPathRooted(normalized) || normalized.Contains(".."))
            { error = "目标必须是无 .. 的项目相对路径。"; return false; }
            if (kind == ESAgentArtifactKind.AICommand
                && normalized.StartsWith("Assets/Plugins/ES/AICommands/", StringComparison.Ordinal)
                && normalized.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            { error = string.Empty; return true; }
            if (kind == ESAgentArtifactKind.AgentSkill
                && normalized.StartsWith(".agents/skills/es-", StringComparison.Ordinal)
                && normalized.EndsWith("/", StringComparison.Ordinal))
            {
                string folder = normalized.Substring(".agents/skills/".Length).TrimEnd('/');
                if (folder.IndexOf('/') < 0)
                { error = string.Empty; return true; }
            }
            error = kind == ESAgentArtifactKind.AICommand
                ? "AICommand 目标必须位于 Assets/Plugins/ES/AICommands 且为 .md。"
                : "Agent Skill 目标必须是 .agents/skills/es-*/ 直接子目录。";
            return false;
        }
    }

    public static class ESAgentAuthoringAssetCatalog
    {
        private static readonly string[] FirstPartyAssetRoots =
        {
            "Assets/ESNormalAssets",
            "Assets/Plugins/ES",
            "Assets/Scripts"
        };

        private static readonly Dictionary<ESAgentReferenceKind, List<string>> ReferenceCache =
            new Dictionary<ESAgentReferenceKind, List<string>>();
        private static List<string> aiCommandTargetCache;
        private static List<string> agentSkillTargetCache;

        public static List<string> GetReferencePaths(ESAgentReferenceKind kind, string currentPath = null,
            bool forceRefresh = false)
        {
            if (!forceRefresh && ReferenceCache.TryGetValue(kind, out List<string> cached))
                return WithCurrent(cached, currentPath);
            IEnumerable<string> paths;
            switch (kind)
            {
                case ESAgentReferenceKind.AIWarning:
                    paths = FindAssetPaths("Assets/Plugins/ES/AIWarnings", path => path.EndsWith(".md", StringComparison.OrdinalIgnoreCase));
                    break;
                case ESAgentReferenceKind.AICommand:
                    paths = FindAssetPaths("Assets/Plugins/ES/AICommands", path => path.EndsWith(".md", StringComparison.OrdinalIgnoreCase));
                    break;
                case ESAgentReferenceKind.AgentSkill:
                    paths = EnumerateSkillFiles();
                    break;
                case ESAgentReferenceKind.CSharpSource:
                    paths = FindAssetPaths(FirstPartyAssetRoots,
                        path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase));
                    break;
                case ESAgentReferenceKind.Documentation:
                    paths = EnumerateProjectFiles(new[] { "Documentation", "ES/Documentation" }, ".md");
                    break;
                case ESAgentReferenceKind.ProjectAsset:
                    paths = FindAssetPaths(FirstPartyAssetRoots,
                        path => !AssetDatabase.IsValidFolder(path)
                                && !path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase));
                    break;
                default:
                    paths = Array.Empty<string>();
                    break;
            }
            var result = paths.Distinct(StringComparer.Ordinal).OrderBy(path => path, StringComparer.Ordinal).ToList();
            ReferenceCache[kind] = result;
            return WithCurrent(result, currentPath);
        }

        public static List<string> GetAICommandTargets(string currentPath = null, bool forceRefresh = false)
        {
            if (forceRefresh || aiCommandTargetCache == null)
                aiCommandTargetCache = FindAssetPaths("Assets/Plugins/ES/AICommands",
                    path => path.EndsWith(".md", StringComparison.OrdinalIgnoreCase)).ToList();
            return WithCurrent(aiCommandTargetCache, currentPath);
        }

        public static List<string> GetAgentSkillTargets(string currentPath = null, bool forceRefresh = false)
        {
            if (forceRefresh || agentSkillTargetCache == null)
            {
                string root = Path.Combine(ESAgentArtifactGenerationWorkspace.GetProjectRoot(), ".agents", "skills");
                agentSkillTargetCache = (Directory.Exists(root)
                        ? Directory.EnumerateDirectories(root, "es-*", SearchOption.TopDirectoryOnly)
                            .Select(path => ".agents/skills/" + Path.GetFileName(path) + "/")
                        : Array.Empty<string>())
                    .OrderBy(path => path, StringComparer.Ordinal).ToList();
            }
            return WithCurrent(agentSkillTargetCache, currentPath);
        }

        private static IEnumerable<string> FindAssetPaths(string root, Func<string, bool> predicate)
        {
            return FindAssetPaths(new[] { root }, predicate);
        }

        private static IEnumerable<string> FindAssetPaths(IEnumerable<string> roots, Func<string, bool> predicate)
        {
            string[] validRoots = roots.Where(AssetDatabase.IsValidFolder).ToArray();
            if (validRoots.Length == 0) return Array.Empty<string>();
            return AssetDatabase.FindAssets(string.Empty, validRoots).Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => !string.IsNullOrEmpty(path) && predicate(path.Replace('\\', '/')))
                .Select(Normalize);
        }

        private static IEnumerable<string> EnumerateSkillFiles()
        {
            return EnumerateProjectFiles(new[] { ".agents/skills" }, new[] { ".md", ".yaml", ".yml", ".ps1", ".py" });
        }

        private static IEnumerable<string> EnumerateProjectFiles(IEnumerable<string> roots, params string[] extensions)
        {
            string projectRoot = ESAgentArtifactGenerationWorkspace.GetProjectRoot();
            foreach (string relativeRoot in roots)
            {
                string fullRoot = Path.Combine(projectRoot, relativeRoot.Replace('/', Path.DirectorySeparatorChar));
                if (!Directory.Exists(fullRoot)) continue;
                foreach (string file in ESManagedFileIO.EnumerateFilesSafely(fullRoot, "*"))
                    if (extensions.Any(extension => file.EndsWith(extension, StringComparison.OrdinalIgnoreCase)))
                        yield return Normalize(file.Substring(projectRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            }
        }

        private static List<string> WithCurrent(IEnumerable<string> paths, string currentPath)
        {
            var result = paths.Distinct(StringComparer.Ordinal).OrderBy(path => path, StringComparer.Ordinal).ToList();
            string current = Normalize(currentPath);
            if (!string.IsNullOrEmpty(current) && !result.Contains(current)) result.Insert(0, current);
            if (result.Count == 0) result.Add(string.IsNullOrEmpty(current) ? "<没有可用项>" : current);
            return result;
        }

        private static string Normalize(string path) { return (path ?? string.Empty).Replace('\\', '/'); }
    }

    public static class ESAgentAuthoringGraphSchema
    {
        public static bool TryRepairPorts(GraphAsset asset, out string error)
        {
            if (asset == null || !string.Equals(asset.DomainId, ESAgentGraphStableIds.DomainId,
                    StringComparison.Ordinal))
            {
                error = "只能修复智能助手编排图。";
                return false;
            }
            var definitions = ESGraphAuthoringRegistry.GetNodeDefinitions(asset.DomainKey)
                .ToDictionary(definition => definition.NodeType);
            var repairs = new List<KeyValuePair<ESGraphPortRecord, ESGraphPortDefinition>>();
            foreach (ESGraphNodeRecord node in asset.Nodes)
            {
                if (node == null || !definitions.TryGetValue(node.TypeKey, out IESGraphNodeDefinition definition)) continue;
                if (node.ports == null || node.ports.Count != definition.Ports.Count)
                {
                    error = "节点端口数量无法安全迁移，请重新创建节点：" + (node.title ?? node.nodeId);
                    return false;
                }
                for (int i = 0; i < definition.Ports.Count; i++)
                {
                    ESGraphPortDefinition expected = definition.Ports[i];
                    if (expected == null || !node.TryGetPort(expected.stableKey,
                            out ESGraphPortRecord port))
                    {
                        error = "节点缺少唯一稳定端点，无法安全修复："
                            + (node.title ?? node.nodeId) + "/" + (expected?.stableKey ?? "<null>");
                        return false;
                    }
                    repairs.Add(new KeyValuePair<ESGraphPortRecord, ESGraphPortDefinition>(port, expected));
                }
            }
            for (int i = 0; i < repairs.Count; i++)
            {
                ESGraphPortRecord port = repairs[i].Key;
                ESGraphPortDefinition expected = repairs[i].Value;
                port.name = expected.name;
                port.stableKey = expected.stableKey;
                port.meaning = ESGraphEndpointRules.ResolveMeaning(expected.meaning,
                    expected.name, expected.stableKey);
                port.valueTypeId = expected.valueTypeId;
                port.direction = expected.direction;
                port.capacity = expected.capacity;
                port.aggregation = expected.aggregation;
            }
            error = string.Empty;
            return true;
        }
    }

}
