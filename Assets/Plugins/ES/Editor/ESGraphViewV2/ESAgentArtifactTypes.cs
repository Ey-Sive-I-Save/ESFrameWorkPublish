using System;

namespace ES.EditorInternal
{
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
}

