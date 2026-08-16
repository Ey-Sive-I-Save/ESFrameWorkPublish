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
    public sealed class ESAgentGenerationGoal
    {
        public string nodeId;
        public string title;
        public string objective;
        public string context;
        public string targetUsers;
        public string successCriteria;
    }

    [Serializable]
    public sealed class ESAgentGenerationReference
    {
        public string nodeId;
        public ESAgentReferenceKind referenceKind;
        public string projectPath;
        public string purpose;
        public bool required;
    }

    [Serializable]
    public sealed class ESAgentGenerationConstraint
    {
        public string nodeId;
        public ESAgentConstraintKind kind;
        public ESAgentConstraintScope scope;
        public ESAgentConstraintCombinationMode combinationMode;
        public int priority;
        public string combinationGroup;
        public string statement;
        public string rationale;
        public string verification;
    }

    [Serializable]
    public sealed class ESAgentGenerationBranch
    {
        public string nodeId;
        public string condition;
        public string matchedPath;
        public string defaultPath;
        public string failurePath;
        public string[] matchedTargetNodeIds = Array.Empty<string>();
        public string[] defaultTargetNodeIds = Array.Empty<string>();
        public string[] failureTargetNodeIds = Array.Empty<string>();
    }

    [Serializable]
    public sealed class ESAgentGenerationTraversal
    {
        public string nodeId;
        public string target;
        public string itemAlias;
        public ESAgentTraversalOrder order;
        public int maxDepth;
        public int maxItems;
        public string stopCondition;
        public string emptyResultAction;
        public string failureAction;
    }

    [Serializable]
    public sealed class ESAgentGenerationOutput
    {
        public string nodeId;
        public ESAgentArtifactKind artifactKind;
        public string artifactId;
        public string artifactName;
        public string targetProjectPath;
        public ESAgentArtifactOperationMode operationMode;
        public ESAgentArtifactResolvedOperation resolvedOperation;
        public string requirements;
        public ESAgentCommandIntent commandIntent;
        public ESAgentWriteAuthorization writeAuthorization;
        public ESAgentRiskLevel commandRiskLevel;
        public ESAgentFailurePolicy failurePolicy;
        public string commandType;
        public string defaultWrite;
        public string riskLevel;
        public string expectedInputs;
        public string preconditions;
        public string allowedWriteScopes;
        public string forbiddenOperations;
        public string executionOutline;
        public string acceptanceCriteria;
        public string requiredEvidence;
        public string blockedHandling;
        public string rollbackStrategy;
        public ESAgentSkillEffectKind skillEffectKind;
        public ESAgentSkillIdempotency skillIdempotency;
        public string skillDescription;
        public string skillTriggerScenarios;
        public string skillNonTriggerScenarios;
        public string skillPreconditions;
        public string skillRequiredDependencies;
        public string skillInputContract;
        public string skillWorkflow;
        public string skillOutputContract;
        public string skillSideEffects;
        public string skillNonGoals;
        public string skillFailureRecovery;
        public string skillValidationSteps;
        public string skillPermissionBoundary;
        public string defaultPrompt;
        public bool includeAgentsMetadata;
        public bool includeReferences;
        public bool includeScripts;
    }

    public static class ESAgentGenerationContractValidator
    {
        public static bool TryValidate(ESAgentGenerationOutput output, out string error)
        {
            if (output == null || !Enum.IsDefined(typeof(ESAgentArtifactKind), output.artifactKind)
                || !Enum.IsDefined(typeof(ESAgentArtifactOperationMode), output.operationMode))
            {
                error = "Generation Output 的产物类型或操作方式非法。";
                return false;
            }
            if (output.artifactKind == ESAgentArtifactKind.AICommand)
            {
                if (!Enum.IsDefined(typeof(ESAgentCommandIntent), output.commandIntent)
                    || !Enum.IsDefined(typeof(ESAgentWriteAuthorization), output.writeAuthorization)
                    || !Enum.IsDefined(typeof(ESAgentRiskLevel), output.commandRiskLevel)
                    || !Enum.IsDefined(typeof(ESAgentFailurePolicy), output.failurePolicy))
                {
                    error = "AICommand Generation Output 的结构化语义非法。";
                    return false;
                }
                if (output.commandIntent != ESAgentCommandIntent.ControlledExecution
                    && output.writeAuthorization == ESAgentWriteAuthorization.ScopedWrites)
                {
                    error = "AICommand Generation Output 存在意图与写入授权冲突。";
                    return false;
                }
                if (output.writeAuthorization == ESAgentWriteAuthorization.ScopedWrites
                    && output.failurePolicy != ESAgentFailurePolicy.RollbackAndReport)
                {
                    error = "AICommand Generation Output 缺少与写入授权匹配的回滚策略。";
                    return false;
                }
                if (Missing(output.artifactName, output.targetProjectPath, output.requirements,
                        output.commandType, output.defaultWrite, output.riskLevel, output.expectedInputs,
                        output.preconditions, output.allowedWriteScopes, output.forbiddenOperations,
                        output.executionOutline, output.acceptanceCriteria, output.requiredEvidence,
                        output.blockedHandling, output.rollbackStrategy))
                {
                    error = "AICommand Generation Output 的授权、执行、失败或验收语义不完整。";
                    return false;
                }
                string artifactName = output.artifactName.Trim();
                if (artifactName.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                    artifactName = artifactName.Substring(0, artifactName.Length - 3);
                string expectedPath = "Assets/Plugins/ES/AICommands/" + artifactName + ".md";
                if (!string.Equals(output.targetProjectPath.Replace('\\', '/'), expectedPath,
                        StringComparison.Ordinal))
                {
                    error = "AICommand Generation Output 的名称与目标路径不一致，期望：" + expectedPath;
                    return false;
                }
            }
            else
            {
                if (!Enum.IsDefined(typeof(ESAgentSkillEffectKind), output.skillEffectKind)
                    || !Enum.IsDefined(typeof(ESAgentSkillIdempotency), output.skillIdempotency))
                {
                    error = "Agent Skill Generation Output 的效果或幂等语义非法。";
                    return false;
                }
                if (output.skillEffectKind == ESAgentSkillEffectKind.ControlledMutation
                    && output.skillIdempotency == ESAgentSkillIdempotency.NotApplicable)
                {
                    error = "Agent Skill Generation Output 的副作用与幂等策略冲突。";
                    return false;
                }
                if (!output.includeAgentsMetadata
                    || Missing(output.artifactName, output.targetProjectPath, output.skillDescription,
                        output.skillTriggerScenarios, output.skillNonTriggerScenarios,
                        output.skillPreconditions, output.skillRequiredDependencies,
                        output.skillInputContract, output.skillWorkflow, output.skillOutputContract,
                        output.skillSideEffects, output.skillNonGoals, output.skillFailureRecovery,
                        output.skillValidationSteps, output.skillPermissionBoundary, output.defaultPrompt))
                {
                    error = "Agent Skill Generation Output 的触发、输入输出、副作用、恢复或权限语义不完整。";
                    return false;
                }
                string expectedPath = ".agents/skills/" + output.artifactName.Trim() + "/";
                if (!string.Equals(output.targetProjectPath.Replace('\\', '/'), expectedPath,
                        StringComparison.Ordinal))
                {
                    error = "Agent Skill Generation Output 的名称与目标路径不一致，期望：" + expectedPath;
                    return false;
                }
            }
            error = string.Empty;
            return true;
        }

        private static bool Missing(params string[] values)
        {
            for (int i = 0; i < values.Length; i++)
                if (string.IsNullOrWhiteSpace(values[i]))
                    return true;
            return false;
        }
    }

    /// <summary>
    /// Deterministic semantic gate for generated artifacts.  Structural validity is not
    /// sufficient: at least one domain-specific intent term from the Goal must survive
    /// into every declared Output.  This deliberately rejects the stock template text.
    /// </summary>
    public static class ESAgentGenerationSemanticValidator
    {
        private static readonly string[] PlaceholderFragments =
        {
            "描述希望 AICommand 或 Agent Skill 解决的问题。",
            "生成结果可读、可验证、权限边界明确，并能通过人工 Diff Review。",
            "生成_新任务_AI命令",
            "描述该 AICommand 要授权和约束的单次任务。",
            "es-generated-workflow",
            "描述该 Skill 的能力、触发场景和适用任务。"
        };

        private static readonly HashSet<string> StopTerms = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "生成", "任务", "模块", "工作流", "实现", "执行", "验证", "结果", "目标", "用户", "内容",
            "规则", "项目", "文件", "修改", "命令", "技能", "智能", "助手", "候选", "产物", "流程",
            "要求", "范围", "检查", "审查", "工作", "AI", "Agent", "Command", "Skill", "Graph",
            "artifact", "workflow", "module", "task", "command", "skill", "agent", "graph", "ai"
        };

        public static bool TryValidate(ESAgentGenerationGoal goal,
            IEnumerable<ESAgentGenerationOutput> outputs, out string error)
        {
            error = string.Empty;
            if (goal == null)
            {
                error = "语义一致性校验失败：Goal 为空。";
                return false;
            }
            string goalText = string.Join("\n", new[] { goal.title, goal.objective, goal.context,
                goal.targetUsers, goal.successCriteria });
            string placeholder = PlaceholderFragments.FirstOrDefault(item =>
                ContainsText(goalText, item));
            if (!string.IsNullOrEmpty(placeholder))
            {
                error = "语义一致性校验失败：Goal 仍使用模板/占位内容（" + placeholder
                    + "），请先填写真实业务目标。";
                return false;
            }

            HashSet<string> goalTerms = ExtractIntentTerms(goalText);
            if (goalTerms.Count == 0)
            {
                error = "语义一致性校验失败：Goal 没有可用于关联 Output 的业务意图词。";
                return false;
            }
            ESAgentGenerationOutput[] outputArray = (outputs ?? Array.Empty<ESAgentGenerationOutput>()).ToArray();
            if (outputArray.Length == 0)
            {
                error = "语义一致性校验失败：没有可关联的 Output。";
                return false;
            }
            foreach (ESAgentGenerationOutput output in outputArray)
            {
                if (output == null)
                {
                    error = "语义一致性校验失败：Output 为空。";
                    return false;
                }
                string outputText = string.Join("\n", new[] { output.artifactName, output.requirements,
                    output.acceptanceCriteria, output.executionOutline, output.skillDescription,
                    output.skillTriggerScenarios, output.skillWorkflow, output.skillOutputContract });
                string outputPlaceholder = PlaceholderFragments.FirstOrDefault(item =>
                    ContainsText(outputText, item));
                if (!string.IsNullOrEmpty(outputPlaceholder))
                {
                    error = "语义一致性校验失败：Output “" + output.artifactName
                        + "” 仍使用模板/占位内容（" + outputPlaceholder + "）。";
                    return false;
                }
                HashSet<string> outputTerms = ExtractIntentTerms(outputText);
                HashSet<string> titleTerms = ExtractIntentTerms(goal.title);
                if (titleTerms.Count > 0 && !titleTerms.Any(outputTerms.Contains))
                {
                    error = "语义一致性校验失败：Output “" + (output.artifactName ?? "")
                        + "” 没有体现 Goal 标题“" + (goal.title ?? "")
                        + "”的业务主题。请同步修改产物名称、用途或验收标准。";
                    return false;
                }
                string matched = goalTerms.FirstOrDefault(outputTerms.Contains);
                if (string.IsNullOrEmpty(matched))
                {
                    string examples = string.Join("、", goalTerms.Take(5));
                    error = "语义一致性校验失败：Output “" + (output.artifactName ?? "")
                        + "” 与 Goal 没有共享业务意图词。请在命令名称、用途或验收标准中明确引用 Goal 主题（例如："
                        + examples + "）。";
                    return false;
                }
            }
            return true;
        }

        public static bool TryValidate(ESAgentArtifactGenerationSpec spec, out string error)
        {
            return TryValidate(spec?.goal, spec?.outputs, out error);
        }

        private static bool ContainsText(string text, string fragment)
        {
            return !string.IsNullOrEmpty(text) && text.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static HashSet<string> ExtractIntentTerms(string text)
        {
            var terms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(text)) return terms;
            string normalized = text.Replace("_", " ").Replace("-", " ");
            var latin = System.Text.RegularExpressions.Regex.Matches(normalized, "[A-Za-z][A-Za-z0-9]{2,}");
            foreach (System.Text.RegularExpressions.Match match in latin)
                if (!StopTerms.Contains(match.Value)) terms.Add(match.Value);
            var chinese = System.Text.RegularExpressions.Regex.Matches(normalized, "[\\u4e00-\\u9fff]{2,}");
            foreach (System.Text.RegularExpressions.Match match in chinese)
            {
                string value = match.Value;
                for (int length = 2; length <= Math.Min(6, value.Length); length++)
                    for (int start = 0; start + length <= value.Length; start++)
                    {
                        string term = value.Substring(start, length);
                        if (!StopTerms.Contains(term)) terms.Add(term);
                    }
            }
            return terms;
        }
    }

    [Serializable]
    public sealed class ESAgentGenerationValidation
    {
        public string nodeId;
        public bool validateAICommand;
        public bool validateAgentSkill;
        public bool validateUtf8;
        public bool requireDiffReview;
        public bool requireHumanApproval;
        public string additionalRequirements;
        public string reviewChecklist;
    }

    [Serializable]
    public sealed class ESAgentGenerationRelation
    {
        public string edgeId;
        public int order;
        public string fromNodeId;
        public string fromNodeTypeId;
        public string fromNodeTitle;
        public string fromPortStableKey;
        public string fromPortMeaning;
        public string toNodeId;
        public string toNodeTypeId;
        public string toNodeTitle;
        public string toPortStableKey;
        public string toPortMeaning;
        public ESAgentRelationKind relationKind;
        public string semanticType;
        public string sourceValueTypeId;
        public string targetValueTypeId;
        public ESGraphPortAggregation sourceAggregation;
        public ESGraphPortAggregation targetAggregation;
    }

    /// <summary>
    /// Graph 烘焙后的 Skill 能力包合同。AICommand 与 AISkill 仍然保留各自的产物字段，
    /// 但通过这里共享一个稳定身份、Goal、约束、引用和 Validation 边界。
    /// </summary>
    [Serializable]
    public sealed class ESAgentSkillBundleContract
    {
        public const int CurrentSchemaVersion = 2;

        public int schemaVersion = CurrentSchemaVersion;
        public string bundleId;
        public string displayName;
        public ESAgentSkillBundleKind kind;
        public string goalNodeId;
        public string[] referenceNodeIds = Array.Empty<string>();
        public string[] constraintNodeIds = Array.Empty<string>();
        public string[] branchNodeIds = Array.Empty<string>();
        public string[] traversalNodeIds = Array.Empty<string>();
        public string[] commandOutputNodeIds = Array.Empty<string>();
        public string[] aiSkillOutputNodeIds = Array.Empty<string>();
        public string[] validationNodeIds = Array.Empty<string>();

        public bool IsPaired => kind == ESAgentSkillBundleKind.CommandAndAISkill;

        public static ESAgentSkillBundleContract Create(string graphId, string displayName,
            string goalNodeId, IEnumerable<ESAgentGenerationReference> references,
            IEnumerable<ESAgentGenerationConstraint> constraints,
            IEnumerable<ESAgentGenerationBranch> branches,
            IEnumerable<ESAgentGenerationTraversal> traversals,
            IEnumerable<ESAgentGenerationOutput> outputs,
            IEnumerable<ESAgentGenerationValidation> validations)
        {
            string[] commandIds = (outputs ?? Array.Empty<ESAgentGenerationOutput>())
                .Where(item => item != null && item.artifactKind == ESAgentArtifactKind.AICommand)
                .Select(item => item.nodeId).Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToArray();
            string[] skillIds = (outputs ?? Array.Empty<ESAgentGenerationOutput>())
                .Where(item => item != null && item.artifactKind == ESAgentArtifactKind.AgentSkill)
                .Select(item => item.nodeId).Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToArray();
            var result = new ESAgentSkillBundleContract
            {
                bundleId = ESAgentArtifactIdentity.CreateBundle(graphId ?? string.Empty),
                displayName = string.IsNullOrWhiteSpace(displayName) ? "ES Skill 能力包" : displayName.Trim(),
                kind = commandIds.Length > 0 && skillIds.Length > 0
                    ? ESAgentSkillBundleKind.CommandAndAISkill
                    : commandIds.Length > 0 ? ESAgentSkillBundleKind.CommandOnly : ESAgentSkillBundleKind.AISkillOnly,
                goalNodeId = goalNodeId ?? string.Empty,
                referenceNodeIds = NodeIds(references?.Select(item => item?.nodeId)),
                constraintNodeIds = NodeIds(constraints?.Select(item => item?.nodeId)),
                branchNodeIds = NodeIds(branches?.Select(item => item?.nodeId)),
                traversalNodeIds = NodeIds(traversals?.Select(item => item?.nodeId)),
                commandOutputNodeIds = commandIds,
                aiSkillOutputNodeIds = skillIds,
                validationNodeIds = NodeIds(validations?.Select(item => item?.nodeId))
            };
            return result;
        }

        private static string[] NodeIds(IEnumerable<string> values)
        {
            return (values ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        }
    }

    /// <summary>Graph V2 烘焙出的 Agent Artifact 生成规格；只用于编辑器生成与审查，不进入运行时。</summary>
}
