using System;
using System.Collections.Generic;

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
}

