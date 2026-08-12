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
    public interface IESGraphAuthoringPlanBaker
    {
        bool TryBakePlan(ESBakedGraphSnapshot source, out IESBakedGraphPlan plan,
            out IReadOnlyList<ESGraphValidationIssue> issues);
    }

    public enum ESAgentConstraintKind : byte
    {
        Required = 0,
        Forbidden = 1,
        Permission = 2,
        Quality = 3
    }

    public enum ESAgentConstraintScope : byte
    {
        WholeArtifact = 0,
        Authorization = 1,
        Inputs = 2,
        Execution = 3,
        Validation = 4,
        Recovery = 5
    }

    public enum ESAgentConstraintCombinationMode : byte
    {
        AllOf = 0,
        AnyOf = 1
    }

    public enum ESAgentTraversalOrder : byte
    {
        SourceOrder = 0,
        DependencyFirst = 1,
        PriorityFirst = 2
    }

    public enum ESAgentArtifactKind : byte
    {
        AICommand = 0,
        AgentSkill = 1
    }

    /// <summary>
    /// Skill 能力包的组成方式。这里的 Skill 指 AICommand + AISkill 的统一能力，
    /// 而不是只指某一个 SKILL.md 文件。
    /// </summary>
    public enum ESAgentSkillBundleKind : byte
    {
        CommandOnly = 0,
        AISkillOnly = 1,
        CommandAndAISkill = 2
    }

    internal static class ESAgentNodeCardActionKeys
    {
        public static readonly ESGraphNodeCardActionKey UseOnce =
            ESGraphNodeCardActionKey.FromStableId("es.agent-authoring.output.use-once");
        public static readonly ESGraphNodeCardActionKey SaveCandidate =
            ESGraphNodeCardActionKey.FromStableId("es.agent-authoring.output.save-candidate");
    }

    internal sealed class ESAgentOutputNodeCardActionHandler : IESGraphNodeCardActionHandler
    {
        private static readonly ESGraphNodeTypeKey[] SupportedNodeTypes =
        {
            ESAgentGraphStableIds.Node(ESAgentGraphStableIds.AICommandOutputNode),
            ESAgentGraphStableIds.Node(ESAgentGraphStableIds.AISkillOutputNode)
        };

        private static readonly ESGraphNodeCardActionKey[] SupportedActions =
        {
            ESAgentNodeCardActionKeys.UseOnce,
            ESAgentNodeCardActionKeys.SaveCandidate
        };

        public ESGraphDomainKey Domain => ESAgentGraphStableIds.Domain;
        public IReadOnlyList<ESGraphNodeTypeKey> NodeTypes => SupportedNodeTypes;
        public IReadOnlyList<ESGraphNodeCardActionKey> Actions => SupportedActions;
        public int Priority => 0;

        public ESAgentOutputNodeCardActionHandler()
        {
        }

        public bool CanExecute(ESGraphNodeCardActionContext context, ESGraphNodeCardActionKey action,
            out string unavailableReason)
        {
            if (context == null)
            {
                unavailableReason = "节点局部动作上下文无效。";
                return false;
            }
            if (context.GraphSchemaVersion != GraphAsset.CurrentSchemaVersion || context.HasFutureSchema)
            {
                unavailableReason = "当前图或节点版本不能安全执行 Agent 局部动作。";
                return false;
            }
            unavailableReason = string.Empty;
            return true;
        }

        public void Execute(ESGraphNodeCardActionContext context, ESGraphNodeCardActionKey action)
        {
            string actionName = action == ESAgentNodeCardActionKeys.SaveCandidate
                ? "生成该节点候选" : "执行该节点内容";
            if (!context.TryBakeForUserAction(actionName, out _, out IESBakedGraphPlan plan)
                || !(plan is ESAgentArtifactGenerationSpec spec))
            {
                context.Report("节点局部操作未执行；请查看质量检查中的具体原因。");
                return;
            }
            if (!ESAgentArtifactGenerationWorkspace.TryCreateArtifactView(spec, context.NodeId,
                    out ESAgentArtifactGenerationSpec artifactView, out string filterError))
            {
                context.Report(filterError);
                return;
            }

            ESAgentGenerationOutput output = artifactView.outputs[0];
            string displayName = output.artifactKind == ESAgentArtifactKind.AICommand
                ? "AICommand" : "AISkill";
            if (action == ESAgentNodeCardActionKeys.SaveCandidate)
            {
                if (!ESAgentArtifactGenerationWorkspace.CreateAndSend(artifactView, out string requestDirectory,
                        out string dispatchMessage, out string error))
                {
                    context.Report(error);
                    return;
                }
                context.Report("节点 " + displayName + "候选请求已创建；受控生成会话：" + dispatchMessage
                    + "；候选目录：" + requestDirectory);
                return;
            }

            if (!ESAgentArtifactGenerationWorkspace.SendSingleUse(artifactView, output.artifactKind,
                    out string requestId, out string singleUseDispatchMessage, out string singleUseError))
            {
                context.Report(singleUseError);
                return;
            }
            string useName = output.artifactKind == ESAgentArtifactKind.AICommand
                ? "单次 AICommand" : "临时 AISkill";
            context.Report(useName + "节点请求 " + requestId + " 的受控会话启动流程已创建；状态："
                + singleUseDispatchMessage + "。只有出现 Codex 接收事件后才代表已接收，当前不代表开始执行或完成。");
        }
    }

    public enum ESAgentArtifactOperationMode : byte
    {
        CreateOrUpdate = 0,
        CreateOnly = 1,
        UpdateOnly = 2
    }

    public enum ESAgentArtifactResolvedOperation : byte
    {
        Create = 0,
        Update = 1
    }

    public enum ESAgentCommandIntent : byte
    {
        ContextOnly = 0,
        ReadOnlyReview = 1,
        PlanReview = 2,
        ControlledExecution = 3,
        Handoff = 4
    }

    public enum ESAgentWriteAuthorization : byte
    {
        NoWrites = 0,
        ConfirmBeforeWrite = 1,
        ScopedWrites = 2
    }

    public enum ESAgentRiskLevel : byte
    {
        L1 = 1,
        L2 = 2,
        L3 = 3
    }

    public enum ESAgentFailurePolicy : byte
    {
        StopAndReport = 0,
        RollbackAndReport = 1
    }

    public enum ESAgentSkillEffectKind : byte
    {
        GuidanceOnly = 0,
        ReadOnly = 1,
        ControlledMutation = 2
    }

    public enum ESAgentSkillIdempotency : byte
    {
        Required = 0,
        BestEffort = 1,
        NotApplicable = 2
    }

    public static class ESAgentSemanticPresentation
    {
        public static string CommandIntent(ESAgentCommandIntent value)
        {
            switch (value)
            {
                case ESAgentCommandIntent.ContextOnly: return "信息补全";
                case ESAgentCommandIntent.ReadOnlyReview: return "只读体检";
                case ESAgentCommandIntent.PlanReview: return "方案评审";
                case ESAgentCommandIntent.ControlledExecution: return "安全执行";
                case ESAgentCommandIntent.Handoff: return "交接沉淀";
                default: return "非法命令意图";
            }
        }

        public static string WriteAuthorization(ESAgentWriteAuthorization value)
        {
            switch (value)
            {
                case ESAgentWriteAuthorization.NoWrites: return "否";
                case ESAgentWriteAuthorization.ConfirmBeforeWrite: return "需用户确认";
                case ESAgentWriteAuthorization.ScopedWrites: return "是，仅限声明范围";
                default: return "非法写入授权";
            }
        }

        public static string RiskLevel(ESAgentRiskLevel value)
        {
            return Enum.IsDefined(typeof(ESAgentRiskLevel), value) ? value.ToString() : "非法风险等级";
        }

        public static string FailurePolicy(ESAgentFailurePolicy value)
        {
            return value == ESAgentFailurePolicy.RollbackAndReport ? "失败时回滚并报告" : "停止并报告";
        }

        public static string SkillEffect(ESAgentSkillEffectKind value)
        {
            switch (value)
            {
                case ESAgentSkillEffectKind.GuidanceOnly: return "仅工作流指导";
                case ESAgentSkillEffectKind.ReadOnly: return "只读操作";
                case ESAgentSkillEffectKind.ControlledMutation: return "受控修改";
                default: return "非法效果类型";
            }
        }

        public static string SkillIdempotency(ESAgentSkillIdempotency value)
        {
            switch (value)
            {
                case ESAgentSkillIdempotency.Required: return "必须幂等";
                case ESAgentSkillIdempotency.BestEffort: return "尽力幂等";
                case ESAgentSkillIdempotency.NotApplicable: return "不适用";
                default: return "非法幂等策略";
            }
        }

        public static int ConstraintKindPrecedence(ESAgentConstraintKind value)
        {
            switch (value)
            {
                case ESAgentConstraintKind.Forbidden: return 400;
                case ESAgentConstraintKind.Required: return 300;
                case ESAgentConstraintKind.Permission: return 200;
                case ESAgentConstraintKind.Quality: return 100;
                default: return 0;
            }
        }

        public static string ConstraintScope(ESAgentConstraintScope value)
        {
            switch (value)
            {
                case ESAgentConstraintScope.WholeArtifact: return "整个产物";
                case ESAgentConstraintScope.Authorization: return "授权边界";
                case ESAgentConstraintScope.Inputs: return "输入与前置条件";
                case ESAgentConstraintScope.Execution: return "执行过程";
                case ESAgentConstraintScope.Validation: return "验证与证据";
                case ESAgentConstraintScope.Recovery: return "失败恢复";
                default: return "非法作用域";
            }
        }

        public static string ConstraintCombination(ESAgentConstraintCombinationMode value)
        {
            return value == ESAgentConstraintCombinationMode.AnyOf ? "同组任一满足" : "必须同时满足";
        }

        public static string RelationKind(ESAgentRelationKind value)
        {
            switch (value)
            {
                case ESAgentRelationKind.ProvidesContext: return "提供上下文";
                case ESAgentRelationKind.AppliesConstraint: return "约束产物";
                case ESAgentRelationKind.RequiresValidation: return "必须验证";
                case ESAgentRelationKind.SelectsBranch: return "选择分支";
                case ESAgentRelationKind.TraversesItems: return "遍历结果";
                default: return "非法关系";
            }
        }
    }

    public enum ESAgentGraphCopyFormat : byte
    {
        ImmediateExecutionPrompt = 0,
        ArtifactRequestJson = 1,
        GraphMarkdown = 2
    }

    public enum ESAgentReferenceKind : byte
    {
        AIWarning = 0,
        AICommand = 1,
        AgentSkill = 2,
        CSharpSource = 3,
        Documentation = 4,
        ProjectAsset = 5
    }

    [Serializable]
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
    internal sealed class ESAgentConstraintPayloadV1
    {
        public int schemaVersion = 1;
        public ESAgentConstraintKind kind;
        public string statement;
        public string rationale;
        public string verification;
    }

    internal static class ESAgentConstraintPayloadMigration
    {
        public static bool TryMigrate(string payloadJson, out ESAgentConstraintPayload payload, out string error)
        {
            payload = null;
            if (!ESAgentAuthoringGraphValidator.TryRead(payloadJson,
                    out ESAgentConstraintPayloadV1 v1, out error))
                return false;
            if (v1.schemaVersion != 1)
            {
                error = "不支持的 Constraint Payload Schema：" + v1.schemaVersion;
                return false;
            }
            payload = new ESAgentConstraintPayload
            {
                kind = v1.kind,
                scope = ESAgentConstraintScope.WholeArtifact,
                combinationMode = ESAgentConstraintCombinationMode.AllOf,
                priority = 50,
                combinationGroup = string.Empty,
                statement = v1.statement,
                rationale = v1.rationale,
                verification = v1.verification
            };
            return ESAgentConstraintContractValidator.TryValidate(payload, out error);
        }
    }

    [Serializable]
    internal sealed class ESAgentAICommandOutputPayloadV1
    {
        public int schemaVersion = 1;
        public string commandName;
        public string targetProjectPath;
        public ESAgentArtifactOperationMode operationMode;
        public string commandType;
        public string defaultWrite;
        public string riskLevel;
        public string purpose;
        public string expectedInputs;
        public string executionOutline;
        public string acceptanceCriteria;
        public string requiredSections;
    }

    [Serializable]
    internal sealed class ESAgentSkillOutputPayloadV1
    {
        public int schemaVersion = 1;
        public string skillName;
        public string targetProjectPath;
        public ESAgentArtifactOperationMode operationMode;
        public string description;
        public string triggerScenarios;
        public string workflow;
        public string nonGoals;
        public string validationSteps;
        public string defaultPrompt;
        public bool includeAgentsMetadata;
        public bool includeReferences;
        public bool includeScripts;
    }

    internal static class ESAgentOutputPayloadMigration
    {
        [Serializable]
        private sealed class SchemaHeader
        {
            public int schemaVersion = 0;
        }

        public static bool TryMigrateAICommand(string payloadJson,
            out ESAgentAICommandOutputPayload payload, out string error)
        {
            payload = null;
            if (!TryReadSchema(payloadJson, out int schemaVersion, out error))
                return false;
            if (schemaVersion == ESAgentAICommandOutputPayload.CurrentSchemaVersion)
            {
                payload = JsonUtility.FromJson<ESAgentAICommandOutputPayload>(payloadJson);
                return ESAgentOutputContractValidator.TryValidate(payload, out error);
            }
            if (schemaVersion != 1)
            {
                error = "不支持的 AICommand Payload Schema：" + schemaVersion;
                return false;
            }

            ESAgentAICommandOutputPayloadV1 v1 = JsonUtility.FromJson<ESAgentAICommandOutputPayloadV1>(payloadJson);
            payload = new ESAgentAICommandOutputPayload
            {
                commandName = v1.commandName,
                targetProjectPath = v1.targetProjectPath,
                operationMode = v1.operationMode,
                commandIntent = ParseCommandIntent(v1.commandType),
                writeAuthorization = ParseWriteAuthorization(v1.defaultWrite),
                riskLevel = ParseRiskLevel(v1.riskLevel),
                failurePolicy = ParseWriteAuthorization(v1.defaultWrite) == ESAgentWriteAuthorization.ScopedWrites
                    ? ESAgentFailurePolicy.RollbackAndReport
                    : ESAgentFailurePolicy.StopAndReport,
                purpose = v1.purpose,
                expectedInputs = v1.expectedInputs,
                allowedWriteScopes = string.IsNullOrWhiteSpace(v1.defaultWrite)
                    ? "未授权写入；需要修改时必须重新取得用户确认。"
                    : v1.defaultWrite,
                executionOutline = v1.executionOutline,
                acceptanceCriteria = v1.acceptanceCriteria,
                requiredSections = v1.requiredSections
            };
            return ESAgentOutputContractValidator.TryValidate(payload, out error);
        }

        public static bool TryMigrateSkill(string payloadJson,
            out ESAgentSkillOutputPayload payload, out string error)
        {
            payload = null;
            if (!TryReadSchema(payloadJson, out int schemaVersion, out error))
                return false;
            if (schemaVersion == ESAgentSkillOutputPayload.CurrentSchemaVersion)
            {
                payload = JsonUtility.FromJson<ESAgentSkillOutputPayload>(payloadJson);
                return ESAgentOutputContractValidator.TryValidate(payload, out error);
            }
            if (schemaVersion != 1)
            {
                error = "不支持的 Agent Skill Payload Schema：" + schemaVersion;
                return false;
            }

            ESAgentSkillOutputPayloadV1 v1 = JsonUtility.FromJson<ESAgentSkillOutputPayloadV1>(payloadJson);
            payload = new ESAgentSkillOutputPayload
            {
                skillName = v1.skillName,
                targetProjectPath = v1.targetProjectPath,
                operationMode = v1.operationMode,
                description = v1.description,
                triggerScenarios = v1.triggerScenarios,
                workflow = v1.workflow,
                nonGoals = v1.nonGoals,
                validationSteps = v1.validationSteps,
                defaultPrompt = v1.defaultPrompt,
                includeAgentsMetadata = true,
                includeReferences = v1.includeReferences,
                includeScripts = v1.includeScripts
            };
            return ESAgentOutputContractValidator.TryValidate(payload, out error);
        }

        private static bool TryReadSchema(string payloadJson, out int schemaVersion, out string error)
        {
            schemaVersion = 0;
            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                error = "Payload JSON 不能为空。";
                return false;
            }
            try
            {
                SchemaHeader header = JsonUtility.FromJson<SchemaHeader>(payloadJson);
                schemaVersion = header?.schemaVersion ?? 0;
                if (schemaVersion > 0)
                {
                    error = string.Empty;
                    return true;
                }
                error = "Payload SchemaVersion 无效。";
                return false;
            }
            catch (ArgumentException exception)
            {
                error = "Payload JSON 无效：" + exception.Message;
                return false;
            }
        }

        private static ESAgentCommandIntent ParseCommandIntent(string value)
        {
            if ((value ?? string.Empty).Contains("只读")) return ESAgentCommandIntent.ReadOnlyReview;
            if ((value ?? string.Empty).Contains("方案")) return ESAgentCommandIntent.PlanReview;
            if ((value ?? string.Empty).Contains("信息")) return ESAgentCommandIntent.ContextOnly;
            if ((value ?? string.Empty).Contains("交接")) return ESAgentCommandIntent.Handoff;
            return ESAgentCommandIntent.ControlledExecution;
        }

        private static ESAgentWriteAuthorization ParseWriteAuthorization(string value)
        {
            string normalized = value ?? string.Empty;
            if (normalized.StartsWith("否", StringComparison.Ordinal)) return ESAgentWriteAuthorization.NoWrites;
            if (normalized.Contains("用户确认") || normalized.Contains("需确认")
                || normalized.Contains("由用户") || normalized.Contains("由本节点"))
                return ESAgentWriteAuthorization.ConfirmBeforeWrite;
            return normalized.Contains("是") || normalized.Contains("允许")
                ? ESAgentWriteAuthorization.ScopedWrites
                : ESAgentWriteAuthorization.ConfirmBeforeWrite;
        }

        private static ESAgentRiskLevel ParseRiskLevel(string value)
        {
            string normalized = value ?? string.Empty;
            if (normalized.Contains("L3")) return ESAgentRiskLevel.L3;
            if (normalized.Contains("L2")) return ESAgentRiskLevel.L2;
            return ESAgentRiskLevel.L1;
        }
    }

    public sealed class ESAgentAICommandOutputV1ToV2Migrator : IESGraphNodeMigrator
    {
        public ESGraphDomainKey Domain => ESAgentGraphStableIds.Domain;
        public ESGraphNodeTypeKey NodeType => ESAgentGraphStableIds.Node(ESAgentGraphStableIds.AICommandOutputNode);
        public int FromVersion => 1;
        public int ToVersion => 2;
        public int Priority => 0;

        public bool TryMigrate(GraphAsset asset, ESGraphNodeRecord node, out string error)
        {
            if (asset == null || node == null)
            {
                error = "AICommand 节点迁移上下文为空。";
                return false;
            }
            if (!ESAgentOutputPayloadMigration.TryMigrateAICommand(node.payloadJson,
                    out ESAgentAICommandOutputPayload payload, out error))
                return false;
            return asset.UpdateNode(node.nodeId, node.TypeKey, ToVersion, node.title,
                JsonUtility.ToJson(payload), out error);
        }
    }

    public sealed class ESAgentSkillOutputV1ToV2Migrator : IESGraphNodeMigrator
    {
        public ESGraphDomainKey Domain => ESAgentGraphStableIds.Domain;
        public ESGraphNodeTypeKey NodeType => ESAgentGraphStableIds.Node(ESAgentGraphStableIds.AISkillOutputNode);
        public int FromVersion => 1;
        public int ToVersion => 2;
        public int Priority => 0;

        public bool TryMigrate(GraphAsset asset, ESGraphNodeRecord node, out string error)
        {
            if (asset == null || node == null)
            {
                error = "Agent Skill 节点迁移上下文为空。";
                return false;
            }
            if (!ESAgentOutputPayloadMigration.TryMigrateSkill(node.payloadJson,
                    out ESAgentSkillOutputPayload payload, out error))
                return false;
            return asset.UpdateNode(node.nodeId, node.TypeKey, ToVersion, node.title,
                JsonUtility.ToJson(payload), out error);
        }
    }

    public sealed class ESAgentConstraintV1ToV2Migrator : IESGraphNodeMigrator
    {
        public ESGraphDomainKey Domain => ESAgentGraphStableIds.Domain;
        public ESGraphNodeTypeKey NodeType => ESAgentGraphStableIds.Node(ESAgentGraphStableIds.ConstraintNode);
        public int FromVersion => 1;
        public int ToVersion => ESAgentConstraintPayload.CurrentSchemaVersion;
        public int Priority => 0;

        public bool TryMigrate(GraphAsset asset, ESGraphNodeRecord node, out string error)
        {
            if (asset == null || node == null)
            {
                error = "Constraint 节点迁移上下文为空。";
                return false;
            }
            if (!ESAgentConstraintPayloadMigration.TryMigrate(node.payloadJson,
                    out ESAgentConstraintPayload payload, out error))
                return false;
            return asset.UpdateNode(node.nodeId, node.TypeKey, ToVersion, node.title,
                JsonUtility.ToJson(payload), out error);
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

    [Serializable]
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
        public string fromNodeId;
        public string fromNodeTypeId;
        public string fromNodeTitle;
        public string fromPortStableKey;
        public string toNodeId;
        public string toNodeTypeId;
        public string toNodeTitle;
        public string toPortStableKey;
        public ESAgentRelationKind relationKind;
        public string semanticType;
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
    [Serializable]
    public sealed class ESAgentArtifactGenerationSpec : IESBakedGraphPlan
    {
        public const int CurrentContractSchemaVersion = 5;

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
            var relationKeys = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < relations.Length; i++)
            {
                ESAgentGenerationRelation relation = relations[i];
                if (relation == null || string.IsNullOrWhiteSpace(relation.edgeId)
                    || !edgeIds.Add(relation.edgeId)
                    || string.IsNullOrWhiteSpace(relation.fromNodeId)
                    || string.IsNullOrWhiteSpace(relation.toNodeId)
                    || !Enum.IsDefined(typeof(ESAgentRelationKind), relation.relationKind))
                {
                    error = "GenerationSpec 包含无效关系。";
                    return false;
                }
                string relationKey = relation.fromNodeId + "\n" + relation.fromPortStableKey + "\n"
                    + relation.toNodeId + "\n" + relation.relationKind;
                if (!relationKeys.Add(relationKey))
                {
                    error = "GenerationSpec 包含重复关系：" + relation.edgeId;
                    return false;
                }
                if (!nodeTypes.TryGetValue(relation.fromNodeId, out string expectedFromType)
                    || !nodeTypes.TryGetValue(relation.toNodeId, out string expectedToType)
                    || !string.Equals(relation.fromNodeTypeId, expectedFromType, StringComparison.Ordinal)
                    || !string.Equals(relation.toNodeTypeId, expectedToType, StringComparison.Ordinal)
                    || !ESAgentRelationSemantics.TryResolve(expectedFromType, expectedToType,
                        relation.fromPortStableKey, out ESAgentRelationKind expected))
                {
                    error = "GenerationSpec 包含跨阶段或未知节点关系："
                        + relation.fromNodeId + " -> " + relation.toNodeId;
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
                    if (!routes.Add(relation.fromPortStableKey))
                    {
                        error = "逻辑节点的同一出口不能重复连接：" + relation.fromNodeId
                            + "/" + relation.fromPortStableKey;
                        return false;
                    }
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
                || string.IsNullOrWhiteSpace(branch.failurePath))
            {
                error = "GenerationSpec Branch 缺少条件、命中、默认或失败语义。";
                return false;
            }
            error = string.Empty;
            return true;
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
                ESGraphPortRecord actual = ports.FirstOrDefault(port => port != null
                    && string.Equals(port.stableKey, expected.stableKey, StringComparison.Ordinal));
                if (actual == null || actual.direction != expected.direction || actual.capacity != expected.capacity
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
                    ESGraphPortRecord port = (node.ports ?? new List<ESGraphPortRecord>()).FirstOrDefault(item =>
                        item != null && string.Equals(item.stableKey, requiredKeys[i], StringComparison.Ordinal));
                    int count = port != null && edgeCountByOutput.TryGetValue(port.portId, out int value) ? value : 0;
                    if (count != 1)
                        issues.Add(ESGraphValidationIssue.Error("AgentAuthoring.LogicRoute",
                            "逻辑节点出口必须且只能连接一次：" + requiredKeys[i], node.nodeId));
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
                            defaultPath = branch.defaultPath, failurePath = branch.failurePath });
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
                validations = validations.ToArray(), relations = relations.ToArray() };
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
            var ownerByPort = new Dictionary<string, ESGraphNodeSnapshot>(StringComparer.Ordinal);
            foreach (ESGraphNodeSnapshot node in source.Nodes)
                foreach (ESGraphPortSnapshot port in node.Ports)
                    ownerByPort[port.PortId] = node;

            foreach (ESGraphEdgeSnapshot edge in source.Edges)
            {
                if (!source.TryGetPort(edge.OutputPortId, out ESGraphPortSnapshot output)
                    || !source.TryGetPort(edge.InputPortId, out ESGraphPortSnapshot input)
                    || !ownerByPort.TryGetValue(edge.OutputPortId, out ESGraphNodeSnapshot from)
                    || !ownerByPort.TryGetValue(edge.InputPortId, out ESGraphNodeSnapshot to))
                {
                    failures.Add(ESGraphValidationIssue.Error("AgentAuthoring.Relation.Bake",
                        "无法解析思路图关系。", edge.EdgeId));
                    continue;
                }
                if (!ESAgentRelationSemantics.TryResolve(from.TypeId, to.TypeId, output.StableKey,
                        out ESAgentRelationKind relationKind))
                {
                    failures.Add(ESGraphValidationIssue.Error("AgentAuthoring.Relation.Semantics",
                        "无法确定思路图关系语义。", edge.EdgeId));
                    continue;
                }
                relations.Add(new ESAgentGenerationRelation
                {
                    edgeId = edge.EdgeId,
                    fromNodeId = from.NodeId,
                    fromNodeTypeId = from.TypeId,
                    fromNodeTitle = from.Title,
                    fromPortStableKey = output.StableKey,
                    toNodeId = to.NodeId,
                    toNodeTypeId = to.TypeId,
                    toNodeTitle = to.Title,
                    toPortStableKey = input.StableKey,
                    relationKind = relationKind,
                    semanticType = output.ValueTypeId
                });
            }
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
            foreach (ESGraphNodeRecord node in asset.Nodes)
            {
                if (node == null || !definitions.TryGetValue(node.TypeKey, out IESGraphNodeDefinition definition)) continue;
                if (node.ports == null || node.ports.Count != definition.Ports.Count)
                {
                    error = "节点端口数量无法安全迁移，请重新创建节点：" + (node.title ?? node.nodeId);
                    return false;
                }
            }
            foreach (ESGraphNodeRecord node in asset.Nodes)
            {
                if (node == null || !definitions.TryGetValue(node.TypeKey, out IESGraphNodeDefinition definition)) continue;
                for (int i = 0; i < definition.Ports.Count; i++)
                {
                    ESGraphPortDefinition expected = definition.Ports[i];
                    ESGraphPortRecord port = node.ports[i];
                    port.name = expected.name;
                    port.stableKey = expected.stableKey;
                    port.valueTypeId = expected.valueTypeId;
                    port.direction = expected.direction;
                    port.capacity = expected.capacity;
                }
            }
            error = string.Empty;
            return true;
        }
    }

    public abstract class ESAgentPayloadInspector<T> : IESGraphPayloadInspector, IESGraphNodeCardProvider
        where T : class, new()
    {
        protected static readonly List<string> CardOperationLabels = new List<string>
        {
            "自动创建或更新",
            "仅创建",
            "仅更新"
        };

        public ESGraphDomainKey Domain => ESAgentGraphStableIds.Domain;
        public abstract ESGraphNodeTypeKey NodeType { get; }
        public virtual int Priority => 0;
        public VisualElement Create(string payloadJson, Action<string> commitPayload)
        {
            if (!ESAgentAuthoringGraphValidator.TryRead(payloadJson, out T payload, out _)) payload = new T();
            VisualElement root = Build(payload, () => commitPayload?.Invoke(JsonUtility.ToJson(payload)));
            ESGraphInspectorVisuals.StylePayloadRoot(root);
            return root;
        }

        public VisualElement CreateCard(ESGraphNodeCardContext context)
        {
            if (context == null)
                return null;
            if (!ESAgentAuthoringGraphValidator.TryRead(context.PayloadJson, out T payload, out _)) payload = new T();
            VisualElement root = BuildCard(payload, context,
                () => context.CommitPayload(JsonUtility.ToJson(payload)));
            if (root == null)
                return null;
            root.name = "es-node-key-fields";
            root.userData = context;
            root.style.marginTop = 3f;
            root.style.marginBottom = 4f;
            root.style.paddingTop = 4f;
            root.style.paddingBottom = 4f;
            root.style.paddingLeft = 5f;
            root.style.paddingRight = 5f;
            root.style.backgroundColor = new Color(0.075f, 0.09f, 0.12f, 0.88f);
            root.style.borderTopWidth = 1f;
            root.style.borderBottomWidth = 1f;
            root.style.borderLeftWidth = 1f;
            root.style.borderRightWidth = 1f;
            Color border = new Color(0.25f, 0.31f, 0.39f, 0.9f);
            root.style.borderTopColor = border;
            root.style.borderBottomColor = border;
            root.style.borderLeftColor = border;
            root.style.borderRightColor = border;
            root.style.borderTopLeftRadius = 4f;
            root.style.borderTopRightRadius = 4f;
            root.style.borderBottomLeftRadius = 4f;
            root.style.borderBottomRightRadius = 4f;
            return root;
        }

        protected abstract VisualElement Build(T payload, Action commit);
        protected virtual VisualElement BuildCard(T payload, ESGraphNodeCardContext context, Action commit)
        {
            return null;
        }

        protected static TextField CardText(ESGraphNodeCardContext context, string name, string label,
            string value, string tooltip, Action<string> set, Action commit, string fieldName = null)
        {
            var field = new TextField(label)
            {
                name = name,
                value = value ?? string.Empty,
                isDelayed = true,
                tooltip = tooltip ?? string.Empty
            };
            field.style.minHeight = 22f;
            field.style.fontSize = 11f;
            field.style.marginTop = 1f;
            field.style.marginBottom = 1f;
            field.labelElement.style.minWidth = 48f;
            field.labelElement.style.maxWidth = 64f;
            field.labelElement.style.fontSize = 9f;
            field.isReadOnly = !(context?.CanEditPayload ?? false);
            StyleField(field, field.labelElement, fieldName, IsEmptyValue(value));
            field.RegisterValueChangedCallback(evt =>
            {
                if (context?.CanEditPayload != true)
                    return;
                string next = evt.newValue ?? string.Empty;
                set?.Invoke(next);
                commit?.Invoke();
            });
            return field;
        }

        protected static TextField CardReadOnlyText(string name, string label, string value, string tooltip,
            string fieldName = null)
        {
            var field = new TextField(label)
            {
                name = name,
                value = value ?? string.Empty,
                isReadOnly = true,
                tooltip = tooltip ?? value ?? string.Empty
            };
            field.style.minHeight = 20f;
            field.style.fontSize = 10f;
            field.style.marginTop = 1f;
            field.style.marginBottom = 1f;
            field.labelElement.style.minWidth = 48f;
            field.labelElement.style.maxWidth = 64f;
            field.labelElement.style.fontSize = 9f;
            StyleField(field, field.labelElement, fieldName, IsEmptyValue(value));
            return field;
        }

        protected static PopupField<string> CardPopup(ESGraphNodeCardContext context, string name, string label,
            List<string> choices, int selectedIndex, Action<int> set, Action commit, string fieldName = null)
        {
            var field = new PopupField<string>(label, choices,
                Mathf.Clamp(selectedIndex, 0, Math.Max(0, choices.Count - 1)))
            {
                name = name
            };
            field.style.minHeight = 22f;
            field.style.fontSize = 11f;
            field.style.marginTop = 1f;
            field.style.marginBottom = 1f;
            field.labelElement.style.minWidth = 48f;
            field.labelElement.style.maxWidth = 64f;
            field.labelElement.style.fontSize = 9f;
            field.SetEnabled(context?.CanEditPayload ?? false);
            StyleField(field, field.labelElement, fieldName, false);
            field.RegisterValueChangedCallback(evt =>
            {
                if (context?.CanEditPayload != true)
                    return;
                set?.Invoke(Math.Max(0, choices.IndexOf(evt.newValue)));
                commit?.Invoke();
            });
            return field;
        }

        protected static Toggle CardToggle(ESGraphNodeCardContext context, string name, string label, bool value,
            Action<bool> set, Action commit, string fieldName = null)
        {
            var field = new Toggle(label) { name = name, value = value };
            field.style.minHeight = 20f;
            field.style.fontSize = 11f;
            field.style.marginTop = 1f;
            field.style.marginBottom = 1f;
            field.SetEnabled(context?.CanEditPayload ?? false);
            StyleField(field, field.labelElement, fieldName, !value);
            field.RegisterValueChangedCallback(evt =>
            {
                if (context?.CanEditPayload != true)
                    return;
                set?.Invoke(evt.newValue);
                commit?.Invoke();
            });
            return field;
        }

        protected static IntegerField CardInteger(ESGraphNodeCardContext context, string name, string label,
            int value, int min, int max, Action<int> set, Action commit, string fieldName = null)
        {
            var field = new IntegerField(label) { name = name, value = value, isDelayed = true };
            field.style.minHeight = 22f;
            field.style.fontSize = 11f;
            field.style.marginTop = 1f;
            field.style.marginBottom = 1f;
            field.labelElement.style.minWidth = 48f;
            field.labelElement.style.maxWidth = 64f;
            field.labelElement.style.fontSize = 9f;
            field.SetEnabled(context?.CanEditPayload ?? false);
            StyleField(field, field.labelElement, fieldName, false);
            field.RegisterValueChangedCallback(evt =>
            {
                if (context?.CanEditPayload != true)
                    return;
                int next = Mathf.Clamp(evt.newValue, min, max);
                field.SetValueWithoutNotify(next);
                set?.Invoke(next);
                commit?.Invoke();
            });
            return field;
        }

        protected static VisualElement CardPathActions(ESGraphNodeCardContext context, string elementName,
            Func<string> getPath)
        {
            var row = new VisualElement { name = elementName };
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.FlexEnd;
            row.style.marginTop = 3f;

            Button copy = CardButton(elementName + "-copy", "复制路径", "复制完整项目路径。",
                () => context?.CopyText(getPath?.Invoke() ?? string.Empty));
            row.Add(copy);

            string initialPath = getPath?.Invoke() ?? string.Empty;
            Button locate = CardButton(elementName + "-locate", "定位", "在 Project 窗口定位当前项目资产。", () =>
            {
                string path = (getPath?.Invoke() ?? string.Empty).Replace('\\', '/');
                if (!path.StartsWith("Assets/", StringComparison.Ordinal))
                    return;
                UnityEngine.Object target = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                if (target == null)
                    return;
                Selection.activeObject = target;
                EditorGUIUtility.PingObject(target);
            });
            locate.SetEnabled(initialPath.Replace('\\', '/').StartsWith("Assets/", StringComparison.Ordinal));
            locate.style.marginLeft = 4f;
            row.Add(locate);
            return row;
        }

        protected static TextField CardArtifactStatus(string name, ESAgentArtifactKind kind,
            ESAgentArtifactOperationMode operationMode, string projectPath)
        {
            string status;
            string tooltip;
            if (!TryResolveArtifactPath(kind, projectPath, out string fullPath, out string error))
            {
                status = "路径非法 · " + error;
                tooltip = error;
            }
            else
            {
                bool exists = kind == ESAgentArtifactKind.AICommand
                    ? File.Exists(fullPath)
                    : Directory.Exists(fullPath);
                if (exists && operationMode == ESAgentArtifactOperationMode.CreateOnly)
                {
                    status = "已存在 · 仅创建将阻断";
                    tooltip = "目标已经存在，当前“仅创建”方式会在生成前阻断。";
                }
                else if (!exists && operationMode == ESAgentArtifactOperationMode.UpdateOnly)
                {
                    status = "尚未创建 · 仅更新将阻断";
                    tooltip = "目标尚不存在，当前“仅更新”方式会在生成前阻断。";
                }
                else
                {
                    status = exists ? "已存在 · 将更新" : "尚未创建 · 将新建";
                    tooltip = exists ? "目标路径当前存在。" : "目标路径当前不存在。";
                }
            }
            return CardReadOnlyText(name, "状态", status, tooltip);
        }

        protected static VisualElement CardArtifactActions(ESGraphNodeCardContext context, string elementName,
            ESAgentArtifactKind kind, Func<string> getPath, Func<string> getSuggestedPath, Action synchronizePath,
            Func<string> getInvocationToken = null)
        {
            var row = new VisualElement { name = elementName };
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            row.style.justifyContent = Justify.FlexEnd;
            row.style.marginTop = 3f;

            string useLabel = kind == ESAgentArtifactKind.AICommand ? "单次使用" : "临时使用";
            Button use = CardButton(elementName + "-use", useLabel,
                kind == ESAgentArtifactKind.AICommand
                    ? "只执行当前 AICommand Output 对应的 Graph 分支，不生成永久产物。"
                    : "只在本次任务中使用当前 Agent Skill Output 对应的 Graph 分支，不安装技能。",
                () => context?.ExecuteNodeAction(ESAgentNodeCardActionKeys.UseOnce));
            use.SetEnabled(context?.CanExecuteNodeAction(ESAgentNodeCardActionKeys.UseOnce) ?? false);
            row.Add(use);

            Button candidate = CardButton(elementName + "-candidate", "生成候选",
                "只为当前 Output 及其 Goal、Reference、Constraint、Validation 关系分支创建隔离候选。",
                () => context?.ExecuteNodeAction(ESAgentNodeCardActionKeys.SaveCandidate));
            candidate.SetEnabled(context?.CanExecuteNodeAction(ESAgentNodeCardActionKeys.SaveCandidate) ?? false);
            candidate.style.marginLeft = 4f;
            row.Add(candidate);

            Button synchronize = CardButton(elementName + "-sync", kind == ESAgentArtifactKind.AICommand
                    ? "同步路径" : "同步目录",
                "按当前名称生成受支持的正式目标路径。", () =>
                {
                    if (context?.CanEditPayload != true)
                        return;
                    synchronizePath?.Invoke();
                });
            synchronize.SetEnabled((context?.CanEditPayload ?? false)
                && ESAgentArtifactPathPolicy.IsAllowedTarget(kind, getSuggestedPath?.Invoke(), out _));
            synchronize.style.marginLeft = 4f;
            row.Add(synchronize);

            if (getInvocationToken != null)
            {
                Button invocation = CardButton(elementName + "-invocation", "复制调用",
                    "复制该 Agent Skill 的调用标记。",
                    () => context?.CopyText(getInvocationToken() ?? string.Empty));
                invocation.style.marginLeft = 4f;
                row.Add(invocation);
            }

            Button copy = CardButton(elementName + "-copy", "复制路径", "复制完整项目路径。",
                () => context?.CopyText(getPath?.Invoke() ?? string.Empty));
            copy.style.marginLeft = 4f;
            row.Add(copy);

            Button locate = CardButton(elementName + "-locate",
                kind == ESAgentArtifactKind.AICommand ? "定位" : "打开目录",
                kind == ESAgentArtifactKind.AICommand
                    ? "在 Project 窗口定位文件；文件不存在时打开目标目录。"
                    : "在文件管理器中打开技能目录；目录不存在时打开它的安全父目录。",
                () => RevealArtifactPath(context, kind, getPath?.Invoke()));
            locate.SetEnabled(ESAgentArtifactPathPolicy.IsAllowedTarget(kind, getPath?.Invoke(), out _));
            locate.style.marginLeft = 4f;
            row.Add(locate);
            return row;
        }

        private static void RevealArtifactPath(ESGraphNodeCardContext context, ESAgentArtifactKind kind,
            string projectPath)
        {
            if (!TryResolveArtifactPath(kind, projectPath, out string fullPath, out string error))
            {
                context?.Report(error);
                return;
            }
            if (kind == ESAgentArtifactKind.AICommand && File.Exists(fullPath))
            {
                string assetPath = (projectPath ?? string.Empty).Replace('\\', '/').Trim();
                UnityEngine.Object target = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
                if (target != null)
                {
                    Selection.activeObject = target;
                    EditorGUIUtility.PingObject(target);
                    return;
                }
                EditorUtility.RevealInFinder(fullPath);
                return;
            }

            string revealPath = kind == ESAgentArtifactKind.AgentSkill && Directory.Exists(fullPath)
                ? fullPath
                : Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(revealPath) && Directory.Exists(revealPath))
                EditorUtility.RevealInFinder(revealPath);
            else
                context?.Report("目标及其安全父目录尚不存在。请先检查名称和目标路径。");
        }

        private static bool TryResolveArtifactPath(ESAgentArtifactKind kind, string projectPath,
            out string fullPath, out string error)
        {
            fullPath = string.Empty;
            if (!ESAgentArtifactPathPolicy.IsAllowedTarget(kind, projectPath, out error))
                return false;
            try
            {
                string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                string normalized = (projectPath ?? string.Empty).Replace('/', Path.DirectorySeparatorChar)
                    .Replace('\\', Path.DirectorySeparatorChar).Trim();
                fullPath = Path.GetFullPath(Path.Combine(projectRoot, normalized));
                string rootPrefix = projectRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    + Path.DirectorySeparatorChar;
                if (!fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    fullPath = string.Empty;
                    error = "目标路径超出当前项目目录。";
                    return false;
                }
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                fullPath = string.Empty;
                error = "目标路径无法解析：" + exception.Message;
                return false;
            }
        }

        protected static Button CardButton(string name, string text, string tooltip, Action action)
        {
            var button = new Button(() => action?.Invoke())
            {
                name = name,
                text = text ?? string.Empty,
                tooltip = tooltip ?? string.Empty
            };
            StyleCardButton(button);
            return button;
        }

        private static void StyleCardButton(Button button)
        {
            button.style.minWidth = 48f;
            button.style.minHeight = 20f;
            button.style.paddingLeft = 5f;
            button.style.paddingRight = 5f;
            button.style.fontSize = 10f;
            button.style.flexShrink = 0f;
        }
        protected static TextField Text(string label, string value, bool multiline = false,
            string fieldName = null)
        {
            var field = new TextField(label) { value = value ?? string.Empty, multiline = multiline };
            ESGraphInspectorVisuals.StyleTextField(field);
            StyleField(field, field.labelElement, fieldName, IsEmptyValue(value));
            return field;
        }

        protected static VisualElement FieldSummary(T payload, string title = "关键字段")
        {
            int core = 0;
            int coreReady = 0;
            int important = 0;
            int importantReady = 0;
            IReadOnlyList<ESFieldPresentationMetadata> summaryFields
                = ESFieldPresentationMetadataCache.GetSummaryFields(typeof(T));
            for (int i = 0; i < summaryFields.Count; i++)
            {
                ESFieldPresentationMetadata metadata = summaryFields[i];
                bool ready = IsReadyValue(metadata.Field.GetValue(payload), metadata.Required);
                if (metadata.Level == ESFieldLevel.Core)
                {
                    core++;
                    if (ready) coreReady++;
                }
                else
                {
                    important++;
                    if (ready) importantReady++;
                }
            }

            var box = new VisualElement { name = "es-field-summary" };
            box.style.flexDirection = FlexDirection.Row;
            box.style.alignItems = Align.Center;
            box.style.marginTop = 2f;
            box.style.marginBottom = 5f;
            box.style.paddingLeft = 7f;
            box.style.paddingRight = 7f;
            box.style.paddingTop = 4f;
            box.style.paddingBottom = 4f;
            box.style.borderLeftWidth = 3f;
            Color accent = coreReady == core
                ? ESEditorPresentation.GetFieldLevelAccent(ESFieldLevel.Core)
                : ESEditorPresentation.GetStatusAccent(0, ESStatusKind.Error);
            box.style.borderLeftColor = accent;
            Color background = accent;
            background.a = EditorGUIUtility.isProSkin ? 0.11f : 0.07f;
            box.style.backgroundColor = background;
            var caption = new Label(title + " " + coreReady + "/" + core)
            {
                tooltip = coreReady == core
                    ? "核心字段已经完整。"
                    : "还有 " + (core - coreReady) + " 个核心字段需要补充。"
            };
            caption.style.unityFontStyleAndWeight = FontStyle.Bold;
            caption.style.fontSize = 10f;
            caption.style.color = accent;
            caption.style.flexGrow = 1f;
            box.Add(caption);
            if (important > 0)
            {
                var secondary = new Label("重点 " + importantReady + "/" + important);
                secondary.style.fontSize = 9f;
                secondary.style.color = new Color(0.64f, 0.7f, 0.8f, 0.95f);
                box.Add(secondary);
            }
            return box;
        }

        protected static void StyleField(VisualElement field, Label label, string fieldName, bool isEmpty)
        {
            if (field == null
                || !ESFieldPresentationMetadataCache.TryGet(
                    typeof(T), fieldName, out ESFieldPresentationMetadata metadata))
                return;
            ESEditorPresentation.StyleField(field, label, metadata.Level,
                metadata.Required, isEmpty, metadata.Hint);
        }

        private static bool IsEmptyValue(string value)
        {
            return string.IsNullOrWhiteSpace(value);
        }

        private static bool IsReadyValue(object value, bool required)
        {
            if (!required)
                return true;
            if (value == null)
                return false;
            if (value is string text)
                return !string.IsNullOrWhiteSpace(text);
            if (value is bool enabled)
                return enabled;
            return true;
        }
        protected static void CommitOnFocusOut(TextField field, Action<string> set, Action commit)
        {
            if (field == null)
                return;
            string lastCommitted = field.value ?? string.Empty;
            field.RegisterCallback<FocusOutEvent>(_ =>
            {
                string next = field.value ?? string.Empty;
                if (string.Equals(lastCommitted, next, StringComparison.Ordinal))
                    return;
                set?.Invoke(next);
                commit?.Invoke();
                lastCommitted = next;
            });
        }

        protected static VisualElement SearchPicker(string labelText, string buttonText, string tooltip,
            Action refresh, out Button pickerButton)
        {
            var row = new VisualElement();
            var label = new Label(labelText);
            row.Add(label);

            pickerButton = new Button
            {
                text = buttonText + "  ▼",
                tooltip = tooltip
            };
            row.Add(pickerButton);
            ESGraphInspectorVisuals.StylePickerRow(row, label, pickerButton);

            if (refresh != null)
            {
                var refreshButton = new Button(refresh)
                {
                    text = "刷新",
                    tooltip = "重新扫描项目中的可选项；扫描只在点击后执行。"
                };
                refreshButton.style.width = 48f;
                refreshButton.style.minWidth = 48f;
                refreshButton.style.minHeight = 24f;
                refreshButton.style.flexGrow = 0f;
                refreshButton.style.marginLeft = 3f;
                row.Add(refreshButton);
            }
            return row;
        }

        protected static VisualElement OperationPicker(ESAgentArtifactOperationMode current,
            Action<ESAgentArtifactOperationMode> onSelected)
        {
            VisualElement row = SearchPicker(
                "创建 / 更新方式",
                OperationLabel(current),
                "自动创建或更新最常用；仅创建与仅更新会在目标状态不匹配时阻断。",
                null,
                out Button pickerButton);
            ESAgentArtifactOperationMode selectedValue = current;
            pickerButton.clicked += () =>
            {
                Action<ESAgentArtifactOperationMode> select = value =>
                {
                    selectedValue = value;
                    pickerButton.text = OperationLabel(value) + "  ▼";
                    onSelected?.Invoke(value);
                };
                ESSearchDropdown.Open(
                    pickerButton,
                    "选择创建 / 更新方式",
                    new[]
                    {
                        OperationEntry(ESAgentArtifactOperationMode.CreateOrUpdate, selectedValue,
                            "自动创建或更新", "目标不存在时创建；通过稳定 ArtifactId 找到已有目标时更新。", select,
                            "推荐"),
                        OperationEntry(ESAgentArtifactOperationMode.CreateOnly, selectedValue,
                            "仅创建", "目标或目录已经存在时立即阻断，避免覆盖。", select),
                        OperationEntry(ESAgentArtifactOperationMode.UpdateOnly, selectedValue,
                            "仅更新", "找不到携带相同 ArtifactId 的正式产物时立即阻断。", select)
                    },
                    minimumWindowSize: new Vector2(500f, 280f));
            };
            return row;
        }

        private static ESSearchDropdown.Entry OperationEntry(ESAgentArtifactOperationMode value,
            ESAgentArtifactOperationMode current, string label, string description,
            Action<ESAgentArtifactOperationMode> onSelected, string badge = null)
        {
            bool selected = value == current;
            return ESSearchDropdown.Entry.Item(
                label,
                () => onSelected?.Invoke(value),
                subtitle: description,
                badge: selected ? "当前" : badge,
                selected: selected);
        }

        private static string OperationLabel(ESAgentArtifactOperationMode value)
        {
            switch (value)
            {
                case ESAgentArtifactOperationMode.CreateOnly:
                    return "仅创建";
                case ESAgentArtifactOperationMode.UpdateOnly:
                    return "仅更新";
                default:
                    return "自动创建或更新";
            }
        }

        protected static IEnumerable<ESSearchDropdown.Entry> PathEntries(IEnumerable<string> paths,
            string currentPath, Action<string> onSelected)
        {
            string current = NormalizePickerPath(currentPath);
            if (paths == null)
                yield break;
            foreach (string rawPath in paths)
            {
                string path = NormalizePickerPath(rawPath);
                if (string.IsNullOrEmpty(path))
                    continue;
                if (path.StartsWith("<", StringComparison.Ordinal))
                {
                    yield return ESSearchDropdown.Entry.Disabled(path, tooltip: "请刷新列表或确认对应目录中已有可用内容。");
                    continue;
                }

                string captured = path;
                bool selected = string.Equals(current, path, StringComparison.Ordinal);
                yield return ESSearchDropdown.Entry.Item(
                    GetPickerDisplayName(path),
                    () => onSelected?.Invoke(captured),
                    GetPickerGroup(path),
                    subtitle: GetPickerParentCaption(path),
                    badge: selected ? "当前" : null,
                    selected: selected);
            }
        }

        private static string NormalizePickerPath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/').Trim();
        }

        private static string GetPickerDisplayName(string path)
        {
            string trimmed = path.TrimEnd('/');
            int separator = trimmed.LastIndexOf('/');
            return separator >= 0 ? trimmed.Substring(separator + 1) : trimmed;
        }

        private static string GetPickerParentCaption(string path)
        {
            string[] segments = path.TrimEnd('/').Split('/');
            int parentCount = segments.Length - 1;
            if (parentCount <= 0)
                return string.Empty;
            if (parentCount <= 2)
                return string.Join("/", segments.Take(parentCount));
            return "…/" + segments[parentCount - 2] + "/" + segments[parentCount - 1];
        }

        private static string GetPickerGroup(string path)
        {
            const string warningsRoot = "Assets/Plugins/ES/AIWarnings/";
            if (path.StartsWith(warningsRoot, StringComparison.Ordinal))
            {
                string tail = path.Substring(warningsRoot.Length);
                int separator = tail.IndexOf('/');
                return separator > 0 ? "项目规则/" + tail.Substring(0, separator) : "项目规则";
            }
            if (path.StartsWith("Assets/Plugins/ES/AICommands/", StringComparison.Ordinal))
                return "AICommand 命令";
            if (path.StartsWith(".agents/skills/", StringComparison.Ordinal))
            {
                if (path.EndsWith("/", StringComparison.Ordinal))
                    return "Agent Skill 技能";
                string[] segments = path.Split('/');
                return segments.Length > 2 ? "Agent Skill 技能/" + segments[2] : "Agent Skill 技能";
            }
            if (path.StartsWith("Assets/Scripts/", StringComparison.Ordinal))
                return "C# 源码/项目逻辑";
            if (path.StartsWith("Assets/Plugins/ES/", StringComparison.Ordinal))
                return "ES 插件内容";
            if (path.StartsWith("Documentation/", StringComparison.Ordinal)
                || path.StartsWith("ES/Documentation/", StringComparison.Ordinal))
                return "项目文档";
            return "项目资产";
        }
    }

    public sealed class ESAgentGoalPayloadInspector : ESAgentPayloadInspector<ESAgentGoalPayload>
    {
        public override ESGraphNodeTypeKey NodeType => ESAgentGraphStableIds.Node(ESAgentGraphStableIds.GoalNode);
        protected override VisualElement Build(ESAgentGoalPayload p, Action commit)
        {
            var r = new VisualElement();
            r.Add(new HelpBox("最终目的和成功标准是发送、生成、更新与复制前的硬门禁。",
                HelpBoxMessageType.Info));
            r.Add(FieldSummary(p, "目标核心字段"));
            var a = Text("标题", p.title, fieldName: nameof(p.title));
            var b = Text("最终目的", p.objective, true, nameof(p.objective));
            var c = Text("背景与上下文", p.context, true, nameof(p.context));
            var d = Text("目标用户 / 触发场景", p.targetUsers, true, nameof(p.targetUsers));
            var e = Text("成功标准 / 最终结果", p.successCriteria, true, nameof(p.successCriteria));
            foreach (TextField field in new[] { a, b, c, d, e }) r.Add(field);
            CommitOnFocusOut(a, x => p.title = x, commit);
            CommitOnFocusOut(b, x => p.objective = x, commit);
            CommitOnFocusOut(c, x => p.context = x, commit);
            CommitOnFocusOut(d, x => p.targetUsers = x, commit);
            CommitOnFocusOut(e, x => p.successCriteria = x, commit);
            return r;
        }

        protected override VisualElement BuildCard(ESAgentGoalPayload p, ESGraphNodeCardContext context,
            Action commit)
        {
            var root = new VisualElement();
            root.Add(FieldSummary(p, "目标"));
            root.Add(CardText(context, "es-node-card-goal-title", "目标", p.title,
                "生成目标的短标题。按 Enter 或离开输入框后提交。", value => p.title = value, commit,
                nameof(p.title)));
            root.Add(CardText(context, "es-node-card-goal-objective", "目的", p.objective,
                p.objective, value => p.objective = value, commit, nameof(p.objective)));
            root.Add(CardReadOnlyText("es-node-card-goal-relations", "关系",
                context.IncomingConnectionCount + " 入 / " + context.OutgoingConnectionCount + " 出",
                "只读连接摘要；完整关系仍以 Graph 连线为准。"));
            return root;
        }
    }

    public sealed class ESAgentReferencePayloadInspector : ESAgentPayloadInspector<ESAgentReferencePayload>
    {
        private static readonly List<string> ReferenceKindLabels = new List<string>
        {
            "项目最高规则 / 警告",
            "AICommand 命令",
            "Agent Skill 技能",
            "C# 源代码（高级）",
            "项目文档",
            "项目资产"
        };

        public override ESGraphNodeTypeKey NodeType => ESAgentGraphStableIds.Node(ESAgentGraphStableIds.ReferenceNode);
        protected override VisualElement Build(ESAgentReferencePayload p, Action commit)
        {
            var root = new VisualElement();
            root.Add(new HelpBox("选择生成前需要阅读的资料。普通使用可从下拉列表选择，也可以直接拖入项目资产。",
                HelpBoxMessageType.Info));
            var kind = new PopupField<string>("引用类型", ReferenceKindLabels,
                Mathf.Clamp((int)p.referenceKind, 0, ReferenceKindLabels.Count - 1));
            var path = Text("项目内文件路径（系统）", p.projectPath);
            path.tooltip = "相对于项目根目录的文件路径。优先使用拖入或下拉选择，避免手动输入。";
            var objectField = new ObjectField("拖入项目资产") { objectType = typeof(UnityEngine.Object), allowSceneObjects = false,
                value = p.projectPath.StartsWith("Assets/", StringComparison.Ordinal) ? AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(p.projectPath) : null };
            VisualElement available = SearchPicker(
                "从项目中选择",
                "搜索项目资料",
                "支持中文名称搜索和目录分组；选择后会同步路径与项目资产。",
                () => ESAgentAuthoringAssetCatalog.GetReferencePaths(p.referenceKind, path.value, true),
                out Button availableButton);
            availableButton.clicked += () =>
            {
                ESSearchDropdown.Open(
                    availableButton,
                    "选择项目资料",
                    () => PathEntries(
                        ESAgentAuthoringAssetCatalog.GetReferencePaths(p.referenceKind, path.value),
                        path.value,
                        selected =>
                        {
                            p.projectPath = selected;
                            path.SetValueWithoutNotify(selected);
                            objectField.SetValueWithoutNotify(selected.StartsWith("Assets/", StringComparison.Ordinal)
                                ? AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(selected)
                                : null);
                            commit();
                        }),
                    minimumWindowSize: new Vector2(560f, 380f));
            };
            var purpose = Text("用途", p.purpose, true);
            var required = new Toggle("生成前必须读取") { value = p.required };
            root.Add(kind); root.Add(path); root.Add(objectField); root.Add(available); root.Add(purpose); root.Add(required);
            kind.RegisterValueChangedCallback(e => { p.referenceKind = (ESAgentReferenceKind)Math.Max(0, ReferenceKindLabels.IndexOf(e.newValue)); commit(); });
            CommitOnFocusOut(path, value => p.projectPath = value, commit);
            objectField.RegisterValueChangedCallback(e => { string selected = AssetDatabase.GetAssetPath(e.newValue); if (string.IsNullOrEmpty(selected)) return; p.projectPath = selected.Replace('\\', '/'); path.SetValueWithoutNotify(p.projectPath); commit(); });
            CommitOnFocusOut(purpose, value => p.purpose = value, commit);
            required.RegisterValueChangedCallback(e => { p.required = e.newValue; commit(); });
            return root;
        }

        protected override VisualElement BuildCard(ESAgentReferencePayload p, ESGraphNodeCardContext context,
            Action commit)
        {
            var root = new VisualElement();
            root.Add(CardPopup(context, "es-node-card-reference-kind", "类型", ReferenceKindLabels,
                (int)p.referenceKind, value => p.referenceKind = (ESAgentReferenceKind)value, commit));
            root.Add(CardText(context, "es-node-card-reference-path", "路径", p.projectPath,
                p.projectPath, value => p.projectPath = value, commit));
            root.Add(CardToggle(context, "es-node-card-reference-required", "生成前必须读取", p.required,
                value => p.required = value, commit));
            root.Add(CardPathActions(context, "es-node-card-reference-actions", () => p.projectPath));
            return root;
        }
    }

    public sealed class ESAgentConstraintPayloadInspector : ESAgentPayloadInspector<ESAgentConstraintPayload>
    {
        private static readonly List<string> ConstraintKindLabels = new List<string>
        {
            "必须做到",
            "禁止事项",
            "允许范围",
            "质量要求"
        };
        private static readonly List<string> ScopeLabels = new List<string>
        {
            "整个产物", "授权边界", "输入与前置条件", "执行过程", "验证与证据", "失败恢复"
        };
        private static readonly List<string> CombinationLabels = new List<string>
        {
            "必须同时满足", "同组任一满足"
        };

        public override ESGraphNodeTypeKey NodeType => ESAgentGraphStableIds.Node(ESAgentGraphStableIds.ConstraintNode);
        protected override VisualElement Build(ESAgentConstraintPayload p, Action commit)
        {
            var root = new VisualElement();
            root.Add(new HelpBox("明确规则作用范围和合并方式。禁止项优先于必须项、允许项和质量项；同类型按优先级从高到低解释。",
                HelpBoxMessageType.Info));
            var kind = new PopupField<string>("规则类型", ConstraintKindLabels,
                Mathf.Clamp((int)p.kind, 0, ConstraintKindLabels.Count - 1));
            var scope = new PopupField<string>("作用范围", ScopeLabels,
                Mathf.Clamp((int)p.scope, 0, ScopeLabels.Count - 1));
            var combination = new PopupField<string>("合并方式", CombinationLabels,
                Mathf.Clamp((int)p.combinationMode, 0, CombinationLabels.Count - 1));
            var priority = new IntegerField("同类型优先级（0-100）") { value = Mathf.Clamp(p.priority, 0, 100), isDelayed = true };
            var group = Text("AnyOf 组合组", p.combinationGroup, false);
            group.SetEnabled(p.combinationMode == ESAgentConstraintCombinationMode.AnyOf);
            var statement = Text("规则 / 需求", p.statement, true);
            var rationale = Text("为什么需要这条规则", p.rationale, true);
            var verification = Text("如何确认已经做到", p.verification, true);
            root.Add(kind);
            root.Add(scope);
            root.Add(combination);
            root.Add(priority);
            root.Add(group);
            root.Add(statement);
            root.Add(rationale);
            root.Add(verification);
            kind.RegisterValueChangedCallback(evt =>
            {
                p.kind = (ESAgentConstraintKind)Math.Max(0, ConstraintKindLabels.IndexOf(evt.newValue));
                commit();
            });
            scope.RegisterValueChangedCallback(evt =>
            {
                p.scope = (ESAgentConstraintScope)Math.Max(0, ScopeLabels.IndexOf(evt.newValue));
                commit();
            });
            combination.RegisterValueChangedCallback(evt =>
            {
                p.combinationMode = (ESAgentConstraintCombinationMode)Math.Max(0,
                    CombinationLabels.IndexOf(evt.newValue));
                if (p.combinationMode == ESAgentConstraintCombinationMode.AnyOf
                    && string.IsNullOrWhiteSpace(p.combinationGroup))
                    p.combinationGroup = "option-set";
                else if (p.combinationMode == ESAgentConstraintCombinationMode.AllOf)
                    p.combinationGroup = string.Empty;
                group.SetValueWithoutNotify(p.combinationGroup);
                group.SetEnabled(p.combinationMode == ESAgentConstraintCombinationMode.AnyOf);
                commit();
            });
            priority.RegisterValueChangedCallback(evt =>
            {
                p.priority = Mathf.Clamp(evt.newValue, 0, 100);
                priority.SetValueWithoutNotify(p.priority);
                commit();
            });
            CommitOnFocusOut(group, value => p.combinationGroup = value?.Trim() ?? string.Empty, commit);
            CommitOnFocusOut(statement, value => p.statement = value, commit);
            CommitOnFocusOut(rationale, value => p.rationale = value, commit);
            CommitOnFocusOut(verification, value => p.verification = value, commit);
            return root;
        }

        protected override VisualElement BuildCard(ESAgentConstraintPayload p, ESGraphNodeCardContext context,
            Action commit)
        {
            var root = new VisualElement();
            root.Add(CardPopup(context, "es-node-card-constraint-kind", "类型", ConstraintKindLabels,
                (int)p.kind, value => p.kind = (ESAgentConstraintKind)value, commit));
            root.Add(CardPopup(context, "es-node-card-constraint-scope", "范围", ScopeLabels,
                (int)p.scope, value => p.scope = (ESAgentConstraintScope)value, commit));
            root.Add(CardPopup(context, "es-node-card-constraint-combination", "合并", CombinationLabels,
                (int)p.combinationMode, value =>
                {
                    p.combinationMode = (ESAgentConstraintCombinationMode)value;
                    if (p.combinationMode == ESAgentConstraintCombinationMode.AnyOf
                        && string.IsNullOrWhiteSpace(p.combinationGroup))
                        p.combinationGroup = "option-set";
                    else if (p.combinationMode == ESAgentConstraintCombinationMode.AllOf)
                        p.combinationGroup = string.Empty;
                }, commit));
            root.Add(CardInteger(context, "es-node-card-constraint-priority", "优先级", p.priority,
                0, 100, value => p.priority = value, commit));
            if (p.combinationMode == ESAgentConstraintCombinationMode.AnyOf)
                root.Add(CardText(context, "es-node-card-constraint-group", "组合组", p.combinationGroup,
                    p.combinationGroup, value => p.combinationGroup = value?.Trim() ?? string.Empty, commit));
            root.Add(CardText(context, "es-node-card-constraint-statement", "规则", p.statement,
                p.statement, value => p.statement = value, commit));
            return root;
        }
    }

    public sealed class ESAgentBranchPayloadInspector : ESAgentPayloadInspector<ESAgentBranchPayload>
    {
        public override ESGraphNodeTypeKey NodeType => ESAgentGraphStableIds.Node(ESAgentGraphStableIds.BranchNode);

        protected override VisualElement Build(ESAgentBranchPayload p, Action commit)
        {
            var root = new VisualElement();
            root.Add(new HelpBox("三条路径都是执行合同的一部分，缺少任一接线时整图不能 Bake。",
                HelpBoxMessageType.Info));
            root.Add(FieldSummary(p, "分支合同"));
            var condition = Text("判断条件", p.condition, true, nameof(p.condition));
            var matched = Text("条件命中", p.matchedPath, true, nameof(p.matchedPath));
            var fallback = Text("默认路径", p.defaultPath, true, nameof(p.defaultPath));
            var failure = Text("判断失败", p.failurePath, true, nameof(p.failurePath));
            root.Add(condition); root.Add(matched); root.Add(fallback); root.Add(failure);
            CommitOnFocusOut(condition, value => p.condition = value, commit);
            CommitOnFocusOut(matched, value => p.matchedPath = value, commit);
            CommitOnFocusOut(fallback, value => p.defaultPath = value, commit);
            CommitOnFocusOut(failure, value => p.failurePath = value, commit);
            return root;
        }

        protected override VisualElement BuildCard(ESAgentBranchPayload p,
            ESGraphNodeCardContext context, Action commit)
        {
            var root = new VisualElement();
            root.Add(FieldSummary(p, "分支合同"));
            root.Add(CardText(context, "es-node-card-branch-condition", "条件", p.condition,
                p.condition, value => p.condition = value, commit, nameof(p.condition)));
            root.Add(CardText(context, "es-node-card-branch-default", "默认", p.defaultPath,
                p.defaultPath, value => p.defaultPath = value, commit, nameof(p.defaultPath)));
            root.Add(CardText(context, "es-node-card-branch-failure", "失败", p.failurePath,
                p.failurePath, value => p.failurePath = value, commit, nameof(p.failurePath)));
            return root;
        }
    }

    public sealed class ESAgentTraversePayloadInspector : ESAgentPayloadInspector<ESAgentTraversePayload>
    {
        private static readonly List<string> OrderLabels = new List<string>
        {
            "按输入顺序", "依赖优先", "优先级优先"
        };

        public override ESGraphNodeTypeKey NodeType => ESAgentGraphStableIds.Node(ESAgentGraphStableIds.TraverseNode);

        protected override VisualElement Build(ESAgentTraversePayload p, Action commit)
        {
            var root = new VisualElement();
            root.Add(new HelpBox("遍历不创建 Graph 循环；达到最大深度或最大数量后必须停止。",
                HelpBoxMessageType.Info));
            root.Add(FieldSummary(p, "有界遍历合同"));
            var target = Text("遍历目标", p.target, true, nameof(p.target));
            var alias = Text("单项名称", p.itemAlias, false, nameof(p.itemAlias));
            var order = new PopupField<string>("处理顺序", OrderLabels,
                Mathf.Clamp((int)p.order, 0, OrderLabels.Count - 1));
            var maxDepth = new IntegerField("最大深度（1-32）") { value = p.maxDepth, isDelayed = true };
            var maxItems = new IntegerField("最大数量（1-512）") { value = p.maxItems, isDelayed = true };
            var stop = Text("停止条件", p.stopCondition, true, nameof(p.stopCondition));
            var empty = Text("空结果行为", p.emptyResultAction, true, nameof(p.emptyResultAction));
            var failure = Text("失败行为", p.failureAction, true, nameof(p.failureAction));
            root.Add(target); root.Add(alias); root.Add(order); root.Add(maxDepth); root.Add(maxItems);
            root.Add(stop); root.Add(empty); root.Add(failure);
            CommitOnFocusOut(target, value => p.target = value, commit);
            CommitOnFocusOut(alias, value => p.itemAlias = value, commit);
            order.RegisterValueChangedCallback(evt =>
            {
                p.order = (ESAgentTraversalOrder)Math.Max(0, OrderLabels.IndexOf(evt.newValue));
                commit();
            });
            maxDepth.RegisterValueChangedCallback(evt =>
            {
                p.maxDepth = Mathf.Clamp(evt.newValue, 1, 32);
                maxDepth.SetValueWithoutNotify(p.maxDepth);
                commit();
            });
            maxItems.RegisterValueChangedCallback(evt =>
            {
                p.maxItems = Mathf.Clamp(evt.newValue, 1, 512);
                maxItems.SetValueWithoutNotify(p.maxItems);
                commit();
            });
            CommitOnFocusOut(stop, value => p.stopCondition = value, commit);
            CommitOnFocusOut(empty, value => p.emptyResultAction = value, commit);
            CommitOnFocusOut(failure, value => p.failureAction = value, commit);
            return root;
        }

        protected override VisualElement BuildCard(ESAgentTraversePayload p,
            ESGraphNodeCardContext context, Action commit)
        {
            var root = new VisualElement();
            root.Add(FieldSummary(p, "有界遍历"));
            root.Add(CardText(context, "es-node-card-traverse-target", "目标", p.target,
                p.target, value => p.target = value, commit, nameof(p.target)));
            root.Add(CardPopup(context, "es-node-card-traverse-order", "顺序", OrderLabels,
                (int)p.order, value => p.order = (ESAgentTraversalOrder)value, commit));
            root.Add(CardInteger(context, "es-node-card-traverse-depth", "最大深度", p.maxDepth,
                1, 32, value => p.maxDepth = value, commit));
            root.Add(CardInteger(context, "es-node-card-traverse-count", "最大数量", p.maxItems,
                1, 512, value => p.maxItems = value, commit));
            return root;
        }
    }

    public sealed class ESAgentAICommandOutputInspector : ESAgentPayloadInspector<ESAgentAICommandOutputPayload>
    {
        private static readonly List<string> IntentLabels = new List<string>
        {
            "信息补全", "只读体检", "方案评审", "安全执行", "交接沉淀"
        };
        private static readonly List<string> WriteAuthorizationLabels = new List<string>
        {
            "不允许写入", "写入前必须确认", "允许声明范围内写入"
        };
        private static readonly List<string> RiskLabels = new List<string> { "L1", "L2", "L3" };
        private static readonly List<string> FailurePolicyLabels = new List<string>
        {
            "停止并报告", "回滚本次事务并报告"
        };

        public override ESGraphNodeTypeKey NodeType => ESAgentGraphStableIds.Node(ESAgentGraphStableIds.AICommandOutputNode);
        protected override VisualElement Build(ESAgentAICommandOutputPayload p, Action commit)
        {
            var r = new VisualElement();
            r.Add(new HelpBox("填写命令要解决的问题、允许修改的文件和验收方式。正式文件会在候选差异检查后才写入。",HelpBoxMessageType.Info));
            r.Add(FieldSummary(p, "AICommand 核心字段"));
            var a = Text("命令名称", p.commandName, fieldName: nameof(p.commandName));
            var b = Text("正式文件路径（系统）", p.targetProjectPath,
                fieldName: nameof(p.targetProjectPath));
            b.tooltip = b.tooltip + "\n正式 AICommand 文件的项目路径，优先从下拉列表选择。";
            VisualElement picker = SearchPicker(
                "选择已有命令",
                "搜索 AICommand",
                "按中文文件名搜索已有 AICommand；选择后会同步正式文件路径。",
                () => ESAgentAuthoringAssetCatalog.GetAICommandTargets(b.value, true),
                out Button pickerButton);
            pickerButton.clicked += () =>
            {
                ESSearchDropdown.Open(
                    pickerButton,
                    "选择已有 AICommand 命令",
                    () => PathEntries(
                        ESAgentAuthoringAssetCatalog.GetAICommandTargets(b.value),
                        b.value,
                        selected =>
                        {
                            p.targetProjectPath = selected;
                            b.SetValueWithoutNotify(selected);
                            commit();
                        }),
                    minimumWindowSize: new Vector2(560f, 380f));
            };
            VisualElement operation = OperationPicker(p.operationMode, value =>
            {
                p.operationMode = value;
                commit();
            });
            var intent = new PopupField<string>("任务意图", IntentLabels,
                Mathf.Clamp((int)p.commandIntent, 0, IntentLabels.Count - 1));
            var writeAuthorization = new PopupField<string>("写入授权", WriteAuthorizationLabels,
                Mathf.Clamp((int)p.writeAuthorization, 0, WriteAuthorizationLabels.Count - 1));
            var risk = new PopupField<string>("风险等级", RiskLabels,
                Mathf.Clamp((int)p.riskLevel - 1, 0, RiskLabels.Count - 1));
            var failurePolicy = new PopupField<string>("失败策略", FailurePolicyLabels,
                Mathf.Clamp((int)p.failurePolicy, 0, FailurePolicyLabels.Count - 1));
            StyleField(intent, intent.labelElement, nameof(p.commandIntent), false);
            StyleField(writeAuthorization, writeAuthorization.labelElement,
                nameof(p.writeAuthorization), false);
            StyleField(risk, risk.labelElement, nameof(p.riskLevel), false);
            StyleField(failurePolicy, failurePolicy.labelElement, nameof(p.failurePolicy), false);
            var f = Text("用途", p.purpose, true, nameof(p.purpose));
            var g = Text("预期输入", p.expectedInputs, true, nameof(p.expectedInputs));
            var preconditions = Text("执行前置条件", p.preconditions, true, nameof(p.preconditions));
            var allowedWriteScopes = Text("允许写入范围", p.allowedWriteScopes, true,
                nameof(p.allowedWriteScopes));
            var forbiddenOperations = Text("禁止操作", p.forbiddenOperations, true,
                nameof(p.forbiddenOperations));
            var h = Text("执行步骤", p.executionOutline, true, nameof(p.executionOutline));
            var i = Text("完成定义", p.acceptanceCriteria, true, nameof(p.acceptanceCriteria));
            var requiredEvidence = Text("必须提供的证据", p.requiredEvidence, true,
                nameof(p.requiredEvidence));
            var blockedHandling = Text("阻断 / 升级处理", p.blockedHandling, true,
                nameof(p.blockedHandling));
            var rollbackStrategy = Text("回滚 / 恢复要求", p.rollbackStrategy, true,
                nameof(p.rollbackStrategy));
            var j = Text("必须包含的章节", p.requiredSections, true, nameof(p.requiredSections));
            foreach (VisualElement v in new VisualElement[] { a, b, picker, operation, intent,
                         writeAuthorization, risk, failurePolicy, f, g, preconditions, allowedWriteScopes,
                         forbiddenOperations, h, i, requiredEvidence, blockedHandling, rollbackStrategy, j }) r.Add(v);
            CommitOnFocusOut(a,x=>p.commandName=x,commit);CommitOnFocusOut(b,x=>p.targetProjectPath=x,commit);
            intent.RegisterValueChangedCallback(e=>{p.commandIntent=(ESAgentCommandIntent)Math.Max(0,IntentLabels.IndexOf(e.newValue));commit();});
            writeAuthorization.RegisterValueChangedCallback(e=>{p.writeAuthorization=(ESAgentWriteAuthorization)Math.Max(0,WriteAuthorizationLabels.IndexOf(e.newValue));commit();});
            risk.RegisterValueChangedCallback(e=>{p.riskLevel=(ESAgentRiskLevel)(Math.Max(0,RiskLabels.IndexOf(e.newValue))+1);commit();});
            failurePolicy.RegisterValueChangedCallback(e=>{p.failurePolicy=(ESAgentFailurePolicy)Math.Max(0,FailurePolicyLabels.IndexOf(e.newValue));commit();});
            CommitOnFocusOut(f,x=>p.purpose=x,commit);CommitOnFocusOut(g,x=>p.expectedInputs=x,commit);CommitOnFocusOut(h,x=>p.executionOutline=x,commit);
            CommitOnFocusOut(preconditions,x=>p.preconditions=x,commit);CommitOnFocusOut(allowedWriteScopes,x=>p.allowedWriteScopes=x,commit);
            CommitOnFocusOut(forbiddenOperations,x=>p.forbiddenOperations=x,commit);CommitOnFocusOut(requiredEvidence,x=>p.requiredEvidence=x,commit);
            CommitOnFocusOut(blockedHandling,x=>p.blockedHandling=x,commit);CommitOnFocusOut(rollbackStrategy,x=>p.rollbackStrategy=x,commit);
            CommitOnFocusOut(i,x=>p.acceptanceCriteria=x,commit);CommitOnFocusOut(j,x=>p.requiredSections=x,commit);return r;
        }

        protected override VisualElement BuildCard(ESAgentAICommandOutputPayload p,
            ESGraphNodeCardContext context, Action commit)
        {
            var root = new VisualElement();
            root.Add(FieldSummary(p, "产物合同"));
            root.Add(CardText(context, "es-node-card-command-name", "命令", p.commandName,
                p.commandName, value => p.commandName = value, commit, nameof(p.commandName)));
            root.Add(CardText(context, "es-node-card-command-path", "路径", p.targetProjectPath,
                p.targetProjectPath, value => p.targetProjectPath = value, commit,
                nameof(p.targetProjectPath)));
            root.Add(CardPopup(context, "es-node-card-command-mode", "方式", CardOperationLabels,
                (int)p.operationMode, value => p.operationMode = (ESAgentArtifactOperationMode)value, commit,
                nameof(p.operationMode)));
            root.Add(CardPopup(context, "es-node-card-command-intent", "意图", IntentLabels,
                (int)p.commandIntent, value => p.commandIntent = (ESAgentCommandIntent)value, commit,
                nameof(p.commandIntent)));
            root.Add(CardPopup(context, "es-node-card-command-write", "写入", WriteAuthorizationLabels,
                (int)p.writeAuthorization, value => p.writeAuthorization = (ESAgentWriteAuthorization)value,
                commit, nameof(p.writeAuthorization)));
            root.Add(CardPopup(context, "es-node-card-command-risk", "风险", RiskLabels,
                Mathf.Clamp((int)p.riskLevel - 1, 0, RiskLabels.Count - 1),
                value => p.riskLevel = (ESAgentRiskLevel)(value + 1), commit, nameof(p.riskLevel)));
            root.Add(CardReadOnlyText("es-node-card-command-purpose", "用途", p.purpose,
                p.purpose, nameof(p.purpose)));
            root.Add(CardReadOnlyText("es-node-card-command-boundary", "边界", p.allowedWriteScopes,
                p.allowedWriteScopes, nameof(p.allowedWriteScopes)));
            root.Add(CardReadOnlyText("es-node-card-command-acceptance", "完成", p.acceptanceCriteria,
                p.acceptanceCriteria, nameof(p.acceptanceCriteria)));
            root.Add(CardReadOnlyText("es-node-card-command-evidence", "证据", p.requiredEvidence,
                p.requiredEvidence, nameof(p.requiredEvidence)));
            root.Add(CardArtifactStatus("es-node-card-command-status", ESAgentArtifactKind.AICommand,
                p.operationMode, p.targetProjectPath));
            root.Add(CardArtifactActions(context, "es-node-card-command-actions", ESAgentArtifactKind.AICommand,
                () => p.targetProjectPath, () => p.SuggestedTargetProjectPath, () =>
                {
                    if (p.SynchronizeTargetProjectPath())
                        commit();
                }));
            return root;
        }
    }

    public sealed class ESAgentSkillOutputInspector : ESAgentPayloadInspector<ESAgentSkillOutputPayload>
    {
        private static readonly List<string> EffectLabels = new List<string>
        {
            "仅工作流指导", "只读操作", "受控修改"
        };
        private static readonly List<string> IdempotencyLabels = new List<string>
        {
            "必须幂等", "尽力幂等", "不适用"
        };

        public override ESGraphNodeTypeKey NodeType => ESAgentGraphStableIds.Node(ESAgentGraphStableIds.AISkillOutputNode);
        protected override VisualElement Build(ESAgentSkillOutputPayload p, Action commit)
        {
            var r = new VisualElement();
            r.Add(new HelpBox("填写技能要解决的工作，并说明什么时候使用、什么时候不要使用。技能目录和文件会在候选检查后才写入。",HelpBoxMessageType.Info));
            r.Add(FieldSummary(p, "AISkill 核心字段"));
            var a = Text("技能名称（英文小写）", p.skillName, fieldName: nameof(p.skillName));
            var b = Text("正式目录（系统）", p.targetProjectPath,
                fieldName: nameof(p.targetProjectPath));
            b.tooltip = b.tooltip + "\n正式技能目录，优先从下拉列表选择。";
            VisualElement picker = SearchPicker(
                "选择已有技能",
                "搜索 AISkill",
                "按技能目录名搜索已有 AISkill；选择后会同步目录和技能名称。",
                () => ESAgentAuthoringAssetCatalog.GetAgentSkillTargets(b.value, true),
                out Button pickerButton);
            pickerButton.clicked += () =>
            {
                ESSearchDropdown.Open(
                    pickerButton,
                        "选择已有 AISkill",
                    () => PathEntries(
                        ESAgentAuthoringAssetCatalog.GetAgentSkillTargets(b.value),
                        b.value,
                        selected =>
                        {
                            p.targetProjectPath = selected;
                            b.SetValueWithoutNotify(selected);
                            string folder = selected.TrimEnd('/').Split('/').Last();
                            p.skillName = folder;
                            a.SetValueWithoutNotify(folder);
                            commit();
                        }),
                    minimumWindowSize: new Vector2(540f, 360f));
            };
            VisualElement operation = OperationPicker(p.operationMode, value =>
            {
                p.operationMode = value;
                commit();
            });
            var effect = new PopupField<string>("效果类型", EffectLabels,
                Mathf.Clamp((int)p.effectKind, 0, EffectLabels.Count - 1));
            var idempotency = new PopupField<string>("幂等策略", IdempotencyLabels,
                Mathf.Clamp((int)p.idempotency, 0, IdempotencyLabels.Count - 1));
            StyleField(effect, effect.labelElement, nameof(p.effectKind), false);
            StyleField(idempotency, idempotency.labelElement, nameof(p.idempotency), false);
            var c = Text("能力说明", p.description, true, nameof(p.description));
            var d = Text("触发场景", p.triggerScenarios, true, nameof(p.triggerScenarios));
            var nonTrigger = Text("不触发场景", p.nonTriggerScenarios, true,
                nameof(p.nonTriggerScenarios));
            var preconditions = Text("使用前置条件", p.preconditions, true, nameof(p.preconditions));
            var dependencies = Text("必要依赖 / 工具", p.requiredDependencies, true,
                nameof(p.requiredDependencies));
            var inputContract = Text("输入契约", p.inputContract, true, nameof(p.inputContract));
            var e = Text("核心工作流程", p.workflow, true, nameof(p.workflow));
            var outputContract = Text("输出契约", p.outputContract, true, nameof(p.outputContract));
            var sideEffects = Text("允许的副作用", p.sideEffects, true, nameof(p.sideEffects));
            var f = Text("不负责的事项 / 禁止事项", p.nonGoals, true, nameof(p.nonGoals));
            var failureRecovery = Text("失败恢复", p.failureRecovery, true, nameof(p.failureRecovery));
            var g = Text("验证步骤", p.validationSteps, true, nameof(p.validationSteps));
            var permissionBoundary = Text("权限边界", p.permissionBoundary, true,
                nameof(p.permissionBoundary));
            var h = Text("默认使用提示", p.defaultPrompt, true, nameof(p.defaultPrompt));
            foreach (VisualElement v in new VisualElement[] { a, b, picker, operation, effect, idempotency,
                         c, d, nonTrigger, preconditions, dependencies, inputContract, e, outputContract,
                         sideEffects, f, failureRecovery, g, permissionBoundary, h }) r.Add(v);
            AddToggle(r,"生成技能入口配置（agents/openai.yaml）",()=>p.includeAgentsMetadata,
                v=>p.includeAgentsMetadata=v,commit, nameof(p.includeAgentsMetadata));
            AddToggle(r,"允许附带参考资料目录（references/）",()=>p.includeReferences,
                v=>p.includeReferences=v,commit, nameof(p.includeReferences));
            AddToggle(r,"允许附带脚本目录（scripts/）",()=>p.includeScripts,
                v=>p.includeScripts=v,commit, nameof(p.includeScripts));
            CommitOnFocusOut(a,x=>p.skillName=x,commit);CommitOnFocusOut(b,x=>p.targetProjectPath=x,commit);
            effect.RegisterValueChangedCallback(e=>{p.effectKind=(ESAgentSkillEffectKind)Math.Max(0,EffectLabels.IndexOf(e.newValue));commit();});
            idempotency.RegisterValueChangedCallback(e=>{p.idempotency=(ESAgentSkillIdempotency)Math.Max(0,IdempotencyLabels.IndexOf(e.newValue));commit();});
            CommitOnFocusOut(c,x=>p.description=x,commit);CommitOnFocusOut(d,x=>p.triggerScenarios=x,commit);CommitOnFocusOut(e,x=>p.workflow=x,commit);
            CommitOnFocusOut(nonTrigger,x=>p.nonTriggerScenarios=x,commit);CommitOnFocusOut(preconditions,x=>p.preconditions=x,commit);
            CommitOnFocusOut(dependencies,x=>p.requiredDependencies=x,commit);CommitOnFocusOut(inputContract,x=>p.inputContract=x,commit);
            CommitOnFocusOut(outputContract,x=>p.outputContract=x,commit);CommitOnFocusOut(sideEffects,x=>p.sideEffects=x,commit);
            CommitOnFocusOut(failureRecovery,x=>p.failureRecovery=x,commit);CommitOnFocusOut(permissionBoundary,x=>p.permissionBoundary=x,commit);
            CommitOnFocusOut(f,x=>p.nonGoals=x,commit);CommitOnFocusOut(g,x=>p.validationSteps=x,commit);CommitOnFocusOut(h,x=>p.defaultPrompt=x,commit);return r;
        }

        protected override VisualElement BuildCard(ESAgentSkillOutputPayload p, ESGraphNodeCardContext context,
            Action commit)
        {
            var root = new VisualElement();
            root.Add(FieldSummary(p, "产物合同"));
            root.Add(CardText(context, "es-node-card-skill-name", "技能", p.skillName,
                p.skillName, value => p.skillName = value, commit, nameof(p.skillName)));
            root.Add(CardText(context, "es-node-card-skill-path", "目录", p.targetProjectPath,
                p.targetProjectPath, value => p.targetProjectPath = value, commit,
                nameof(p.targetProjectPath)));
            root.Add(CardPopup(context, "es-node-card-skill-mode", "方式", CardOperationLabels,
                (int)p.operationMode, value => p.operationMode = (ESAgentArtifactOperationMode)value, commit,
                nameof(p.operationMode)));
            root.Add(CardPopup(context, "es-node-card-skill-effect", "效果", EffectLabels,
                (int)p.effectKind, value => p.effectKind = (ESAgentSkillEffectKind)value, commit,
                nameof(p.effectKind)));
            root.Add(CardPopup(context, "es-node-card-skill-idempotency", "幂等", IdempotencyLabels,
                (int)p.idempotency, value => p.idempotency = (ESAgentSkillIdempotency)value, commit,
                nameof(p.idempotency)));
            root.Add(CardReadOnlyText("es-node-card-skill-summary", "能力", p.description,
                string.IsNullOrWhiteSpace(p.description) ? "尚未填写能力说明。" : p.description,
                nameof(p.description)));
            root.Add(CardReadOnlyText("es-node-card-skill-trigger", "触发", p.triggerScenarios,
                p.triggerScenarios, nameof(p.triggerScenarios)));
            root.Add(CardReadOnlyText("es-node-card-skill-output", "输出", p.outputContract,
                p.outputContract, nameof(p.outputContract)));
            root.Add(CardReadOnlyText("es-node-card-skill-boundary", "权限", p.permissionBoundary,
                p.permissionBoundary, nameof(p.permissionBoundary)));
            root.Add(CardToggle(context, "es-node-card-skill-agents-metadata", "入口配置",
                p.includeAgentsMetadata, value => p.includeAgentsMetadata = value, commit,
                nameof(p.includeAgentsMetadata)));
            root.Add(CardToggle(context, "es-node-card-skill-references", "参考资料",
                p.includeReferences, value => p.includeReferences = value, commit,
                nameof(p.includeReferences)));
            root.Add(CardToggle(context, "es-node-card-skill-scripts", "辅助脚本",
                p.includeScripts, value => p.includeScripts = value, commit,
                nameof(p.includeScripts)));
            root.Add(CardReadOnlyText("es-node-card-skill-structure", "结构", p.IncludedContentSummary,
                p.IncludedContentSummary));
            root.Add(CardArtifactStatus("es-node-card-skill-status", ESAgentArtifactKind.AgentSkill,
                p.operationMode, p.targetProjectPath));
            root.Add(CardArtifactActions(context, "es-node-card-skill-actions", ESAgentArtifactKind.AgentSkill,
                () => p.targetProjectPath, () => p.SuggestedTargetProjectPath, () =>
                {
                    if (p.SynchronizeTargetProjectPath())
                        commit();
                }, () => p.InvocationToken));
            return root;
        }

        private static void AddToggle(VisualElement root,string label,Func<bool> get,Action<bool> set,
            Action commit, string fieldName)
        {
            var t = new Toggle(label) { value = get() };
            root.Add(t);
            t.tooltip = "打开后会把这一部分内容纳入候选产物；正式写入仍需要人工批准。";
            StyleField(t, t.labelElement, fieldName, !t.value);
            t.RegisterValueChangedCallback(e => { set(e.newValue); commit(); });
        }
    }

    public sealed class ESAgentValidationPayloadInspector : ESAgentPayloadInspector<ESAgentValidationPayload>
    {
        public override ESGraphNodeTypeKey NodeType => ESAgentGraphStableIds.Node(ESAgentGraphStableIds.ValidationNode);
        protected override VisualElement Build(ESAgentValidationPayload p, Action commit)
        {
            var r = new VisualElement();
            r.Add(new HelpBox("这里决定候选文件需要经过哪些检查。候选差异查看和人工批准始终开启。",
                HelpBoxMessageType.Info));
            r.Add(FieldSummary(p, "交付门禁"));
            AddToggle(r, "检查 AICommand 命令格式", () => p.validateAICommand,
                v => p.validateAICommand = v, commit, nameof(p.validateAICommand));
            AddToggle(r, "检查 Agent Skill 技能结构", () => p.validateAgentSkill,
                v => p.validateAgentSkill = v, commit, nameof(p.validateAgentSkill));
            AddToggle(r, "检查中文编码（严格 UTF-8）", () => p.validateUtf8,
                v => p.validateUtf8 = v, commit, nameof(p.validateUtf8));
            AddToggle(r, "必须查看候选差异", () => p.requireDiffReview,
                v => p.requireDiffReview = v, commit, nameof(p.requireDiffReview));
            AddToggle(r, "必须人工批准", () => p.requireHumanApproval,
                v => p.requireHumanApproval = v, commit, nameof(p.requireHumanApproval));
            var t = Text("其他验收要求", p.additionalRequirements, true,
                nameof(p.additionalRequirements));
            var c = Text("人工检查清单", p.reviewChecklist, true, nameof(p.reviewChecklist));
            r.Add(t);
            r.Add(c);
            CommitOnFocusOut(t, x => p.additionalRequirements = x, commit);
            CommitOnFocusOut(c, x => p.reviewChecklist = x, commit);
            return r;
        }

        protected override VisualElement BuildCard(ESAgentValidationPayload p,
            ESGraphNodeCardContext context, Action commit)
        {
            var root = new VisualElement();
            root.Add(FieldSummary(p, "交付门禁"));
            root.Add(CardToggle(context, "es-node-card-validation-command", "检查 AICommand", p.validateAICommand,
                value => p.validateAICommand = value, commit, nameof(p.validateAICommand)));
            root.Add(CardToggle(context, "es-node-card-validation-skill", "检查 Agent Skill", p.validateAgentSkill,
                value => p.validateAgentSkill = value, commit, nameof(p.validateAgentSkill)));
            root.Add(CardToggle(context, "es-node-card-validation-utf8", "严格 UTF-8", p.validateUtf8,
                value => p.validateUtf8 = value, commit, nameof(p.validateUtf8)));
            root.Add(CardToggle(context, "es-node-card-validation-diff", "查看差异", p.requireDiffReview,
                value => p.requireDiffReview = value, commit, nameof(p.requireDiffReview)));
            root.Add(CardToggle(context, "es-node-card-validation-approval", "人工批准", p.requireHumanApproval,
                value => p.requireHumanApproval = value, commit, nameof(p.requireHumanApproval)));
            root.Add(CardReadOnlyText("es-node-card-validation-checklist", "清单", p.reviewChecklist,
                p.reviewChecklist, nameof(p.reviewChecklist)));
            return root;
        }

        private static void AddToggle(VisualElement root, string label, Func<bool> get, Action<bool> set,
            Action commit, string fieldName)
        {
            var t = new Toggle(label) { value = get() };
            root.Add(t);
            StyleField(t, t.labelElement, fieldName, !t.value);
            t.RegisterValueChangedCallback(e => { set(e.newValue); commit(); });
        }
    }
}
