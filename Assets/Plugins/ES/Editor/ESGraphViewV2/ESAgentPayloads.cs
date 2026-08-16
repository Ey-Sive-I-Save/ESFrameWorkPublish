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
    public sealed class ESAgentGoalPayload
    {
        public int schemaVersion = 1;
        [ESField(ESFieldLevel.Core, Required = true, Hint = "一句话说明这张图最终要完成什么。")]
        public string title = "生成新的 Agent Artifact";
        [ESField(ESFieldLevel.Core, Required = true, Hint = "明确真实目标，所有产物内容都必须与它一致。")]
        [TextArea] public string objective = "描述希望 AICommand 或 Agent Skill 解决的问题。";
        [ESField(ESFieldLevel.Important, Hint = "补充项目现场、已知事实和限制条件。")]
        [TextArea] public string context = "";
        [ESField(ESFieldLevel.Important, Hint = "说明谁会使用以及在什么场景触发。")]
        [TextArea] public string targetUsers = "该 AICommand / Agent Skill 的使用者与触发场景。";
        [ESField(ESFieldLevel.Core, Required = true, Hint = "用于判断最终结果是否真正完成。")]
        [TextArea] public string successCriteria = "生成结果可读、可验证、权限边界明确，并能通过人工 Diff Review。";
    }

    [Serializable]
    public sealed class ESAgentReferencePayload
    {
        public int schemaVersion = 1;
        public ESAgentReferenceKind referenceKind = ESAgentReferenceKind.AICommand;
        public string projectPath = "Assets/Plugins/ES/AICommands/生成_AgentArtifact候选_AI命令.md";
        public string purpose = "Agent Artifact 候选生成的权限与输出合同";
        public bool required = true;
    }

    [Serializable]
    public sealed class ESAgentConstraintPayload
    {
        public const int CurrentSchemaVersion = 2;

        public int schemaVersion = CurrentSchemaVersion;
        public ESAgentConstraintKind kind = ESAgentConstraintKind.Required;
        public ESAgentConstraintScope scope = ESAgentConstraintScope.WholeArtifact;
        public ESAgentConstraintCombinationMode combinationMode = ESAgentConstraintCombinationMode.AllOf;
        [Range(0, 100)] public int priority = 50;
        public string combinationGroup = "";
        [TextArea] public string statement = "只生成候选文件，不直接写入正式目录。";
        [TextArea] public string rationale = "说明为什么需要该规则。";
        [TextArea] public string verification = "说明如何验证该规则已经满足。";
    }

    [Serializable]
    public sealed class ESAgentBranchPayload
    {
        public int schemaVersion = 1;
        [ESField(ESFieldLevel.Core, Required = true, Hint = "用可核对的自然语言说明何时进入命中分支。")]
        [TextArea] public string condition = "当目标包含多个可独立检查的范围时进入命中分支。";
        [ESField(ESFieldLevel.Important, Required = true, Hint = "条件成立时必须执行的路径语义。")]
        [TextArea] public string matchedPath = "按声明范围逐项处理，并保留每项证据。";
        [ESField(ESFieldLevel.Core, Required = true, Hint = "条件不成立时的确定性兜底路径，不允许省略。")]
        [TextArea] public string defaultPath = "按单一目标执行，并继续保留验证门禁。";
        [ESField(ESFieldLevel.Core, Required = true, Hint = "条件无法判断或读取失败时的停止与报告路径。")]
        [TextArea] public string failurePath = "停止产生副作用，报告缺失上下文和恢复动作。";
    }

    [Serializable]
    public sealed class ESAgentTraversePayload
    {
        public int schemaVersion = 1;
        [ESField(ESFieldLevel.Core, Required = true, Hint = "说明要遍历的输入集合，例如声明的文件、规则或候选项。")]
        [TextArea] public string target = "Graph 声明且与当前 Output 相连的 References 与 Constraints。";
        [ESField(ESFieldLevel.Important, Required = true, Hint = "遍历中单个元素的可读名称。")]
        public string itemAlias = "当前项";
        public ESAgentTraversalOrder order = ESAgentTraversalOrder.SourceOrder;
        [Range(1, 32)] public int maxDepth = 8;
        [Range(1, 512)] public int maxItems = 128;
        [ESField(ESFieldLevel.Core, Required = true, Hint = "满足时立即结束遍历，防止无意义继续处理。")]
        [TextArea] public string stopCondition = "达到数量或深度上限，或继续处理将越过授权边界。";
        [ESField(ESFieldLevel.Important, Required = true, Hint = "输入集合为空时的明确行为。")]
        [TextArea] public string emptyResultAction = "记录空结果并进入完成出口，不猜测或补造输入。";
        [ESField(ESFieldLevel.Core, Required = true, Hint = "单项失败或遍历异常时的恢复路径。")]
        [TextArea] public string failureAction = "停止后续副作用，保留已完成证据并进入失败出口。";
    }

    [Serializable]
    [ESAgentArtifact(ESAgentGraphStableIds.AICommandArtifact)]
    public sealed class ESAgentAICommandOutputPayload
    {
        public const int CurrentSchemaVersion = 2;

        public int schemaVersion = CurrentSchemaVersion;
        [ESField(ESFieldLevel.Core, Required = true, Hint = "AICommand 的稳定名称，同时决定建议文件名。")]
        public string commandName = "生成_新任务_AI命令";
        [ESField(ESFieldLevel.Core, Required = true, Hint = "正式产物唯一目标位置，必须与命令名称一致。")]
        public string targetProjectPath = "Assets/Plugins/ES/AICommands/新_AI命令_AI命令.md";
        [ESField(ESFieldLevel.Important, Hint = "决定创建、更新或自动选择的处理方式。")]
        public ESAgentArtifactOperationMode operationMode = ESAgentArtifactOperationMode.CreateOrUpdate;
        [ESField(ESFieldLevel.Important, Required = true, Hint = "说明该命令是检查、评审还是受控执行。")]
        public ESAgentCommandIntent commandIntent = ESAgentCommandIntent.ControlledExecution;
        [ESField(ESFieldLevel.Core, Required = true, Hint = "决定生成内容能够进行何种写入。")]
        public ESAgentWriteAuthorization writeAuthorization = ESAgentWriteAuthorization.ConfirmBeforeWrite;
        [ESField(ESFieldLevel.Important, Required = true, Hint = "用于选择匹配风险的验证和确认强度。")]
        public ESAgentRiskLevel riskLevel = ESAgentRiskLevel.L2;
        [ESField(ESFieldLevel.Important, Required = true, Hint = "失败时必须停止还是回滚本次事务。")]
        public ESAgentFailurePolicy failurePolicy = ESAgentFailurePolicy.RollbackAndReport;
        [ESField(ESFieldLevel.Core, Required = true, Hint = "说明该命令真正解决什么问题。")]
        [TextArea] public string purpose = "描述该 AICommand 要授权和约束的单次任务。";
        [ESField(ESFieldLevel.Important, Required = true, Hint = "列出命令成立所需的用户输入和上下文。")]
        [TextArea] public string expectedInputs = "用户目标、范围、权威规则和相关项目路径。";
        [ESField(ESFieldLevel.Important, Required = true, Hint = "条件不满足时必须停止，不能猜测执行。")]
        [TextArea] public string preconditions = "目标、范围和权威规则已明确；缺失时停止并请求补充。";
        [ESField(ESFieldLevel.Core, Required = true, Hint = "这是命令最重要的权限边界。")]
        [TextArea] public string allowedWriteScopes = "只允许修改用户明确授权且由本节点列出的项目路径。";
        [ESField(ESFieldLevel.Core, Required = true, Hint = "明确禁止越过的操作和外部副作用。")]
        [TextArea] public string forbiddenOperations = "不得扩大用户授权；不得执行未明确授权的 Git、删除、发布、上传或外部写入。";
        [ESField(ESFieldLevel.Important, Required = true, Hint = "给出可复核的执行顺序。")]
        [TextArea] public string executionOutline = "读取规则\n核对现状\n执行受控修改\n验证\n交付";
        [ESField(ESFieldLevel.Core, Required = true, Hint = "定义什么结果才算真正完成。")]
        [TextArea] public string acceptanceCriteria = "输出必须包含已读规则、改动、验证和剩余风险。";
        [ESField(ESFieldLevel.Core, Required = true, Hint = "要求报告可以被复核的真实证据。")]
        [TextArea] public string requiredEvidence = "按实际执行层级报告源码检查、编译、测试和运行证据；未执行项必须明确标记。";
        [ESField(ESFieldLevel.Important, Required = true, Hint = "遇到越界或缺少授权时如何停止并交接。")]
        [TextArea] public string blockedHandling = "停止越界操作，说明阻断事实、已完成工作和所需用户决策。";
        [ESField(ESFieldLevel.Important, Required = true, Hint = "写入失败后如何恢复本次可撤销修改。")]
        [TextArea] public string rollbackStrategy = "本次写入失败时回滚本次事务；无法安全回滚时立即停止并报告。";
        [ESField(ESFieldLevel.Important, Required = true, Hint = "正式 AICommand 必须具备的章节。")]
        [TextArea] public string requiredSections = "必须先读\n执行要求\n交付格式\n需求";

        public string SuggestedTargetProjectPath
        {
            get
            {
                string name = (commandName ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(name))
                    return string.Empty;
                if (name.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                    name = name.Substring(0, name.Length - 3);
                return "Assets/Plugins/ES/AICommands/" + name + ".md";
            }
        }

        public bool SynchronizeTargetProjectPath()
        {
            string suggested = SuggestedTargetProjectPath;
            if (!ESAgentArtifactPathPolicy.IsAllowedTarget(ESAgentArtifactKind.AICommand, suggested, out _)
                || string.Equals(targetProjectPath, suggested, StringComparison.Ordinal))
                return false;
            targetProjectPath = suggested;
            return true;
        }
    }

    [Serializable]
    [ESAgentArtifact(ESAgentGraphStableIds.AISkillArtifact)]
    public sealed class ESAgentSkillOutputPayload
    {
        public const int CurrentSchemaVersion = 2;

        public int schemaVersion = CurrentSchemaVersion;
        [ESField(ESFieldLevel.Core, Required = true, Hint = "Skill 的稳定英文名称和调用标记。")]
        public string skillName = "es-generated-workflow";
        [ESField(ESFieldLevel.Core, Required = true, Hint = "正式 Skill 唯一目录，必须与名称一致。")]
        public string targetProjectPath = ".agents/skills/es-generated-workflow/";
        [ESField(ESFieldLevel.Important, Hint = "决定创建、更新或自动选择的处理方式。")]
        public ESAgentArtifactOperationMode operationMode = ESAgentArtifactOperationMode.CreateOrUpdate;
        [ESField(ESFieldLevel.Core, Required = true, Hint = "说明 Skill 是否会读取或修改项目状态。")]
        public ESAgentSkillEffectKind effectKind = ESAgentSkillEffectKind.ControlledMutation;
        [ESField(ESFieldLevel.Important, Required = true, Hint = "说明重复执行时如何避免产生重复副作用。")]
        public ESAgentSkillIdempotency idempotency = ESAgentSkillIdempotency.BestEffort;
        [ESField(ESFieldLevel.Core, Required = true, Hint = "一句话说明这个 Skill 能解决什么问题。")]
        [TextArea] public string description = "描述该 Skill 的能力、触发场景和适用任务。";
        [ESField(ESFieldLevel.Core, Required = true, Hint = "说明什么情况下必须使用该 Skill。")]
        [TextArea] public string triggerScenarios = "说明何时必须使用该 Skill。";
        [ESField(ESFieldLevel.Core, Required = true, Hint = "防止 Skill 被错误触发或扩大适用范围。")]
        [TextArea] public string nonTriggerScenarios = "说明何时不应触发该 Skill。";
        [ESField(ESFieldLevel.Important, Required = true, Hint = "使用前必须具备的输入、规则和工具。")]
        [TextArea] public string preconditions = "所需项目规则、输入和工具可用；否则停止并报告缺口。";
        [ESField(ESFieldLevel.Important, Required = true, Hint = "必须读取的规则、工具和参考资料。")]
        [TextArea] public string requiredDependencies = "列出必须读取的规则、依赖工具和可选参考资料。";
        [ESField(ESFieldLevel.Core, Required = true, Hint = "明确 Skill 接受哪些输入。")]
        [TextArea] public string inputContract = "用户目标、授权范围、目标路径和必要上下文。";
        [ESField(ESFieldLevel.Important, Required = true, Hint = "可重复执行且可验证的核心步骤。")]
        [TextArea] public string workflow = "读取权威规则\n执行受控步骤\n验证\n交付";
        [ESField(ESFieldLevel.Core, Required = true, Hint = "明确 Skill 最终必须交付哪些内容。")]
        [TextArea] public string outputContract = "输出实际执行内容、改动文件、验证证据、未完成项和剩余风险。";
        [ESField(ESFieldLevel.Important, Required = true, Hint = "列出 Skill 允许产生的副作用。")]
        [TextArea] public string sideEffects = "仅产生当前用户或 AICommand 已授权范围内的副作用。";
        [ESField(ESFieldLevel.Core, Required = true, Hint = "明确 Skill 不负责和绝对禁止的事项。")]
        [TextArea] public string nonGoals = "不得扩大用户授权，不得绕过 AICommand、候选目录或人工批准。";
        [ESField(ESFieldLevel.Important, Required = true, Hint = "失败时停止副作用并恢复可撤销修改。")]
        [TextArea] public string failureRecovery = "失败时停止后续副作用，恢复本次可安全撤销的修改并报告阻断。";
        [ESField(ESFieldLevel.Core, Required = true, Hint = "用于证明 Skill 工作流真实有效。")]
        [TextArea] public string validationSteps = "严格 UTF-8\n目标路径白名单\n候选完整性\nDiff Review";
        [ESField(ESFieldLevel.Core, Required = true, Hint = "Skill 不能扩大用户或 AICommand 的授权。")]
        [TextArea] public string permissionBoundary = "Skill 只提供可复用工作流，不扩大用户或 AICommand 的权限。";
        [ESField(ESFieldLevel.Important, Required = true, Hint = "生成 agents/openai.yaml 的正式入口配置。")]
        public bool includeAgentsMetadata = true;
        [ESField(ESFieldLevel.Normal, Hint = "允许候选附带 references 参考资料目录。")]
        public bool includeReferences = true;
        [ESField(ESFieldLevel.Normal, Hint = "允许候选附带 scripts 辅助脚本目录。")]
        public bool includeScripts;
        [ESField(ESFieldLevel.Important, Required = true, Hint = "用户或 Agent 调用该 Skill 时的默认提示。")]
        public string defaultPrompt = "Use $es-generated-workflow to complete the requested ESFramework workflow.";

        public string SuggestedTargetProjectPath
        {
            get
            {
                string name = (skillName ?? string.Empty).Trim();
                return string.IsNullOrEmpty(name) ? string.Empty : ".agents/skills/" + name + "/";
            }
        }

        public string InvocationToken
        {
            get
            {
                string name = (skillName ?? string.Empty).Trim();
                return string.IsNullOrEmpty(name) ? string.Empty : "$" + name;
            }
        }

        public string IncludedContentSummary
        {
            get
            {
                var parts = new List<string> { "SKILL.md" };
                if (includeAgentsMetadata) parts.Add("agents/openai.yaml");
                if (includeReferences) parts.Add("references/");
                if (includeScripts) parts.Add("scripts/");
                return string.Join(" · ", parts);
            }
        }

        public bool SynchronizeTargetProjectPath()
        {
            string suggested = SuggestedTargetProjectPath;
            if (!ESAgentArtifactPathPolicy.IsAllowedTarget(ESAgentArtifactKind.AgentSkill, suggested, out _)
                || string.Equals(targetProjectPath, suggested, StringComparison.Ordinal))
                return false;
            targetProjectPath = suggested;
            return true;
        }
    }

    internal static class ESAgentOutputContractValidator
    {
        public static bool TryValidate(ESAgentAICommandOutputPayload payload, out string error)
        {
            if (payload == null || payload.schemaVersion != ESAgentAICommandOutputPayload.CurrentSchemaVersion)
            {
                error = "AICommand 语义契约必须迁移到 Schema v"
                    + ESAgentAICommandOutputPayload.CurrentSchemaVersion + "。";
                return false;
            }
            if (!Enum.IsDefined(typeof(ESAgentArtifactOperationMode), payload.operationMode)
                || !Enum.IsDefined(typeof(ESAgentCommandIntent), payload.commandIntent)
                || !Enum.IsDefined(typeof(ESAgentWriteAuthorization), payload.writeAuthorization)
                || !Enum.IsDefined(typeof(ESAgentRiskLevel), payload.riskLevel)
                || !Enum.IsDefined(typeof(ESAgentFailurePolicy), payload.failurePolicy))
            {
                error = "AICommand 包含非法的操作、意图、授权、风险或失败策略枚举。";
                return false;
            }
            if (payload.commandIntent != ESAgentCommandIntent.ControlledExecution
                && payload.writeAuthorization == ESAgentWriteAuthorization.ScopedWrites)
            {
                error = "只有受控执行类 AICommand 可以预授权范围内写入。";
                return false;
            }
            if (payload.writeAuthorization == ESAgentWriteAuthorization.ScopedWrites
                && payload.failurePolicy != ESAgentFailurePolicy.RollbackAndReport)
            {
                error = "允许范围内写入的 AICommand 必须声明失败回滚策略。";
                return false;
            }
            if (Missing(payload.commandName, payload.targetProjectPath, payload.purpose, payload.expectedInputs,
                    payload.preconditions, payload.allowedWriteScopes, payload.forbiddenOperations,
                    payload.executionOutline, payload.acceptanceCriteria, payload.requiredEvidence,
                    payload.blockedHandling, payload.rollbackStrategy, payload.requiredSections))
            {
                error = "AICommand 必须完整声明名称、路径、输入、前置条件、授权边界、执行、验收、证据、阻断和恢复语义。";
                return false;
            }
            string commandName = payload.commandName.Trim();
            if (commandName.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                commandName = commandName.Substring(0, commandName.Length - 3);
            string expectedPath = "Assets/Plugins/ES/AICommands/" + commandName + ".md";
            if (!string.Equals(payload.targetProjectPath.Replace('\\', '/'), expectedPath,
                    StringComparison.Ordinal))
            {
                error = "AICommand 名称与目标路径不一致，期望：" + expectedPath;
                return false;
            }
            error = string.Empty;
            return true;
        }

        public static bool TryValidate(ESAgentSkillOutputPayload payload, out string error)
        {
            if (payload == null || payload.schemaVersion != ESAgentSkillOutputPayload.CurrentSchemaVersion)
            {
                error = "Agent Skill 语义契约必须迁移到 Schema v"
                    + ESAgentSkillOutputPayload.CurrentSchemaVersion + "。";
                return false;
            }
            if (!Enum.IsDefined(typeof(ESAgentArtifactOperationMode), payload.operationMode)
                || !Enum.IsDefined(typeof(ESAgentSkillEffectKind), payload.effectKind)
                || !Enum.IsDefined(typeof(ESAgentSkillIdempotency), payload.idempotency))
            {
                error = "Agent Skill 包含非法的操作、效果或幂等策略枚举。";
                return false;
            }
            if (payload.effectKind == ESAgentSkillEffectKind.ControlledMutation
                && payload.idempotency == ESAgentSkillIdempotency.NotApplicable)
            {
                error = "可能修改状态的 Agent Skill 必须声明必须幂等或尽力幂等。";
                return false;
            }
            if (!payload.includeAgentsMetadata)
            {
                error = "Agent Skill 必须生成 agents/openai.yaml 入口配置。";
                return false;
            }
            if (Missing(payload.skillName, payload.targetProjectPath, payload.description,
                    payload.triggerScenarios, payload.nonTriggerScenarios, payload.preconditions,
                    payload.requiredDependencies, payload.inputContract, payload.workflow,
                    payload.outputContract, payload.sideEffects, payload.nonGoals, payload.failureRecovery,
                    payload.validationSteps, payload.permissionBoundary, payload.defaultPrompt))
            {
                error = "Agent Skill 必须完整声明触发边界、依赖、输入输出、工作流、副作用、失败恢复、验证和权限边界。";
                return false;
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

    internal static class ESAgentConstraintContractValidator
    {
        public static bool TryValidate(ESAgentConstraintPayload payload, out string error)
        {
            if (payload == null || payload.schemaVersion != ESAgentConstraintPayload.CurrentSchemaVersion)
            {
                error = "Constraint 语义契约必须迁移到 Schema v"
                    + ESAgentConstraintPayload.CurrentSchemaVersion + "。";
                return false;
            }
            if (!Enum.IsDefined(typeof(ESAgentConstraintKind), payload.kind)
                || !Enum.IsDefined(typeof(ESAgentConstraintScope), payload.scope)
                || !Enum.IsDefined(typeof(ESAgentConstraintCombinationMode), payload.combinationMode))
            {
                error = "Constraint 包含非法的规则类型、作用域或合并模式。";
                return false;
            }
            if (payload.priority < 0 || payload.priority > 100)
            {
                error = "Constraint 优先级必须位于 0 到 100。";
                return false;
            }
            if (string.IsNullOrWhiteSpace(payload.statement)
                || string.IsNullOrWhiteSpace(payload.rationale)
                || string.IsNullOrWhiteSpace(payload.verification))
            {
                error = "Constraint 必须完整声明规则、原因和验证方法。";
                return false;
            }
            string group = (payload.combinationGroup ?? string.Empty).Trim();
            if (payload.combinationMode == ESAgentConstraintCombinationMode.AnyOf)
            {
                if (!System.Text.RegularExpressions.Regex.IsMatch(group, "^[a-z0-9][a-z0-9._-]{0,63}$"))
                {
                    error = "AnyOf Constraint 必须提供小写稳定组合组标识。";
                    return false;
                }
            }
            else if (!string.IsNullOrEmpty(group))
            {
                error = "AllOf Constraint 不得保留 AnyOf 组合组标识。";
                return false;
            }
            error = string.Empty;
            return true;
        }
    }

    [Serializable]
    public sealed class ESAgentValidationPayload
    {
        public int schemaVersion = 1;
        [ESField(ESFieldLevel.Important, Hint = "校验 AICommand 候选结构和内容。")]
        public bool validateAICommand = true;
        [ESField(ESFieldLevel.Important, Hint = "校验 Agent Skill 候选结构和内容。")]
        public bool validateAgentSkill = true;
        [ESField(ESFieldLevel.Core, Required = true, Hint = "所有文本产物必须通过严格 UTF-8 检查。")]
        public bool validateUtf8 = true;
        [ESField(ESFieldLevel.Core, Required = true, Hint = "正式写入前必须查看候选差异。")]
        public bool requireDiffReview = true;
        [ESField(ESFieldLevel.Core, Required = true, Hint = "候选必须经过人工批准才能进入正式位置。")]
        public bool requireHumanApproval = true;
        [ESField(ESFieldLevel.Important, Hint = "补充本图特有的验证要求。")]
        [TextArea] public string additionalRequirements = "不得包含 U+FFFD；不得越过候选目录。";
        [ESField(ESFieldLevel.Core, Required = true, Hint = "人工审查时逐项确认的检查清单。")]
        [TextArea] public string reviewChecklist = "目标路径正确\n内容符合 Graph\n没有越权修改\n验证证据真实";
    }

}
