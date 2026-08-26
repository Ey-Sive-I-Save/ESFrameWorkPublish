using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;
using GraphAsset = global::ES.ESGraphAssetBase;

namespace ES.EditorInternal
{
    public static class ESAgentAuthoringGraphPreset
    {
        public const string DefaultGraphFolder = "Assets/ESNormalAssets/Editor/AgentAuthoring/Graphs";

        [MenuItem(MenuItemPathDefine.ASSET_CREATE_CONTENT_CONTEXT_PATH + "图与流程/智能助手编排/Skill 能力包（AICommand + AISkill）", false, 201)]
        public static void CreatePairedFromAssetsMenu() { TryCreateAsset(ESAgentAuthoringPresetKind.Paired, out _, out _); }
        [MenuItem(MenuItemPathDefine.ASSET_CREATE_CONTENT_CONTEXT_PATH + "图与流程/智能助手编排/AICommand 实现链", false, 202)]
        public static void CreateAICommandFromAssetsMenu() { TryCreateAsset(ESAgentAuthoringPresetKind.AICommandOnly, out _, out _); }
        [MenuItem(MenuItemPathDefine.ASSET_CREATE_CONTENT_CONTEXT_PATH + "图与流程/智能助手编排/AISkill 能力链", false, 203)]
        public static void CreateAgentSkillFromAssetsMenu() { TryCreateAsset(ESAgentAuthoringPresetKind.AgentSkillOnly, out _, out _); }
        [MenuItem(MenuItemPathDefine.ASSET_CREATE_CONTENT_CONTEXT_PATH + "图与流程/智能助手编排/AI 实战调度图（分支与遍历）", false, 204)]
        public static void CreateMindMapFromAssetsMenu() { TryCreateAsset(ESAgentAuthoringPresetKind.MindMapPaired, out _, out _); }
        [MenuItem(MenuItemPathDefine.ASSET_CREATE_CONTENT_CONTEXT_PATH + "图与流程/智能助手编排/AISkill 执行模板（场景扫描与人工审查）", false, 205)]
        public static void CreateSceneScanReviewFromAssetsMenu() { TryCreateAsset(ESAgentAuthoringPresetKind.SceneScanReview, out _, out _); }
        [MenuItem(MenuItemPathDefine.ASSET_CREATE_CONTENT_CONTEXT_PATH + "图与流程/智能助手编排/AISkill 执行模板（场景质量双重审查）", false, 206)]
        public static void CreateSceneQualityReviewFromAssetsMenu() { TryCreateAsset(ESAgentAuthoringPresetKind.SceneQualityReview, out _, out _); }

        public static bool TryCreateAsset(out GraphAsset asset, out string error)
        {
            return TryCreateAsset(ESAgentAuthoringPresetKind.Paired, out asset, out error);
        }

        public static bool TryCreateAsset(ESAgentAuthoringPresetKind kind, out GraphAsset asset, out string error)
        {
            asset = null;
            error = string.Empty;
            EnsureAssetFolder(DefaultGraphFolder);
            string path = EditorUtility.SaveFilePanelInProject("创建 Agent Authoring Graph",
                GetDefaultAssetName(kind), "asset", GetCreateDescription(kind), DefaultGraphFolder);
            if (string.IsNullOrEmpty(path))
                return false;
            try
            {
                if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) != null
                    || File.Exists(Path.GetFullPath(path)))
                    throw new InvalidOperationException("目标 Agent Authoring Graph 已存在；请选择新的路径。");
                asset = ScriptableObject.CreateInstance<ESAgentAuthoringGraphAsset>();
                Populate(asset, kind);
                List<ESGraphValidationIssue> templateIssues = ESGraphAuthoringRegistry.Validate(asset);
                ESGraphValidationIssue[] templateErrors = templateIssues
                    .Where(issue => issue != null && issue.severity == ESGraphValidationSeverity.Error)
                    .Take(5).ToArray();
                if (templateErrors.Length > 0)
                    throw new InvalidOperationException("内置 AI 协作模板未通过统一 Graph 校验：\n"
                        + string.Join("\n", templateErrors.Select(issue => issue.code + "：" + issue.message)));
                EnsureTemplateCanBake(asset, kind);
                AssetDatabase.CreateAsset(asset, path);
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssetIfDirty(asset);
                Selection.activeObject = asset;
                ESStableGraphViewWindow.ShowWindow().OpenGraph(asset);
                return true;
            }
            catch (Exception exception)
            {
                if (asset != null && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(asset)))
                    UnityEngine.Object.DestroyImmediate(asset);
                asset = null;
                error = "创建 Agent Authoring 预设失败：" + exception.Message;
                Debug.LogException(exception);
                return false;
            }
        }

        public static void Populate(GraphAsset asset)
        {
            Populate(asset, ESAgentAuthoringPresetKind.Paired);
        }

        public static void Populate(GraphAsset asset, ESAgentAuthoringPresetKind kind)
        {
            if (asset == null || !string.Equals(asset.DomainId, ESAgentGraphStableIds.DomainId,
                    StringComparison.Ordinal))
                throw new ArgumentException("必须提供空的 Agent Authoring Graph。", nameof(asset));
            if (asset.Nodes.Count > 0 || asset.Edges.Count > 0)
                throw new InvalidOperationException("Agent Authoring 预设只能填充空 Graph。");
            if (kind == ESAgentAuthoringPresetKind.MindMapPaired)
            {
                PopulateMindMap(asset);
                return;
            }
            if (kind == ESAgentAuthoringPresetKind.SceneScanReview)
            {
                PopulateSceneScanReview(asset);
                return;
            }
            if (kind == ESAgentAuthoringPresetKind.SceneQualityReview)
            {
                PopulateSceneQualityReview(asset);
                return;
            }
            ESGraphNodeRecord goal = Add(asset, ESAgentGraphStableIds.GoalNode, new Vector2(0f, 240f));
            ESGraphNodeRecord reference = Add(asset, ESAgentGraphStableIds.ReferenceNode, new Vector2(250f, 240f));
            ESGraphNodeRecord branch = Add(asset, ESAgentGraphStableIds.BranchNode, new Vector2(500f, 240f));
            ESGraphNodeRecord matchedConstraint = Add(asset, ESAgentGraphStableIds.ConstraintNode,
                new Vector2(770f, 0f));
            ESGraphNodeRecord defaultConstraint = Add(asset, ESAgentGraphStableIds.ConstraintNode,
                new Vector2(770f, 240f));
            ESGraphNodeRecord failureConstraint = Add(asset, ESAgentGraphStableIds.ConstraintNode,
                new Vector2(770f, 480f));
            ESGraphNodeRecord command = kind != ESAgentAuthoringPresetKind.AgentSkillOnly
                ? Add(asset, ESAgentGraphStableIds.AICommandOutputNode,
                    new Vector2(1060f, kind == ESAgentAuthoringPresetKind.Paired ? 80f : 240f)) : null;
            ESGraphNodeRecord skill = kind != ESAgentAuthoringPresetKind.AICommandOnly
                ? Add(asset, ESAgentGraphStableIds.AISkillOutputNode,
                    new Vector2(1060f, kind == ESAgentAuthoringPresetKind.Paired ? 400f : 240f)) : null;
            ESGraphNodeRecord validation = Add(asset, ESAgentGraphStableIds.ValidationNode,
                new Vector2(1360f, 240f));
            asset.UpdateNode(goal.nodeId, goal.typeId, goal.version, goal.title, JsonUtility.ToJson(new ESAgentGoalPayload
            {
                title = GetGoalTitle(kind),
                objective = GetGoalObjective(kind),
                successCriteria = GetGoalSuccessCriteria(kind),
                context = kind == ESAgentAuthoringPresetKind.AICommandOnly
                    ? "把中文需求整理成一条可执行的实现链：读取权威资料、核对现状、按权限修改目标、运行验证并交付真实证据。"
                    : "先产出隔离候选，通过 Diff Review 后再导入正式目录。"
            }), out _);
            ConfigureBasicGenerationDecision(asset, kind, reference, branch,
                matchedConstraint, defaultConstraint, failureConstraint);
            if (command != null) asset.UpdateNode(command.nodeId, command.typeId, command.version, command.title, JsonUtility.ToJson(new ESAgentAICommandOutputPayload
            {
                commandName = "生成_新模块工作流_AI命令",
                targetProjectPath = "Assets/Plugins/ES/AICommands/生成_新模块工作流_AI命令.md",
                commandIntent = ESAgentCommandIntent.ControlledExecution,
                writeAuthorization = ESAgentWriteAuthorization.ScopedWrites,
                riskLevel = ESAgentRiskLevel.L2,
                failurePolicy = ESAgentFailurePolicy.RollbackAndReport,
                purpose = kind == ESAgentAuthoringPresetKind.AICommandOnly
                    ? "把模块编排需求转换为 AI 可以真正执行的 ESFramework 实现合同。"
                    : "为模块编排生成一个有明确权限、必读规则、执行步骤和交付格式的任务合同。",
                expectedInputs = "用户目标、当前实现事实、必读规则、允许修改路径、验收标准。",
                preconditions = "目标范围、权威规则和允许修改路径已明确；并行工作树已完成只读核对。",
                allowedWriteScopes = "只允许修改用户目标和 Graph Constraint 明确列出的项目文件。",
                forbiddenOperations = "不得扩大用户授权；不得擅自删除、提交 Git、发布、上传或修改无关并行分支。",
                executionOutline = "读取权威规则\n核对分支、HEAD 与工作树\n按实现链修改目标文件\n运行相关编译和测试\n交付真实证据与剩余风险",
                acceptanceCriteria = "不得只输出建议；必须完成授权范围内的真实实现，并逐项报告改动、验证和未完成项。",
                requiredEvidence = "源码差异、目标工程编译、适用测试及未执行的 Unity 运行验证必须分层报告。",
                blockedHandling = "遇到越权、依赖缺失或并行冲突时停止相关写入，报告阻断与所需决策。",
                rollbackStrategy = "本轮写入失败时只回滚本事务产生的改动，不撤销用户或其他 AI 的并行修改。",
                requiredSections = "必须先读\n执行边界\n实现步骤\n验证结果\n改动文件\n剩余风险"
            }), out _);
            if (skill != null) asset.UpdateNode(skill.nodeId, skill.typeId, skill.version, skill.title, JsonUtility.ToJson(new ESAgentSkillOutputPayload
            {
                skillName = "es-new-module-workflow", targetProjectPath = ".agents/skills/es-new-module-workflow/",
                description = "Execute a repeatable ESFramework module workflow with live rule reading, scoped writes, validation, and evidence-backed delivery.",
                effectKind = ESAgentSkillEffectKind.ControlledMutation,
                idempotency = ESAgentSkillIdempotency.BestEffort,
                triggerScenarios = "需要按 ESFramework 项目规则执行边界明确的模块编排任务时使用。",
                nonTriggerScenarios = "纯解释、无项目上下文或用户未授权写入时不自动执行修改阶段。",
                preconditions = "项目规则可读取，用户目标和授权范围明确，目标工作树可安全核对。",
                requiredDependencies = "项目 AIWarnings、适用的项目 Skill、工作树审计和目标模块源码。",
                inputContract = "用户目标、允许修改范围、目标模块路径和验收层级。",
                workflow = "读取 Graph References\n核对工作树与模块事实\n执行受控修改\n运行验证\n交付证据与剩余风险",
                outputContract = "实际改动、文件清单、分层验证证据、未完成项和剩余风险。",
                sideEffects = "只在当前用户或 AICommand 已授权的项目范围内修改文件和运行验证。",
                failureRecovery = "停止后续副作用，仅恢复本次可安全撤销的修改并保留诊断证据。",
                permissionBoundary = "Skill 只定义工作流，不扩大用户或 AICommand 的权限。",
                defaultPrompt = "Use $es-new-module-workflow to implement the scoped ESFramework module task."
            }), out _);
            asset.UpdateNode(validation.nodeId, validation.typeId, validation.version, validation.title,
                JsonUtility.ToJson(new ESAgentValidationPayload
                {
                    validateAICommand = command != null,
                    validateAgentSkill = skill != null,
                    validateUtf8 = true,
                    requireDiffReview = true,
                    requireHumanApproval = true,
                    additionalRequirements = "不得包含 U+FFFD；不得越过候选目录。"
                }), out _);
            Connect(asset, goal, ESAgentGraphStableIds.ContextOutputPortKey,
                reference, ESGraphBuiltInPortKeys.Input);
            Connect(asset, reference, ESAgentGraphStableIds.ContextOutputPortKey,
                branch, ESGraphBuiltInPortKeys.Input);
            Connect(asset, branch, ESAgentGraphStableIds.BranchMatchedPortKey,
                matchedConstraint, ESGraphBuiltInPortKeys.Input);
            Connect(asset, branch, ESAgentGraphStableIds.BranchDefaultPortKey,
                defaultConstraint, ESGraphBuiltInPortKeys.Input);
            Connect(asset, branch, ESAgentGraphStableIds.BranchFailurePortKey,
                failureConstraint, ESGraphBuiltInPortKeys.Input);
            foreach (ESGraphNodeRecord constraint in new[]
                     {
                         matchedConstraint, defaultConstraint, failureConstraint
                     })
            {
                if (command != null)
                    Connect(asset, constraint, ESAgentGraphStableIds.RequirementOutputPortKey,
                        command, ESGraphBuiltInPortKeys.Input);
                if (skill != null)
                    Connect(asset, constraint, ESAgentGraphStableIds.RequirementOutputPortKey,
                        skill, ESGraphBuiltInPortKeys.Input);
            }
            if (command != null)
            {
                Connect(asset, command, ESAgentGraphStableIds.ArtifactOutputPortKey,
                    validation, ESGraphBuiltInPortKeys.Input);
            }
            if (skill != null)
            {
                Connect(asset, skill, ESAgentGraphStableIds.ArtifactOutputPortKey,
                    validation, ESGraphBuiltInPortKeys.Input);
            }
        }

        private static void ConfigureBasicGenerationDecision(GraphAsset asset,
            ESAgentAuthoringPresetKind kind, ESGraphNodeRecord reference, ESGraphNodeRecord branch,
            ESGraphNodeRecord matchedConstraint, ESGraphNodeRecord defaultConstraint,
            ESGraphNodeRecord failureConstraint)
        {
            asset.UpdateNode(reference.nodeId, reference.typeId, reference.version, "规则与任务上下文",
                JsonUtility.ToJson(new ESAgentReferencePayload
                {
                    referenceKind = ESAgentReferenceKind.AIWarning,
                    projectPath = "Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md",
                    purpose = "为当前目标选择必须读取的 P0、领域规则和验证边界。",
                    required = true
                }), out _);

            ESAgentBranchPayload branchPayload;
            if (kind == ESAgentAuthoringPresetKind.AICommandOnly)
            {
                branchPayload = new ESAgentBranchPayload
                {
                    condition = "任务是否包含用户已明确授权的项目写入？",
                    matchedPath = "生成可执行的受控修改合同，并要求真实改动与验证证据。",
                    defaultPath = "保持只读分析合同，不把建议或诊断伪装为已完成修改。",
                    failurePath = "无法确认授权或目标范围时停止副作用并请求用户补充。"
                };
                ConfigureConstraint(asset, matchedConstraint, ESAgentConstraintKind.Permission,
                    "只允许修改用户目标和 Graph 明确列出的项目路径。",
                    "把可执行修改限制在当前授权内。", "交付逐项列出实际改动文件和对应验证。");
                ConfigureConstraint(asset, defaultConstraint, ESAgentConstraintKind.Quality,
                    "只读任务必须给出事实、证据缺口和下一步，不得声称已经写入或验收。",
                    "让同一 AICommand 能代表无写入任务的可靠交付。", "改动文件明确为无，未执行验证明确标记。");
                ConfigureConstraint(asset, failureConstraint, ESAgentConstraintKind.Forbidden,
                    "授权、范围或权威规则不明确时禁止猜测写入、Git、删除或发布。",
                    "不确定性不能自动扩大权限。", "阻断报告包含缺失输入、已完成检查和所需决策。");
            }
            else if (kind == ESAgentAuthoringPresetKind.AgentSkillOnly)
            {
                branchPayload = new ESAgentBranchPayload
                {
                    condition = "当前流程是否具备可重复触发、稳定输入输出和明确副作用边界？",
                    matchedPath = "固化为可复用 AISkill 工作流，并保持步骤与验证可重复。",
                    defaultPath = "记录非触发场景和一次性处理边界，避免把偶发任务强行 Skill 化。",
                    failurePath = "依赖、权限或恢复策略不完整时拒绝生成可执行 Skill。"
                };
                ConfigureConstraint(asset, matchedConstraint, ESAgentConstraintKind.Required,
                    "Skill 必须声明触发条件、输入合同、输出合同和可重复的验证步骤。",
                    "可发现不等于可安全复用。", "SKILL.md 的触发、工作流和交付字段可逐项核对。");
                ConfigureConstraint(asset, defaultConstraint, ESAgentConstraintKind.Quality,
                    "必须声明非触发场景、非目标和人工决策点，避免所有请求都误触发。",
                    "反向边界决定 Skill 是否具有代表性。", "典型触发与非触发样例均能得到确定结果。");
                ConfigureConstraint(asset, failureConstraint, ESAgentConstraintKind.Forbidden,
                    "禁止让 Skill 自行扩大写入、Git、删除、发布或外部通信权限。",
                    "Skill 只提供工作流，权限仍来自用户或 AICommand。", "失败恢复和权限边界在候选中保持明确。");
            }
            else
            {
                branchPayload = new ESAgentBranchPayload
                {
                    condition = "当前需求应由单次任务合同执行，还是沉淀为可复用能力？",
                    matchedPath = "把本次授权、实现步骤和验收证据交给 AICommand 产物。",
                    defaultPath = "把稳定触发、输入输出和恢复流程交给 AISkill 产物。",
                    failurePath = "两类产物共享失败关闭与人工批准边界，禁止互相扩大权限。"
                };
                ConfigureConstraint(asset, matchedConstraint, ESAgentConstraintKind.Required,
                    "AICommand 必须保存本次任务的目标、授权范围、实现步骤和验收证据。",
                    "单次执行合同负责当前任务，不承担长期能力发现。", "AICommand 候选包含本次任务完整交付合同。");
                ConfigureConstraint(asset, defaultConstraint, ESAgentConstraintKind.Required,
                    "AISkill 必须保存可重复触发、稳定输入输出、工作流和失败恢复。",
                    "可复用能力不能依赖某次会话的隐含上下文。", "SKILL.md 候选可在新会话按相同输入复用。");
                ConfigureConstraint(asset, failureConstraint, ESAgentConstraintKind.Forbidden,
                    "任一产物都不得自行扩大用户授权；候选必须保持隔离并经过 Diff Review。",
                    "配套生成不代表权限可以在两种产物之间传递。", "两个候选均保留人工批准和失败关闭边界。");
            }
            string branchTitle = kind == ESAgentAuthoringPresetKind.AICommandOnly
                ? "分类执行授权"
                : kind == ESAgentAuthoringPresetKind.AgentSkillOnly
                    ? "判断复用边界"
                    : "分配任务与能力职责";
            asset.UpdateNode(branch.nodeId, branch.typeId, branch.version, branchTitle,
                JsonUtility.ToJson(branchPayload), out _);
        }

        private static ESGraphNodeRecord Add(GraphAsset graph, string nodeTypeId, Vector2 position)
        {
            ESGraphNodeTypeKey nodeType = ESAgentGraphStableIds.Node(nodeTypeId);
            if (!ESGraphAuthoringRegistry.TryGetNodeDefinition(graph.DomainKey, nodeType,
                    out IESGraphNodeDefinition definition))
                throw new InvalidOperationException("未注册预设节点定义：" + nodeType.StableId);
            ESGraphNodeRecord node = graph.AddNode(definition.NodeType, definition.DisplayName, position, definition.Ports);
            graph.UpdateNode(node.nodeId, definition.NodeType, definition.CurrentVersion, node.title,
                definition.CreateDefaultPayload(), out _);
            return node;
        }

        private static void Connect(GraphAsset graph, ESGraphNodeRecord from,
            string outputPortKey, ESGraphNodeRecord to, string inputPortKey)
        {
            if (from == null || !from.TryGetPort(outputPortKey, out ESGraphPortRecord output)
                || output.direction != ESGraphPortDirection.Output)
                throw new InvalidOperationException("源节点缺少指定输出端点：" + outputPortKey);
            if (to == null || !to.TryGetPort(inputPortKey, out ESGraphPortRecord input)
                || input.direction != ESGraphPortDirection.Input)
                throw new InvalidOperationException("目标节点缺少指定输入端点：" + inputPortKey);
            if (!graph.TryAddEdge(output.portId, input.portId, out _, out string error))
                throw new InvalidOperationException(error);
        }

        internal static void EnsureTemplateCanBake(GraphAsset asset,
            ESAgentAuthoringPresetKind kind)
        {
            if (!ESGraphAuthoringRegistry.TryBake(asset, out ESBakedGraphSnapshot snapshot,
                    out IESBakedGraphPlan plan, out List<ESGraphValidationIssue> issues)
                || snapshot == null || plan == null)
            {
                string detail = string.Join("\n", (issues ?? new List<ESGraphValidationIssue>())
                    .Where(issue => issue != null)
                    .Take(5)
                    .Select(issue => issue.code + "：" + issue.message));
                throw new InvalidOperationException("内置 AI 协作模板无法生成可消费的 Bake 合同。"
                    + (string.IsNullOrWhiteSpace(detail) ? string.Empty : "\n" + detail));
            }

            bool executionTemplate = kind == ESAgentAuthoringPresetKind.SceneScanReview
                || kind == ESAgentAuthoringPresetKind.SceneQualityReview;
            if (executionTemplate != (plan is ESAISkillExecutionSpec))
                throw new InvalidOperationException(executionTemplate
                    ? "AISkill 执行模板没有生成 ESAISkillExecutionSpec。"
                    : "产物生成模板错误生成了 AISkill 执行合同。");
            if (!executionTemplate && !(plan is ESAgentArtifactGenerationSpec))
                throw new InvalidOperationException("产物生成模板没有生成 ESAgentArtifactGenerationSpec。");

            EnsureRepresentativeOutcomeContract(asset, executionTemplate);
        }

        private static void EnsureRepresentativeOutcomeContract(GraphAsset asset,
            bool executionTemplate)
        {
            string ownerTypeId = executionTemplate
                ? ESAgentGraphStableIds.SkillTaskNode
                : ESAgentGraphStableIds.BranchNode;
            string[] outcomePortKeys = executionTemplate
                ? new[]
                {
                    ESAgentGraphStableIds.SkillSuccessPortKey,
                    ESAgentGraphStableIds.SkillFailurePortKey,
                    ESAgentGraphStableIds.SkillTimeoutPortKey,
                    ESAgentGraphStableIds.SkillCancelledPortKey
                }
                : new[]
                {
                    ESAgentGraphStableIds.BranchMatchedPortKey,
                    ESAgentGraphStableIds.BranchDefaultPortKey,
                    ESAgentGraphStableIds.BranchFailurePortKey
                };
            ESGraphNodeRecord owner = asset.Nodes.SingleOrDefault(node =>
                string.Equals(node.typeId, ownerTypeId, StringComparison.Ordinal));
            if (owner == null)
                throw new InvalidOperationException("内置模板缺少代表业务结果的节点：" + ownerTypeId);

            var endpointIds = new HashSet<string>(StringComparer.Ordinal);
            var targetNodeIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (string portKey in outcomePortKeys)
            {
                if (!owner.TryGetPort(portKey, out ESGraphPortRecord port)
                    || port.direction != ESGraphPortDirection.Output
                    || !endpointIds.Add(port.portId))
                    throw new InvalidOperationException("内置模板缺少独立稳定业务结果端点：" + portKey);
                ESGraphEdgeRecord[] routes = asset.Edges.Where(edge =>
                    string.Equals(edge.outputPortId, port.portId, StringComparison.Ordinal)).ToArray();
                if (routes.Length != 1 || !asset.TryFindPort(routes[0].inputPortId,
                        out ESGraphNodeRecord target, out _))
                    throw new InvalidOperationException("内置模板业务结果必须且只能路由到一个明确目标：" + portKey);
                if (!targetNodeIds.Add(target.nodeId))
                    throw new InvalidOperationException("内置模板不能把不同业务结果合并到同一目标：" + portKey);
            }
        }

        private static void PopulateMindMap(GraphAsset asset)
        {
            ESGraphNodeRecord goal = Add(asset, ESAgentGraphStableIds.GoalNode, new Vector2(0f, 240f));
            ESGraphNodeRecord rules = Add(asset, ESAgentGraphStableIds.ReferenceNode, new Vector2(240f, 160f));
            ESGraphNodeRecord contract = Add(asset, ESAgentGraphStableIds.ReferenceNode, new Vector2(480f, 160f));
            ESGraphNodeRecord branch = Add(asset, ESAgentGraphStableIds.BranchNode, new Vector2(720f, 180f));
            ESGraphNodeRecord traversal = Add(asset, ESAgentGraphStableIds.TraverseNode, new Vector2(940f, 0f));
            ESGraphNodeRecord required = Add(asset, ESAgentGraphStableIds.ConstraintNode, new Vector2(1200f, 0f));
            ESGraphNodeRecord forbidden = Add(asset, ESAgentGraphStableIds.ConstraintNode, new Vector2(1200f, 160f));
            ESGraphNodeRecord permission = Add(asset, ESAgentGraphStableIds.ConstraintNode, new Vector2(1200f, 320f));
            ESGraphNodeRecord quality = Add(asset, ESAgentGraphStableIds.ConstraintNode, new Vector2(1200f, 480f));
            ESGraphNodeRecord command = Add(asset, ESAgentGraphStableIds.AICommandOutputNode, new Vector2(1500f, 120f));
            ESGraphNodeRecord skill = Add(asset, ESAgentGraphStableIds.AISkillOutputNode, new Vector2(1500f, 360f));
            ESGraphNodeRecord validation = Add(asset, ESAgentGraphStableIds.ValidationNode, new Vector2(1800f, 240f));

            asset.UpdateNode(goal.nodeId, goal.typeId, goal.version, goal.title, JsonUtility.ToJson(new ESAgentGoalPayload
            {
                title = "完整 Agent Artifact 需求思路图",
                objective = "把用户目标、权威上下文、权限边界、质量门禁和最终 AICommand/Agent Skill 产物组织为可审查的生成合同。",
                context = "所有分支最终汇合到候选产物与人工批准，不形成玩法运行时。",
                targetUsers = "需要通过 Graph 编排复杂 AI 协作任务的 ESFramework 开发者。",
                successCriteria = "AI 能按关系图理解需求归属，生成结构清晰且可通过 Diff Review 的配套候选。"
            }), out _);
            asset.UpdateNode(branch.nodeId, branch.typeId, branch.version, branch.title,
                JsonUtility.ToJson(new ESAgentBranchPayload
                {
                    condition = "当 Graph 声明了多个 References 或 Constraints 时，必须进入逐项检查分支。",
                    matchedPath = "进入有界遍历，逐项读取并记录对产物的影响。",
                    defaultPath = "按单一上下文继续执行，但仍保留授权和批准门禁。",
                    failurePath = "停止生成，报告无法判断分支所缺少的 Graph 上下文。"
                }), out _);
            asset.UpdateNode(traversal.nodeId, traversal.typeId, traversal.version, traversal.title,
                JsonUtility.ToJson(new ESAgentTraversePayload
                {
                    target = "与当前 Output 相连的 References 和 Constraints",
                    itemAlias = "需求项",
                    order = ESAgentTraversalOrder.DependencyFirst,
                    maxDepth = 8,
                    maxItems = 128,
                    stopCondition = "达到硬上限、发现越权路径或继续处理会形成循环。",
                    emptyResultAction = "记录没有可处理需求项并进入完成出口。",
                    failureAction = "停止后续写入，保留已检查项并进入失败出口。"
                }), out _);
            asset.UpdateNode(rules.nodeId, rules.typeId, rules.version, "项目规则入口", JsonUtility.ToJson(new ESAgentReferencePayload
            {
                referenceKind = ESAgentReferenceKind.AIWarning,
                projectPath = "Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md",
                purpose = "根据任务领域选择必须读取的 P0 与专项规则",
                required = true
            }), out _);
            asset.UpdateNode(contract.nodeId, contract.typeId, contract.version, "候选生成合同", JsonUtility.ToJson(new ESAgentReferencePayload
            {
                referenceKind = ESAgentReferenceKind.AgentSkill,
                projectPath = ".agents/skills/es-generate-agent-artifacts/SKILL.md",
                purpose = "候选隔离、Manifest、验证与人工批准工作流",
                required = true
            }), out _);
            ConfigureConstraint(asset, required, ESAgentConstraintKind.Required,
                "必须把 Graph 中每条关系、引用、规则、输出和 Validation 写入生成请求。",
                "防止 AI 只读取扁平字段而丢失思路结构。", "generation-request.json 与 Prompt 包含 relations 和 Mermaid 思路图。");
            ConfigureConstraint(asset, forbidden, ESAgentConstraintKind.Forbidden,
                "禁止直接写正式 AICommands、.agents/skills、运行时源码或生成的 .csproj。",
                "Graph 只授权生成候选。", "所有输出路径都位于 request/candidate，正式导入只由人工批准触发。");
            ConfigureConstraint(asset, permission, ESAgentConstraintKind.Permission,
                "只允许读取 Graph 声明的 References，并在候选目录创建声明的 OutputArtifact。",
                "把 AI 权限限制在用户可见的图结构内。", "Manifest 中每个目标都能映射到 GenerationSpec.outputs。");
            ConfigureConstraint(asset, quality, ESAgentConstraintKind.Quality,
                "结果必须清晰、完整、严格 UTF-8，并明确已执行与未执行的验证。",
                "让候选可以被人和后续 AI 可靠复核。", "UTF-8 Guard、结构验证、Diff Review 清单全部可见。");

            asset.UpdateNode(command.nodeId, command.typeId, command.version, command.title,
                JsonUtility.ToJson(new ESAgentAICommandOutputPayload
                {
                    commandName = "生成_需求思路图_AI命令",
                    targetProjectPath = "Assets/Plugins/ES/AICommands/生成_需求思路图_AI命令.md",
                    commandIntent = ESAgentCommandIntent.ControlledExecution,
                    writeAuthorization = ESAgentWriteAuthorization.ScopedWrites,
                    riskLevel = ESAgentRiskLevel.L2,
                    failurePolicy = ESAgentFailurePolicy.RollbackAndReport,
                    purpose = "把完整需求思路图转换为权限、执行与验收边界明确的 AICommand。",
                    expectedInputs = "用户目标、Graph References、Constraints、允许修改范围和验收层级。",
                    preconditions = "需求关系可遍历，权威引用可读取，授权范围和停止条件已明确。",
                    allowedWriteScopes = "只允许修改 Graph Constraint 明确授权的项目路径。",
                    forbiddenOperations = "不得越过 Graph 权限关系执行 Git、删除、发布或无关项目写入。",
                    executionOutline = "读取目标与引用\n遍历约束关系\n执行受控步骤\n运行分层验证\n交付关系映射和证据",
                    acceptanceCriteria = "AICommand 必须覆盖需求思路图中的目标、约束、引用和验证关系。",
                    requiredEvidence = "每个已执行步骤、改动文件和验证结果都能回溯到对应 Graph 关系。",
                    blockedHandling = "关系缺失、形成循环或授权不明确时停止副作用并报告缺失输入。",
                    rollbackStrategy = "只撤销本次事务产生且可安全恢复的改动，不覆盖并行工作。",
                    requiredSections = "必须先读\n关系映射\n权限边界\n执行步骤\n验证证据\n剩余风险"
                }), out _);
            asset.UpdateNode(skill.nodeId, skill.typeId, skill.version, skill.title,
                JsonUtility.ToJson(new ESAgentSkillOutputPayload
                {
                    skillName = "es-requirement-mind-map-workflow",
                    targetProjectPath = ".agents/skills/es-requirement-mind-map-workflow/",
                    description = "Use the complete 需求思路图 to execute a repeatable ESFramework artifact workflow.",
                    effectKind = ESAgentSkillEffectKind.ControlledMutation,
                    idempotency = ESAgentSkillIdempotency.BestEffort,
                    triggerScenarios = "需要按需求思路图复用复杂 Agent Artifact 编排流程时使用。",
                    nonTriggerScenarios = "单一事实查询、无 Graph 上下文或关系尚未获得用户确认时不触发修改阶段。",
                    preconditions = "Graph 已通过统一校验，引用可读取，权限与遍历上限已明确。",
                    requiredDependencies = "Agent Authoring Graph、项目 AIWarnings、候选生成 Skill 和工作树审计。",
                    inputContract = "已批准的需求图、允许修改范围、目标产物和验收层级。",
                    workflow = "读取需求思路图关系\n有界遍历目标与约束\n执行受控步骤\n运行分层验证\n交付关系映射",
                    outputContract = "输出必须逐项映射需求思路图中的目标、约束、引用和验证关系。",
                    sideEffects = "只在当前授权范围内生成隔离候选或执行明确批准的项目写入。",
                    failureRecovery = "停止未开始的副作用，保留已检查关系和诊断，并只恢复本次安全可撤销修改。",
                    permissionBoundary = "Skill 不从 Graph 关系推导新权限，也不替代人工批准。",
                    defaultPrompt = "Use $es-requirement-mind-map-workflow to execute the approved requirement mind map."
                }), out _);
            asset.UpdateNode(validation.nodeId, validation.typeId, validation.version, validation.title,
                JsonUtility.ToJson(new ESAgentValidationPayload()), out _);

            Connect(asset, goal, ESAgentGraphStableIds.ContextOutputPortKey,
                rules, ESGraphBuiltInPortKeys.Input);
            Connect(asset, rules, ESAgentGraphStableIds.ContextOutputPortKey,
                contract, ESGraphBuiltInPortKeys.Input);
            Connect(asset, contract, ESAgentGraphStableIds.ContextOutputPortKey,
                branch, ESGraphBuiltInPortKeys.Input);
            Connect(asset, branch, ESAgentGraphStableIds.BranchMatchedPortKey,
                traversal, ESGraphBuiltInPortKeys.Input);
            Connect(asset, branch, ESAgentGraphStableIds.BranchDefaultPortKey,
                permission, ESGraphBuiltInPortKeys.Input);
            Connect(asset, branch, ESAgentGraphStableIds.BranchFailurePortKey,
                forbidden, ESGraphBuiltInPortKeys.Input);
            Connect(asset, traversal, ESAgentGraphStableIds.TraverseItemPortKey,
                required, ESGraphBuiltInPortKeys.Input);
            Connect(asset, traversal, ESAgentGraphStableIds.TraverseCompletedPortKey,
                quality, ESGraphBuiltInPortKeys.Input);
            Connect(asset, traversal, ESAgentGraphStableIds.TraverseFailurePortKey,
                forbidden, ESGraphBuiltInPortKeys.Input);
            foreach (ESGraphNodeRecord constraint in new[] { required, forbidden, permission, quality })
            {
                Connect(asset, constraint, ESAgentGraphStableIds.RequirementOutputPortKey,
                    command, ESGraphBuiltInPortKeys.Input);
                Connect(asset, constraint, ESAgentGraphStableIds.RequirementOutputPortKey,
                    skill, ESGraphBuiltInPortKeys.Input);
            }
            Connect(asset, command, ESAgentGraphStableIds.ArtifactOutputPortKey,
                validation, ESGraphBuiltInPortKeys.Input);
            Connect(asset, skill, ESAgentGraphStableIds.ArtifactOutputPortKey,
                validation, ESGraphBuiltInPortKeys.Input);
        }

        internal static void PopulateSceneScanReview(GraphAsset asset)
        {
            if (asset == null || !string.Equals(asset.DomainId, ESAgentGraphStableIds.DomainId,
                    StringComparison.Ordinal) || asset.Nodes.Count > 0 || asset.Edges.Count > 0)
                throw new InvalidOperationException("场景扫描模板只能填充空的 Agent Authoring Graph。");

            ESAutomationSceneScanPrototype.InitializeForEditor();
            if (!ESAutomationTaskRegistry.TryGet("es.scene.scan", 1, out _))
                throw new InvalidOperationException("场景扫描 TaskContract 尚未完成受信注册。");

            ESGraphNodeRecord input = Add(asset, ESAgentGraphStableIds.SkillInputNode, new Vector2(0f, 180f));
            ESGraphNodeRecord scan = Add(asset, ESAgentGraphStableIds.SkillTaskNode, new Vector2(300f, 180f));
            ESGraphNodeRecord approval = Add(asset, ESAgentGraphStableIds.SkillApprovalNode, new Vector2(620f, 80f));
            ESGraphNodeRecord completed = Add(asset, ESAgentGraphStableIds.SkillOutputNode, new Vector2(940f, 0f));
            ESGraphNodeRecord rejected = Add(asset, ESAgentGraphStableIds.SkillOutputNode, new Vector2(940f, 150f));
            ESGraphNodeRecord failed = Add(asset, ESAgentGraphStableIds.SkillOutputNode, new Vector2(620f, 360f));
            ESGraphNodeRecord timedOut = Add(asset, ESAgentGraphStableIds.SkillOutputNode, new Vector2(940f, 360f));
            ESGraphNodeRecord cancelled = Add(asset, ESAgentGraphStableIds.SkillOutputNode, new Vector2(1260f, 360f));

            asset.UpdateNode(input.nodeId, input.typeId, input.version, "场景扫描参数",
                JsonUtility.ToJson(new ESAISkillInputPayload
                {
                    skillId = "es.skill.scene-scan-review",
                    displayName = "场景扫描与人工审查",
                    parameters = new[]
                    {
                        new ESAISkillParameter { parameterId = "includeInactive", label = "包含未激活对象", valueType = ESAISkillValueType.Boolean, required = true, defaultValue = "false" },
                        new ESAISkillParameter { parameterId = "detailMode", label = "报告粒度", valueType = ESAISkillValueType.Choice, required = true, defaultValue = "summary", choices = new[] { "summary", "detailed" } },
                        new ESAISkillParameter { parameterId = "topComponentCount", label = "高频组件数量", valueType = ESAISkillValueType.Integer, required = true, defaultValue = "10", validationPattern = "^(?:[1-9]|[1-4][0-9]|50)$" }
                    }
                }), out _);
            asset.UpdateNode(scan.nodeId, scan.typeId, scan.version, "扫描当前场景",
                JsonUtility.ToJson(new ESAISkillTaskPayload
                {
                    taskId = "es.scene.scan",
                    taskVersion = 1,
                    preset = "explicit",
                    retryCount = 0,
                    timeoutSeconds = 120,
                    inputBindings = new[]
                    {
                        BoundValueBinding("includeInactive", "includeInactive"),
                        BoundValueBinding("detailMode", "detailMode"),
                        BoundValueBinding("topComponentCount", "topComponentCount")
                    }
                }), out _);
            asset.UpdateNode(approval.nodeId, approval.typeId, approval.version, "审查扫描结果",
                JsonUtility.ToJson(new ESAISkillApprovalPayload
                {
                    title = "确认场景扫描结果",
                    message = "请打开结构化报告，核对内容、产物路径和对应 Hash，再决定是否接受本次扫描结果。",
                    requireCommentOnReject = true
                }), out _);
            ConfigureOutput(asset, completed, "accepted", "已批准的扫描结果");
            ConfigureOutput(asset, rejected, "rejected", "人工未通过的扫描结果");
            ConfigureOutput(asset, failed, "failed", "扫描执行失败");
            ConfigureOutput(asset, timedOut, "timed-out", "扫描执行超时");
            ConfigureOutput(asset, cancelled, "cancelled", "扫描执行取消");

            Connect(asset, input, ESAgentGraphStableIds.SkillNextPortKey,
                scan, ESGraphBuiltInPortKeys.Input);
            Connect(asset, input, ESAgentGraphStableIds.SkillParametersPortKey,
                scan, ESAgentGraphStableIds.SkillInputPortKey);
            Connect(asset, scan, ESAgentGraphStableIds.SkillSuccessPortKey,
                approval, ESGraphBuiltInPortKeys.Input);
            Connect(asset, approval, ESAgentGraphStableIds.SkillApprovedPortKey,
                completed, ESGraphBuiltInPortKeys.Input);
            Connect(asset, approval, ESAgentGraphStableIds.SkillRejectedPortKey,
                rejected, ESGraphBuiltInPortKeys.Input);
            Connect(asset, scan, ESAgentGraphStableIds.SkillFailurePortKey,
                failed, ESGraphBuiltInPortKeys.Input);
            Connect(asset, scan, ESAgentGraphStableIds.SkillTimeoutPortKey,
                timedOut, ESGraphBuiltInPortKeys.Input);
            Connect(asset, scan, ESAgentGraphStableIds.SkillCancelledPortKey,
                cancelled, ESGraphBuiltInPortKeys.Input);
            Connect(asset, scan, ESAgentGraphStableIds.SkillRunResultPortKey,
                approval, ESAgentGraphStableIds.SkillInputPortKey);
            foreach (ESGraphNodeRecord output in new[] { completed, rejected, failed, timedOut, cancelled })
                Connect(asset, scan, ESAgentGraphStableIds.SkillRunResultPortKey,
                    output, ESAgentGraphStableIds.SkillInputPortKey);
        }

        internal static void PopulateSceneQualityReview(GraphAsset asset)
        {
            if (asset == null || !string.Equals(asset.DomainId, ESAgentGraphStableIds.DomainId,
                    StringComparison.Ordinal) || asset.Nodes.Count > 0 || asset.Edges.Count > 0)
                throw new InvalidOperationException("场景质量双重审查模板只能填充空的 Agent Authoring Graph。");

            ESAutomationSceneScanPrototype.InitializeForEditor();
            if (!ESAutomationTaskRegistry.TryGet("es.scene.scan", 1, out _))
                throw new InvalidOperationException("场景扫描 TaskContract 尚未完成受信注册。");

            ESGraphNodeRecord input = Add(asset, ESAgentGraphStableIds.SkillInputNode,
                new Vector2(0f, 260f));
            ESGraphNodeRecord scan = Add(asset, ESAgentGraphStableIds.SkillTaskNode,
                new Vector2(280f, 260f));
            ESGraphNodeRecord contentReview = Add(asset, ESAgentGraphStableIds.SkillApprovalNode,
                new Vector2(600f, 100f));
            ESGraphNodeRecord evidenceReview = Add(asset, ESAgentGraphStableIds.SkillApprovalNode,
                new Vector2(900f, 100f));
            ESGraphNodeRecord completed = Add(asset, ESAgentGraphStableIds.SkillOutputNode,
                new Vector2(1220f, 0f));
            ESGraphNodeRecord contentRejected = Add(asset, ESAgentGraphStableIds.SkillOutputNode,
                new Vector2(900f, 320f));
            ESGraphNodeRecord evidenceRejected = Add(asset, ESAgentGraphStableIds.SkillOutputNode,
                new Vector2(1220f, 220f));
            ESGraphNodeRecord failed = Add(asset, ESAgentGraphStableIds.SkillOutputNode,
                new Vector2(600f, 520f));
            ESGraphNodeRecord timedOut = Add(asset, ESAgentGraphStableIds.SkillOutputNode,
                new Vector2(900f, 520f));
            ESGraphNodeRecord cancelled = Add(asset, ESAgentGraphStableIds.SkillOutputNode,
                new Vector2(1220f, 520f));

            asset.UpdateNode(input.nodeId, input.typeId, input.version, "纯输出工作流参数入口",
                JsonUtility.ToJson(new ESAISkillInputPayload
                {
                    skillId = "es.skill.scene-quality-review",
                    displayName = "场景质量双重审查",
                    parameters = new[]
                    {
                        new ESAISkillParameter { parameterId = "includeInactive", label = "包含未激活对象", valueType = ESAISkillValueType.Boolean, required = true, defaultValue = "false" },
                        new ESAISkillParameter { parameterId = "detailMode", label = "报告粒度", valueType = ESAISkillValueType.Choice, required = true, defaultValue = "detailed", choices = new[] { "summary", "detailed" } },
                        new ESAISkillParameter { parameterId = "topComponentCount", label = "高频组件数量", valueType = ESAISkillValueType.Integer, required = true, defaultValue = "10", validationPattern = "^(?:[1-9]|[1-4][0-9]|50)$" }
                    }
                }), out _);
            asset.UpdateNode(scan.nodeId, scan.typeId, scan.version, "生成结构化扫描结果",
                JsonUtility.ToJson(new ESAISkillTaskPayload
                {
                    taskId = "es.scene.scan",
                    taskVersion = 1,
                    preset = "explicit",
                    retryCount = 0,
                    timeoutSeconds = 120,
                    inputBindings = new[]
                    {
                        BoundValueBinding("includeInactive", "includeInactive"),
                        BoundValueBinding("detailMode", "detailMode"),
                        BoundValueBinding("topComponentCount", "topComponentCount")
                    }
                }), out _);
            asset.UpdateNode(contentReview.nodeId, contentReview.typeId, contentReview.version,
                "内容完整性审查", JsonUtility.ToJson(new ESAISkillApprovalPayload
                {
                    title = "确认内容完整性",
                    message = "先检查对象层级、激活状态、组件统计和详细对象清单；通过后再进入证据完整性审查。",
                    requireCommentOnReject = true
                }), out _);
            asset.UpdateNode(evidenceReview.nodeId, evidenceReview.typeId, evidenceReview.version,
                "证据完整性审查", JsonUtility.ToJson(new ESAISkillApprovalPayload
                {
                    title = "确认证据完整性",
                    message = "核对审批面板列出的产物路径、Hash 和任务运行记录后选择批准或拒绝。",
                    requireCommentOnReject = true
                }), out _);
            ConfigureOutput(asset, completed, "accepted", "双审通过的扫描结果");
            ConfigureOutput(asset, contentRejected, "content-rejected", "内容审查未通过的扫描结果");
            ConfigureOutput(asset, evidenceRejected, "evidence-rejected", "证据审查未通过的扫描结果");
            ConfigureOutput(asset, failed, "failed", "扫描执行失败");
            ConfigureOutput(asset, timedOut, "timed-out", "扫描执行超时");
            ConfigureOutput(asset, cancelled, "cancelled", "扫描执行取消");

            Connect(asset, input, ESAgentGraphStableIds.SkillNextPortKey,
                scan, ESGraphBuiltInPortKeys.Input);
            Connect(asset, input, ESAgentGraphStableIds.SkillParametersPortKey,
                scan, ESAgentGraphStableIds.SkillInputPortKey);
            Connect(asset, scan, ESAgentGraphStableIds.SkillSuccessPortKey,
                contentReview, ESGraphBuiltInPortKeys.Input);
            Connect(asset, contentReview, ESAgentGraphStableIds.SkillApprovedPortKey,
                evidenceReview, ESGraphBuiltInPortKeys.Input);
            Connect(asset, contentReview, ESAgentGraphStableIds.SkillRejectedPortKey,
                contentRejected, ESGraphBuiltInPortKeys.Input);
            Connect(asset, evidenceReview, ESAgentGraphStableIds.SkillApprovedPortKey,
                completed, ESGraphBuiltInPortKeys.Input);
            Connect(asset, evidenceReview, ESAgentGraphStableIds.SkillRejectedPortKey,
                evidenceRejected, ESGraphBuiltInPortKeys.Input);
            Connect(asset, scan, ESAgentGraphStableIds.SkillFailurePortKey,
                failed, ESGraphBuiltInPortKeys.Input);
            Connect(asset, scan, ESAgentGraphStableIds.SkillTimeoutPortKey,
                timedOut, ESGraphBuiltInPortKeys.Input);
            Connect(asset, scan, ESAgentGraphStableIds.SkillCancelledPortKey,
                cancelled, ESGraphBuiltInPortKeys.Input);
            foreach (ESGraphNodeRecord review in new[] { contentReview, evidenceReview })
                Connect(asset, scan, ESAgentGraphStableIds.SkillRunResultPortKey,
                    review, ESAgentGraphStableIds.SkillInputPortKey);
            foreach (ESGraphNodeRecord output in new[]
                     {
                         completed, contentRejected, evidenceRejected, failed, timedOut, cancelled
                     })
                Connect(asset, scan, ESAgentGraphStableIds.SkillRunResultPortKey,
                    output, ESAgentGraphStableIds.SkillInputPortKey);
        }

        private static ESAISkillTaskInputBinding BoundValueBinding(string targetField,
            string sourcePath)
            => new ESAISkillTaskInputBinding
            {
                targetField = targetField,
                source = ESAISkillTaskInputSource.BoundValue,
                sourceId = ESAgentGraphStableIds.SkillInputPortKey,
                sourcePath = sourcePath,
                required = true
            };

        private static void ConfigureOutput(GraphAsset asset, ESGraphNodeRecord node,
            string outputId, string displayName)
        {
            asset.UpdateNode(node.nodeId, node.typeId, node.version, displayName,
                JsonUtility.ToJson(new ESAISkillOutputPayload
                {
                    outputId = outputId,
                    displayName = displayName
                }), out _);
        }

        private static void ConfigureConstraint(GraphAsset asset, ESGraphNodeRecord node,
            ESAgentConstraintKind kind, string statement, string rationale, string verification)
        {
            asset.UpdateNode(node.nodeId, node.typeId, node.version, node.title,
                JsonUtility.ToJson(new ESAgentConstraintPayload
                {
                    kind = kind,
                    statement = statement,
                    rationale = rationale,
                    verification = verification
                }), out _);
        }

        private static string GetDefaultAssetName(ESAgentAuthoringPresetKind kind)
        {
            switch (kind)
            {
                case ESAgentAuthoringPresetKind.AICommandOnly: return "AICommand 实现链图";
                case ESAgentAuthoringPresetKind.AgentSkillOnly: return "AISkill 能力编排图";
                case ESAgentAuthoringPresetKind.MindMapPaired: return "AI 实战调度图";
                case ESAgentAuthoringPresetKind.SceneScanReview: return "AISkill 场景扫描审查图";
                case ESAgentAuthoringPresetKind.SceneQualityReview: return "AISkill 场景质量双重审查图";
                default: return "Skill 能力包编排图";
            }
        }

        private static string GetCreateDescription(ESAgentAuthoringPresetKind kind)
        {
            switch (kind)
            {
                case ESAgentAuthoringPresetKind.AICommandOnly: return "创建以文本需求驱动真实实现的 AICommand 链图";
                case ESAgentAuthoringPresetKind.AgentSkillOnly: return "创建只生成 AISkill 候选文件的预设图";
                case ESAgentAuthoringPresetKind.MindMapPaired: return "创建带三路分支、有界遍历和双产物门禁的 AI 实战调度图";
                case ESAgentAuthoringPresetKind.SceneScanReview: return "创建参数化场景扫描、人工确认和结构化终态齐全的可执行 AISkill 图";
                case ESAgentAuthoringPresetKind.SceneQualityReview: return "创建先审查扫描内容、再审查证据完整性，并为两级拒绝和任务异常保留独立终态的可烘焙 AISkill 图";
                default: return "创建由 AICommand 与 AISkill 共同组成的 Skill 能力包预设图";
            }
        }

        private static string GetGoalTitle(ESAgentAuthoringPresetKind kind)
        {
            switch (kind)
            {
                case ESAgentAuthoringPresetKind.AICommandOnly: return "模块编排 AICommand 实现链";
                case ESAgentAuthoringPresetKind.AgentSkillOnly: return "模块编排 AISkill";
                default: return "模块编排 Skill 能力包";
            }
        }

        private static string GetGoalObjective(ESAgentAuthoringPresetKind kind)
        {
            switch (kind)
            {
                case ESAgentAuthoringPresetKind.AICommandOnly:
                    return "根据项目规则，把模块编排需求整理成一条可以交给 AI 真正执行的实现链，并生成可审查的 AICommand 候选。";
                case ESAgentAuthoringPresetKind.AgentSkillOnly:
                    return "根据项目规则生成一个面向模块编排、可复用、触发边界明确并带验证步骤的 AISkill 候选。";
                default:
                    return "根据项目规则为模块编排生成由 AICommand 与 AISkill 共同组成的可复用 Skill 能力包。";
            }
        }

        private static string GetGoalSuccessCriteria(ESAgentAuthoringPresetKind kind)
        {
            switch (kind)
            {
                case ESAgentAuthoringPresetKind.AICommandOnly:
                    return "模块编排 AICommand 与目标一致、权限明确，并通过候选 Diff Review。";
                case ESAgentAuthoringPresetKind.AgentSkillOnly:
                    return "模块编排 AISkill 的触发、非目标、工作流和验证步骤均可审查。";
                default:
                    return "模块编排 AICommand 与 AISkill 保持同一业务主题、输入和权限边界，并通过候选检查和人工批准。";
            }
        }

        internal static void EnsureAssetFolder(string assetFolder)
        {
            string normalized = (assetFolder ?? string.Empty).Replace('\\', '/').TrimEnd('/');
            if (string.IsNullOrEmpty(normalized) || AssetDatabase.IsValidFolder(normalized)) return;
            string[] parts = normalized.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }

    public static class ESAgentArtifactGenerationWorkspace
    {
        public const string CandidateRoot = "ES/Automation/Candidates/AgentAuthoring";
        public const string SnapshotRoot = "ES/Automation/Artifacts/GraphSnapshots";
        private const string LatestRequestEditorPref = "ES.AgentAuthoring.LatestRequest";
        private const string RequestByGraphEditorPrefPrefix = "ES.AgentAuthoring.RequestByGraph.";
        private const string ArtifactIdentityMarkerLabel = "ES-AGENT-ARTIFACT-ID:";

        public static event Action StateChanged;

        public static bool TryCreateArtifactView(ESAgentArtifactGenerationSpec source,
            ESAgentArtifactKind artifactKind, out ESAgentArtifactGenerationSpec artifactView,
            out string error)
        {
            if (!Enum.IsDefined(typeof(ESAgentArtifactKind), artifactKind))
            {
                artifactView = null;
                error = "GenerationSpec 或产物类型无效。";
                return false;
            }

            string missingOutputError = artifactKind == ESAgentArtifactKind.AICommand
                ? "整图没有 AICommand Output，无法作为 Command 使用或保存。"
                : "整图没有 Agent Skill Output，无法作为 Skill 使用或保存。";
            return TryCreateArtifactView(source, item => item.artifactKind == artifactKind,
                false, missingOutputError, out artifactView, out error);
        }

        public static bool TryCreateArtifactView(ESAgentArtifactGenerationSpec source,
            string outputNodeId, out ESAgentArtifactGenerationSpec artifactView, out string error)
        {
            if (string.IsNullOrWhiteSpace(outputNodeId))
            {
                artifactView = null;
                error = "Output NodeId 不能为空。";
                return false;
            }

            return TryCreateArtifactView(source,
                item => string.Equals(item.nodeId, outputNodeId, StringComparison.Ordinal),
                true, "GenerationSpec 中没有稳定 NodeId 对应的 Output：" + outputNodeId,
                out artifactView, out error);
        }

        private static bool TryCreateArtifactView(ESAgentArtifactGenerationSpec source,
            Func<ESAgentGenerationOutput, bool> selector, bool requireSingleOutput,
            string missingOutputError, out ESAgentArtifactGenerationSpec artifactView, out string error)
        {
            artifactView = null;
            if (source?.goal == null || selector == null)
            {
                error = "GenerationSpec 不完整。";
                return false;
            }
            if (!ESAgentGenerationIntentValidator.TryValidate(source, out error))
                return false;

            ESAgentArtifactGenerationSpec cloned;
            try
            {
                cloned = JsonUtility.FromJson<ESAgentArtifactGenerationSpec>(JsonUtility.ToJson(source));
            }
            catch (ArgumentException exception)
            {
                error = "无法建立独立整图视图：" + exception.Message;
                return false;
            }
            if (cloned?.goal == null)
            {
                error = "GenerationSpec 缺少最终目的。";
                return false;
            }

            ESAgentGenerationOutput[] selectedOutputs = (cloned.outputs
                    ?? Array.Empty<ESAgentGenerationOutput>())
                .Where(item => item != null && selector(item))
                .ToArray();
            if (selectedOutputs.Length == 0)
            {
                error = missingOutputError;
                return false;
            }
            if (requireSingleOutput && selectedOutputs.Length != 1)
            {
                error = "Output NodeId 在 GenerationSpec 中不唯一，已拒绝局部执行。";
                return false;
            }
            if (selectedOutputs.Any(item => !Enum.IsDefined(typeof(ESAgentArtifactKind), item.artifactKind)))
            {
                error = "所选 Output 的产物类型无效。";
                return false;
            }

            ESAgentGenerationRelation[] sourceRelations = cloned.relations
                ?? Array.Empty<ESAgentGenerationRelation>();
            var incoming = new Dictionary<string, List<ESAgentGenerationRelation>>(StringComparer.Ordinal);
            var outgoing = new Dictionary<string, List<ESAgentGenerationRelation>>(StringComparer.Ordinal);
            for (int i = 0; i < sourceRelations.Length; i++)
            {
                ESAgentGenerationRelation relation = sourceRelations[i];
                if (relation == null || string.IsNullOrWhiteSpace(relation.fromNodeId)
                    || string.IsNullOrWhiteSpace(relation.toNodeId))
                {
                    error = "GenerationSpec 包含无法解析的思路图关系。";
                    return false;
                }
                AddRelation(incoming, relation.toNodeId, relation);
                AddRelation(outgoing, relation.fromNodeId, relation);
            }

            var retainedNodeIds = new HashSet<string>(StringComparer.Ordinal);
            var selectedOutputIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < selectedOutputs.Length; i++)
            {
                string nodeId = selectedOutputs[i].nodeId;
                if (string.IsNullOrWhiteSpace(nodeId))
                {
                    error = "所选 Output 缺少稳定 NodeId。";
                    return false;
                }
                retainedNodeIds.Add(nodeId);
                selectedOutputIds.Add(nodeId);
            }

            TraverseRelations(selectedOutputIds, incoming, true, retainedNodeIds);
            TraverseRelations(selectedOutputIds, outgoing, false, retainedNodeIds);
            if (string.IsNullOrWhiteSpace(cloned.goal.nodeId)
                || !retainedNodeIds.Contains(cloned.goal.nodeId))
            {
                error = "所选 Output 无法沿 Relations 回溯到最终目的。";
                return false;
            }

            ESAgentGenerationValidation[] selectedValidations = (cloned.validations
                    ?? Array.Empty<ESAgentGenerationValidation>())
                .Where(item => item != null && retainedNodeIds.Contains(item.nodeId))
                .ToArray();
            if (selectedValidations.Length == 0)
            {
                error = "所选 Output 没有连接 Validation，不能绕过审查和人工批准门禁。";
                return false;
            }

            ESAgentGenerationRelation[] retainedRelations = sourceRelations
                .Where(item => retainedNodeIds.Contains(item.fromNodeId)
                    && retainedNodeIds.Contains(item.toNodeId))
                .OrderBy(item => item.order)
                .ThenBy(item => item.edgeId, StringComparer.Ordinal)
                .ToArray();
            for (int i = 0; i < selectedOutputs.Length; i++)
            {
                string outputId = selectedOutputs[i].nodeId;
                bool hasRequirement = retainedRelations.Any(item => item.toNodeId == outputId);
                bool hasValidation = retainedRelations.Any(item => item.fromNodeId == outputId
                    && selectedValidations.Any(validation => validation.nodeId == item.toNodeId));
                if (!hasRequirement || !hasValidation)
                {
                    error = "Output 的要求或 Validation 关系不完整：" + selectedOutputs[i].artifactName;
                    return false;
                }
            }

            artifactView = new ESAgentArtifactGenerationSpec
            {
                sourceGraphId = cloned.sourceGraphId,
                sourceOriginGraphId = cloned.sourceOriginGraphId,
                sourceContentSignature = cloned.sourceContentSignature,
                goal = cloned.goal,
                references = (cloned.references ?? Array.Empty<ESAgentGenerationReference>())
                    .Where(item => item != null && retainedNodeIds.Contains(item.nodeId)).ToArray(),
                constraints = (cloned.constraints ?? Array.Empty<ESAgentGenerationConstraint>())
                    .Where(item => item != null && retainedNodeIds.Contains(item.nodeId)).ToArray(),
                branches = (cloned.branches ?? Array.Empty<ESAgentGenerationBranch>())
                    .Where(item => item != null && retainedNodeIds.Contains(item.nodeId)).ToArray(),
                traversals = (cloned.traversals ?? Array.Empty<ESAgentGenerationTraversal>())
                    .Where(item => item != null && retainedNodeIds.Contains(item.nodeId)).ToArray(),
                outputs = selectedOutputs,
                validations = selectedValidations,
                relations = retainedRelations,
                riskAcceptance = cloned.riskAcceptance
            };
            artifactView.skillBundle = ESAgentSkillBundleContract.Create(
                artifactView.sourceGraphId, artifactView.goal.title, artifactView.goal.nodeId,
                artifactView.references, artifactView.constraints, artifactView.branches,
                artifactView.traversals, artifactView.outputs,
                artifactView.validations);
            if (!ESAgentGenerationIntentValidator.TryValidate(artifactView, out error))
            {
                artifactView = null;
                return false;
            }
            if (!ESAgentGenerationRiskValidator.TryValidate(artifactView, out error))
            {
                artifactView = null;
                return false;
            }
            error = string.Empty;
            return true;
        }

        private static void AddRelation(Dictionary<string, List<ESAgentGenerationRelation>> index,
            string nodeId, ESAgentGenerationRelation relation)
        {
            if (!index.TryGetValue(nodeId, out List<ESAgentGenerationRelation> relations))
            {
                relations = new List<ESAgentGenerationRelation>();
                index.Add(nodeId, relations);
            }
            relations.Add(relation);
        }

        private static void TraverseRelations(IEnumerable<string> roots,
            Dictionary<string, List<ESAgentGenerationRelation>> index, bool traverseIncoming,
            HashSet<string> retainedNodeIds)
        {
            var queue = new Queue<string>(roots);
            while (queue.Count > 0)
            {
                string nodeId = queue.Dequeue();
                if (!index.TryGetValue(nodeId, out List<ESAgentGenerationRelation> relations))
                    continue;
                for (int i = 0; i < relations.Count; i++)
                {
                    string next = traverseIncoming ? relations[i].fromNodeId : relations[i].toNodeId;
                    if (retainedNodeIds.Add(next))
                        queue.Enqueue(next);
                }
            }
        }

        public static bool CreateAndSend(ESAgentArtifactGenerationSpec spec, out string requestDirectory,
            out string error)
        {
            return CreateAndSend(spec, out requestDirectory, out _, out error);
        }

        public static bool CreateAndSend(ESAgentArtifactGenerationSpec spec, out string requestDirectory,
            out string dispatchMessage, out string error)
        {
            requestDirectory = string.Empty;
            dispatchMessage = string.Empty;
            if (!TryCreateRequest(spec, out ESAgentArtifactGenerationRequest request, out string prompt, out error))
                return false;
            requestDirectory = request.requestDirectory;
            ESAutomationTaskInvocationResult run = ESAgentGraphAutomation.Dispatch(
                ESAgentGraphAutomation.GenerateTaskId, request.requestId, request.requestDirectory,
                spec.sourceGraphId, spec.SourceContentSignature, "candidate", prompt,
                request.spec.riskAcceptance, ESGraphRiskAcceptance.CurrentOperatorId);
            dispatchMessage = run.message;
            if (run.status != "Accepted" && run.status != "Starting" && run.status != "Running")
            {
                error = "候选请求已创建，但 Automation 未确认发送：" + run.message;
                return false;
            }
            ESAgentArtifactCandidateReviewWindow.Open(requestDirectory);
            return true;
        }

        public static bool TryCreateRequest(ESAgentArtifactGenerationSpec spec,
            out ESAgentArtifactGenerationRequest request, out string prompt, out string error)
        {
            request = null;
            prompt = string.Empty;
            if (spec == null || spec.goal == null || spec.outputs == null || spec.outputs.Length == 0)
            {
                error = "GenerationSpec 不完整。";
                return false;
            }
            if (!TryPrepareArtifactOperations(spec, out error))
                return false;

            string requestId = CreateRequestId("artifact");
            string relativeDirectory = CandidateRoot + "/" + requestId;
            string fullDirectory = ResolveProjectPath(relativeDirectory);
            string stagingDirectory = fullDirectory + ".tmp-" + Guid.NewGuid().ToString("N");
            string candidateDirectory = Path.Combine(stagingDirectory, "candidate");
            try
            {
                Directory.CreateDirectory(candidateDirectory);
                request = new ESAgentArtifactGenerationRequest
                {
                    requestId = requestId,
                    createdAtUtc = DateTime.UtcNow.ToString("O"),
                    requestDirectory = relativeDirectory,
                    candidateDirectory = relativeDirectory + "/candidate",
                    spec = spec
                };
                prompt = BuildPrompt(request);
                WriteUtf8(Path.Combine(stagingDirectory, "generation-request.json"), JsonUtility.ToJson(request, true));
                WriteUtf8(Path.Combine(stagingDirectory, "generation-prompt.md"), prompt);
                WriteUtf8(Path.Combine(stagingDirectory, "README.md"), BuildRequestReadme(request));
                if (Directory.Exists(fullDirectory))
                    throw new IOException("请求目录已存在：" + relativeDirectory);
                Directory.Move(stagingDirectory, fullDirectory);
                EditorPrefs.SetString(LatestRequestEditorPref, relativeDirectory);
                RememberRequest(spec, relativeDirectory);
                NotifyStateChanged();
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                DeleteCandidateDirectory(stagingDirectory);
                error = "创建候选请求失败：" + exception.Message;
                return false;
            }
        }

        public static bool SendImmediateExecution(ESAgentArtifactGenerationSpec spec, out string requestId,
            out string dispatchMessage, out string error)
        {
            requestId = string.Empty;
            dispatchMessage = string.Empty;
            if (spec?.goal == null || string.IsNullOrWhiteSpace(spec.goal.objective)
                || string.IsNullOrWhiteSpace(spec.goal.successCriteria)
                || spec.outputs == null || spec.outputs.Length == 0)
            {
                error = "即时执行失败：最终目的和成功标准不能为空。";
                return false;
            }
            if (!ESAgentGenerationIntentValidator.TryValidate(spec, out error))
            {
                error = "即时执行失败：" + error;
                return false;
            }
            if (!ESAgentGenerationRiskValidator.TryValidate(spec, out error))
            {
                error = "即时执行失败：" + error;
                return false;
            }
            foreach (ESAgentGenerationOutput output in spec.outputs ?? Array.Empty<ESAgentGenerationOutput>())
            {
                if (!ESAgentGenerationContractValidator.TryValidate(output, out error))
                {
                    error = "即时执行失败：" + error;
                    return false;
                }
            }
            requestId = CreateRequestId("run");
            string prompt = BuildImmediateExecutionPrompt(spec, requestId);
            ESAutomationTaskInvocationResult run = ESAgentGraphAutomation.Dispatch(
                ESAgentGraphAutomation.UseTaskId, requestId, string.Empty, spec.sourceGraphId,
                spec.SourceContentSignature, "immediate", prompt, spec.riskAcceptance,
                ESGraphRiskAcceptance.CurrentOperatorId);
            dispatchMessage = run.message;
            if (run.status != "Accepted" && run.status != "Starting" && run.status != "Running")
            {
                error = "即时执行未能由 Automation 确认发送：" + run.message;
                return false;
            }
            error = string.Empty;
            return true;
        }

        public static bool SendSingleUse(ESAgentArtifactGenerationSpec spec,
            ESAgentArtifactKind artifactKind, out string requestId, out string dispatchMessage,
            out string error)
        {
            if (!TryCreateArtifactView(spec, artifactKind,
                    out ESAgentArtifactGenerationSpec artifactView, out error))
            {
                requestId = string.Empty;
                dispatchMessage = string.Empty;
                return false;
            }
            if (artifactView.outputs.Length != 1)
            {
                requestId = string.Empty;
                dispatchMessage = string.Empty;
                error = "检测到多个同类 Output，请在目标节点卡片中执行局部使用。";
                return false;
            }

            return SendSingleUseArtifactView(artifactView, out requestId, out dispatchMessage, out error);
        }

        public static bool SendSingleUse(ESAgentArtifactGenerationSpec spec, string outputNodeId,
            out string requestId, out string dispatchMessage, out string error)
        {
            if (!TryCreateArtifactView(spec, outputNodeId,
                    out ESAgentArtifactGenerationSpec artifactView, out error))
            {
                requestId = string.Empty;
                dispatchMessage = string.Empty;
                return false;
            }

            return SendSingleUseArtifactView(artifactView, out requestId, out dispatchMessage, out error);
        }

        private static bool SendSingleUseArtifactView(ESAgentArtifactGenerationSpec artifactView,
            out string requestId, out string dispatchMessage, out string error)
        {
            requestId = string.Empty;
            dispatchMessage = string.Empty;
            if (artifactView?.outputs == null || artifactView.outputs.Length != 1)
            {
                error = "单次使用请求必须包含唯一且有效的 Output。";
                return false;
            }
            if (artifactView.contractSchemaVersion != ESAgentArtifactGenerationSpec.CurrentContractSchemaVersion)
            {
                error = "单次使用请求的语义契约版本不受支持。";
                return false;
            }
            ESAgentGenerationOutput output = artifactView.outputs[0];
            if (!ESAgentGenerationContractValidator.TryValidate(output, out error))
            {
                return false;
            }
            if (!ESAgentGenerationRiskValidator.TryValidate(artifactView, out error))
                return false;

            requestId = CreateRequestId(output.artifactKind == ESAgentArtifactKind.AICommand
                ? "command" : "skill");
            string prompt = output.artifactKind == ESAgentArtifactKind.AICommand
                ? BuildImmediateExecutionPrompt(artifactView, requestId)
                : BuildTemporarySkillExecutionPrompt(artifactView, requestId);
            ESAutomationTaskInvocationResult run = ESAgentGraphAutomation.Dispatch(
                ESAgentGraphAutomation.UseTaskId, requestId, string.Empty, artifactView.sourceGraphId,
                artifactView.SourceContentSignature, "single-use", prompt, artifactView.riskAcceptance,
                ESGraphRiskAcceptance.CurrentOperatorId);
            dispatchMessage = run.message;
            if (run.status != "Accepted" && run.status != "Starting" && run.status != "Running")
            {
                error = "单次使用请求未能由 Automation 确认发送：" + run.message;
                return false;
            }
            error = string.Empty;
            return true;
        }

        public static string BuildCopyText(ESAgentArtifactGenerationSpec spec, ESAgentGraphCopyFormat format)
        {
            return TryBuildCopyText(spec, format, out string content, out _) ? content : string.Empty;
        }

        public static bool TryBuildCopyText(ESAgentArtifactGenerationSpec spec, ESAgentGraphCopyFormat format,
            out string content, out string error)
        {
            content = string.Empty;
            if (spec == null)
            {
                error = "没有可复制的智能助手编排图。";
                return false;
            }
            if (!ESAgentGenerationIntentValidator.TryValidate(spec, out error))
            {
                return false;
            }
            if (!ESAgentGenerationRiskValidator.TryValidate(spec, out error))
                return false;
            if (spec.outputs == null || spec.outputs.Length == 0)
            {
                error = "GenerationSpec 没有可复制的 Output。";
                return false;
            }
            foreach (ESAgentGenerationOutput output in spec.outputs ?? Array.Empty<ESAgentGenerationOutput>())
                if (!ESAgentGenerationContractValidator.TryValidate(output, out error))
                    return false;
            switch (format)
            {
                case ESAgentGraphCopyFormat.ImmediateExecutionPrompt:
                    content = BuildImmediateExecutionPrompt(spec, "clipboard");
                    break;
                case ESAgentGraphCopyFormat.ArtifactRequestJson:
                    ESAgentArtifactGenerationSpec resolvedSpec = JsonUtility.FromJson<ESAgentArtifactGenerationSpec>(
                        JsonUtility.ToJson(spec));
                    if (!TryPrepareArtifactOperations(resolvedSpec, out error))
                        return false;
                    content = JsonUtility.ToJson(new ESAgentGraphClipboardPackage
                    {
                        format = format,
                        requestId = "copy-" + CreateRequestId("artifact"),
                        generatedAtUtc = DateTime.UtcNow.ToString("O"),
                        spec = resolvedSpec
                    }, true);
                    break;
                case ESAgentGraphCopyFormat.GraphMarkdown:
                    content = BuildGraphMarkdown(spec);
                    break;
                default:
                    error = "不支持的复制格式：" + format;
                    return false;
            }
            error = string.Empty;
            return true;
        }

        public static string BuildImmediateExecutionPrompt(ESAgentArtifactGenerationSpec spec, string requestId)
        {
            var builder = new StringBuilder();
            builder.AppendLine("# ES Graph 即时执行请求");
            builder.AppendLine();
            builder.AppendLine("请求编号：`" + (requestId ?? string.Empty) + "`");
            builder.AppendLine("GraphId：`" + (spec?.sourceGraphId ?? string.Empty) + "`");
            builder.AppendLine("内容签名：`" + (spec?.SourceContentSignature ?? string.Empty) + "`");
            AppendRiskAcceptance(builder, spec);
            builder.AppendLine();
            builder.AppendLine("## 最终目的");
            builder.AppendLine();
            builder.AppendLine(spec?.goal?.objective ?? string.Empty);
            builder.AppendLine();
            builder.AppendLine("成功标准：" + (spec?.goal?.successCriteria ?? string.Empty));
            if (!string.IsNullOrWhiteSpace(spec?.goal?.context)) builder.AppendLine("上下文：" + spec.goal.context);
            if (!string.IsNullOrWhiteSpace(spec?.goal?.targetUsers)) builder.AppendLine("使用者 / 触发场景：" + spec.goal.targetUsers);
            builder.AppendLine();
            builder.AppendLine("## 当前动作");
            builder.AppendLine();
            builder.AppendLine("直接执行当前请求保留的 Graph 分支所描述的任务。不要把本次动作误解为生成永久 AICommand/Agent Skill；"
                + "除非最终目的本身明确要求，否则不要创建或更新永久 Agent Artifact。");
            builder.AppendLine("Graph 不能扩大当前用户授权；仍须遵守项目 AIWarnings、当前工作树保护和真实证据边界。"
                + "未经当前明确授权，不执行 Git、历史/审计写入、发布、上传或删除。");
            AppendExecutionPlan(builder, spec);
            AppendMindMap(builder, spec);
            builder.AppendLine();
            builder.AppendLine("完成后用中文报告：实际执行内容、改动文件、验证证据、未完成项和剩余风险。不得只复述 Graph。 ");
            return builder.ToString();
        }

        public static string BuildTemporarySkillExecutionPrompt(ESAgentArtifactGenerationSpec spec,
            string requestId)
        {
            var builder = new StringBuilder();
            builder.AppendLine("# ES Graph 临时 Skill 使用请求");
            builder.AppendLine();
            builder.AppendLine("请求编号：`" + (requestId ?? string.Empty) + "`");
            builder.AppendLine("GraphId：`" + (spec?.sourceGraphId ?? string.Empty) + "`");
            builder.AppendLine("内容签名：`" + (spec?.SourceContentSignature ?? string.Empty) + "`");
            AppendRiskAcceptance(builder, spec);
            builder.AppendLine();
            builder.AppendLine("## 本次 Skill");
            builder.AppendLine();
            foreach (ESAgentGenerationOutput output in spec?.outputs ?? Array.Empty<ESAgentGenerationOutput>())
            {
                if (output?.artifactKind != ESAgentArtifactKind.AgentSkill)
                    continue;
                builder.AppendLine("- " + output.artifactName + "：" + output.skillDescription);
                builder.AppendLine("  - 效果：" + ESAgentSemanticPresentation.SkillEffect(output.skillEffectKind)
                    + "；幂等：" + ESAgentSemanticPresentation.SkillIdempotency(output.skillIdempotency));
                if (!string.IsNullOrWhiteSpace(output.skillTriggerScenarios))
                    builder.AppendLine("  - 触发场景：" + output.skillTriggerScenarios);
                if (!string.IsNullOrWhiteSpace(output.skillNonTriggerScenarios))
                    builder.AppendLine("  - 不触发场景：" + output.skillNonTriggerScenarios);
                if (!string.IsNullOrWhiteSpace(output.skillPreconditions))
                    builder.AppendLine("  - 前置条件：" + output.skillPreconditions);
                if (!string.IsNullOrWhiteSpace(output.skillInputContract))
                    builder.AppendLine("  - 输入契约：" + output.skillInputContract);
                if (!string.IsNullOrWhiteSpace(output.skillWorkflow))
                    builder.AppendLine("  - 工作流：" + output.skillWorkflow);
                if (!string.IsNullOrWhiteSpace(output.skillOutputContract))
                    builder.AppendLine("  - 输出契约：" + output.skillOutputContract);
                if (!string.IsNullOrWhiteSpace(output.skillSideEffects))
                    builder.AppendLine("  - 副作用：" + output.skillSideEffects);
                if (!string.IsNullOrWhiteSpace(output.skillNonGoals))
                    builder.AppendLine("  - 非目标：" + output.skillNonGoals);
                if (!string.IsNullOrWhiteSpace(output.skillFailureRecovery))
                    builder.AppendLine("  - 失败恢复：" + output.skillFailureRecovery);
                if (!string.IsNullOrWhiteSpace(output.skillPermissionBoundary))
                    builder.AppendLine("  - 权限边界：" + output.skillPermissionBoundary);
            }
            builder.AppendLine();
            builder.AppendLine("仅在本次任务中把当前 Output 对应的 Graph 分支当作临时 Skill 工作流使用。"
                + "不得安装、创建或更新 `.agents/skills`，不得生成永久 AICommand/Agent Skill 候选；"
                + "Graph 中声明的正式目标路径只提供上下文，不构成写入授权。");
            builder.AppendLine("Graph 和临时 Skill 都不能扩大当前用户授权；仍须遵守 AIWarnings、"
                + "AICommand 合同、工作树保护和真实证据边界。");
            AppendExecutionPlan(builder, spec);
            AppendMindMap(builder, spec);
            builder.AppendLine();
            builder.AppendLine("完成后用中文报告：实际执行内容、改动文件、验证证据、未完成项和剩余风险。"
                + "不得声称该 Skill 已安装或可在后续会话自动发现。");
            return builder.ToString();
        }

        public static string BuildGraphMarkdown(ESAgentArtifactGenerationSpec spec)
        {
            var builder = new StringBuilder();
            builder.AppendLine("# ES Agent Authoring Graph");
            builder.AppendLine();
            builder.AppendLine("- GraphId：`" + (spec?.sourceGraphId ?? string.Empty) + "`");
            builder.AppendLine("- 来源 GraphId：`" + (spec?.sourceOriginGraphId ?? string.Empty) + "`");
            builder.AppendLine("- 内容签名：`" + (spec?.SourceContentSignature ?? string.Empty) + "`");
            AppendRiskAcceptance(builder, spec);
            builder.AppendLine();
            builder.AppendLine("## 最终目的");
            builder.AppendLine();
            builder.AppendLine(spec?.goal?.objective ?? string.Empty);
            builder.AppendLine();
            builder.AppendLine("成功标准：" + (spec?.goal?.successCriteria ?? string.Empty));
            if (spec?.skillBundle != null)
            {
                ESAgentSkillBundleContract bundle = spec.skillBundle;
                builder.AppendLine();
                builder.AppendLine("## Skill 能力包");
                builder.AppendLine();
                builder.AppendLine("- BundleId：`" + bundle.bundleId + "`");
                builder.AppendLine("- 组成：" + bundle.kind + "（AICommand + AISkill）");
                builder.AppendLine("- AICommand Output：" + string.Join(", ", bundle.commandOutputNodeIds ?? Array.Empty<string>()));
                builder.AppendLine("- AISkill Output：" + string.Join(", ", bundle.aiSkillOutputNodeIds ?? Array.Empty<string>()));
                builder.AppendLine("- 共享边界：Goal、References、Constraints、Validation 和人工批准状态必须一致。");
            }
            AppendExecutionPlan(builder, spec);
            AppendMindMap(builder, spec);
            return builder.ToString();
        }

        public static string BuildArtifactIdentityMarker(string artifactId)
        {
            return "<!-- " + ArtifactIdentityMarkerLabel + " " + (artifactId ?? string.Empty) + " -->";
        }

        private static void AppendRiskAcceptance(StringBuilder builder, ESAgentArtifactGenerationSpec spec)
        {
            ESGraphRiskAcceptance acceptance = spec?.riskAcceptance;
            if (acceptance == null) return;
            builder.AppendLine("- 风险确认：`" + acceptance.acceptanceHash + "`");
            builder.AppendLine("- 已确认问题：" + string.Join(", ", acceptance.issueCodes ?? Array.Empty<string>()));
            builder.AppendLine("- 确认者 / 时间：" + acceptance.acceptedBy + " / " + acceptance.acceptedAtUtc);
        }

        private static void AppendExecutionPlan(StringBuilder builder, ESAgentArtifactGenerationSpec spec)
        {
            builder.AppendLine();
            builder.AppendLine("## 必须读取的资料");
            foreach (ESAgentGenerationReference item in spec?.references ?? Array.Empty<ESAgentGenerationReference>())
                builder.AppendLine("- [" + item.referenceKind + "] `" + item.projectPath + "`：" + item.purpose
                    + "（required=" + item.required + "）");
            builder.AppendLine();
            builder.AppendLine("## 约束");
            builder.AppendLine("固定裁决顺序：Forbidden > Required > Permission > Quality；同类型按优先级从高到低。\n");
            foreach (ESAgentGenerationConstraint item in OrderedConstraints(spec))
            {
                builder.AppendLine("- [" + item.kind + " | "
                    + ESAgentSemanticPresentation.ConstraintScope(item.scope) + " | "
                    + ESAgentSemanticPresentation.ConstraintCombination(item.combinationMode)
                    + " | priority=" + item.priority + "] " + item.statement);
                if (item.combinationMode == ESAgentConstraintCombinationMode.AnyOf)
                    builder.AppendLine("  - 组合组：`" + item.combinationGroup + "`");
                if (!string.IsNullOrWhiteSpace(item.rationale)) builder.AppendLine("  - 原因：" + item.rationale);
                if (!string.IsNullOrWhiteSpace(item.verification)) builder.AppendLine("  - 验证：" + item.verification);
            }
            builder.AppendLine();
            builder.AppendLine("## 执行与产物要求");
            foreach (ESAgentGenerationOutput item in spec?.outputs ?? Array.Empty<ESAgentGenerationOutput>())
            {
                builder.AppendLine("- [" + item.artifactKind + "] " + item.artifactName + "：" + item.requirements);
                builder.AppendLine("  - ArtifactId：`" + item.artifactId + "`");
                builder.AppendLine("  - 正式目标：`" + item.targetProjectPath + "`");
                builder.AppendLine("  - 创建 / 更新方式：" + OperationModeCaption(item.operationMode));
                if (item.artifactKind == ESAgentArtifactKind.AICommand)
                {
                    builder.AppendLine("  - 任务意图：" + ESAgentSemanticPresentation.CommandIntent(item.commandIntent));
                    builder.AppendLine("  - 写入授权：" + ESAgentSemanticPresentation.WriteAuthorization(item.writeAuthorization));
                    builder.AppendLine("  - 风险等级：" + ESAgentSemanticPresentation.RiskLevel(item.commandRiskLevel));
                    builder.AppendLine("  - 失败策略：" + ESAgentSemanticPresentation.FailurePolicy(item.failurePolicy));
                    if (!string.IsNullOrWhiteSpace(item.expectedInputs)) builder.AppendLine("  - 预期输入：" + item.expectedInputs);
                    if (!string.IsNullOrWhiteSpace(item.preconditions)) builder.AppendLine("  - 前置条件：" + item.preconditions);
                    if (!string.IsNullOrWhiteSpace(item.allowedWriteScopes)) builder.AppendLine("  - 允许写入范围：" + item.allowedWriteScopes);
                    if (!string.IsNullOrWhiteSpace(item.forbiddenOperations)) builder.AppendLine("  - 禁止操作：" + item.forbiddenOperations);
                    if (!string.IsNullOrWhiteSpace(item.executionOutline)) builder.AppendLine("  - 执行步骤：" + item.executionOutline);
                    if (!string.IsNullOrWhiteSpace(item.acceptanceCriteria)) builder.AppendLine("  - 完成定义：" + item.acceptanceCriteria);
                    if (!string.IsNullOrWhiteSpace(item.requiredEvidence)) builder.AppendLine("  - 必须证据：" + item.requiredEvidence);
                    if (!string.IsNullOrWhiteSpace(item.blockedHandling)) builder.AppendLine("  - 阻断处理：" + item.blockedHandling);
                    if (!string.IsNullOrWhiteSpace(item.rollbackStrategy)) builder.AppendLine("  - 回滚要求：" + item.rollbackStrategy);
                }
                else
                {
                    builder.AppendLine("  - 效果类型：" + ESAgentSemanticPresentation.SkillEffect(item.skillEffectKind));
                    builder.AppendLine("  - 幂等策略：" + ESAgentSemanticPresentation.SkillIdempotency(item.skillIdempotency));
                    if (!string.IsNullOrWhiteSpace(item.skillTriggerScenarios)) builder.AppendLine("  - 触发场景：" + item.skillTriggerScenarios);
                    if (!string.IsNullOrWhiteSpace(item.skillNonTriggerScenarios)) builder.AppendLine("  - 不触发场景：" + item.skillNonTriggerScenarios);
                    if (!string.IsNullOrWhiteSpace(item.skillPreconditions)) builder.AppendLine("  - 前置条件：" + item.skillPreconditions);
                    if (!string.IsNullOrWhiteSpace(item.skillRequiredDependencies)) builder.AppendLine("  - 必要依赖：" + item.skillRequiredDependencies);
                    if (!string.IsNullOrWhiteSpace(item.skillInputContract)) builder.AppendLine("  - 输入契约：" + item.skillInputContract);
                    if (!string.IsNullOrWhiteSpace(item.skillWorkflow)) builder.AppendLine("  - 工作流程：" + item.skillWorkflow);
                    if (!string.IsNullOrWhiteSpace(item.skillOutputContract)) builder.AppendLine("  - 输出契约：" + item.skillOutputContract);
                    if (!string.IsNullOrWhiteSpace(item.skillSideEffects)) builder.AppendLine("  - 副作用：" + item.skillSideEffects);
                    if (!string.IsNullOrWhiteSpace(item.skillFailureRecovery)) builder.AppendLine("  - 失败恢复：" + item.skillFailureRecovery);
                    if (!string.IsNullOrWhiteSpace(item.skillValidationSteps)) builder.AppendLine("  - 验证步骤：" + item.skillValidationSteps);
                    if (!string.IsNullOrWhiteSpace(item.skillPermissionBoundary)) builder.AppendLine("  - 权限边界：" + item.skillPermissionBoundary);
                }
            }
            builder.AppendLine();
            builder.AppendLine("## 验收门禁");
            foreach (ESAgentGenerationValidation item in spec?.validations ?? Array.Empty<ESAgentGenerationValidation>())
                builder.AppendLine("- UTF-8=" + item.validateUtf8 + "，Diff Review=" + item.requireDiffReview
                    + "，人工批准=" + item.requireHumanApproval + "；" + item.additionalRequirements);
        }

        private static string CreateRequestId(string prefix)
        {
            return (prefix ?? "request") + "_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + "_"
                + Guid.NewGuid().ToString("N").Substring(0, 8);
        }

        public static bool TryPrepareArtifactOperations(ESAgentArtifactGenerationSpec spec, out string error)
        {
            if (spec == null)
            {
                error = "GenerationSpec 不能为空。";
                return false;
            }
            if (!ESAgentGenerationIntentValidator.TryValidate(spec, out error))
            {
                return false;
            }
            if (!ESAgentGenerationRiskValidator.TryValidate(spec, out error))
                return false;
            if (!ESGraphIdentity.IsValid(spec.sourceGraphId))
            {
                error = "GenerationSpec 缺少有效 GraphId。";
                return false;
            }
            foreach (ESAgentGenerationOutput output in spec.outputs ?? Array.Empty<ESAgentGenerationOutput>())
            {
                if (output == null)
                {
                    error = "GenerationSpec 包含空输出。";
                    return false;
                }
                if (!ESAgentGenerationContractValidator.TryValidate(output, out error))
                    return false;
                if (string.IsNullOrWhiteSpace(output.artifactId))
                    output.artifactId = ESAgentArtifactIdentity.Create(spec.sourceGraphId, output.nodeId);
                if (string.IsNullOrWhiteSpace(output.artifactId))
                {
                    error = "无法建立永久产物稳定身份：" + output.artifactName;
                    return false;
                }
                if (!TryResolveBoundArtifact(output, out bool exists, out bool occupied, out error))
                    return false;
                if (output.operationMode == ESAgentArtifactOperationMode.CreateOnly && occupied)
                {
                    error = "目标已经存在，但输出被设置为仅创建：" + output.targetProjectPath;
                    return false;
                }
                if (output.operationMode == ESAgentArtifactOperationMode.UpdateOnly && !exists)
                {
                    error = "没有找到可更新的稳定产物：" + output.targetProjectPath;
                    return false;
                }
                if (occupied && !exists)
                {
                    error = "目标位置已被不完整内容占用：" + output.targetProjectPath;
                    return false;
                }
                output.resolvedOperation = exists
                    ? ESAgentArtifactResolvedOperation.Update
                    : ESAgentArtifactResolvedOperation.Create;
            }
            error = string.Empty;
            return true;
        }

        private static bool TryResolveBoundArtifact(ESAgentGenerationOutput output, out bool exists,
            out bool occupied, out string error)
        {
            exists = false;
            occupied = false;
            string primaryRelativePath = output.artifactKind == ESAgentArtifactKind.AICommand
                ? output.targetProjectPath
                : (output.targetProjectPath ?? string.Empty).TrimEnd('/', '\\') + "/SKILL.md";
            if (!TryResolveProjectPath(primaryRelativePath, out string primaryFullPath, out error))
                return false;
            string containerFullPath = output.artifactKind == ESAgentArtifactKind.AICommand
                ? primaryFullPath
                : Path.GetDirectoryName(primaryFullPath) ?? primaryFullPath;
            exists = File.Exists(primaryFullPath);
            occupied = exists || Directory.Exists(containerFullPath);
            if (exists)
            {
                if (TryReadArtifactIdentity(primaryFullPath, out string existingArtifactId)
                    && !string.Equals(existingArtifactId, output.artifactId, StringComparison.Ordinal))
                {
                    error = "目标已绑定到其他 ArtifactId：" + output.targetProjectPath;
                    return false;
                }
                error = string.Empty;
                return true;
            }

            List<string> matches = FindArtifactTargets(output.artifactKind, output.artifactId);
            if (matches.Count > 1)
            {
                error = "发现多个相同 ArtifactId 的正式产物，请先消除重复：" + output.artifactId;
                return false;
            }
            if (matches.Count == 1)
            {
                output.targetProjectPath = matches[0];
                exists = true;
                occupied = true;
            }
            error = string.Empty;
            return true;
        }

        private static List<string> FindArtifactTargets(ESAgentArtifactKind kind, string artifactId)
        {
            var matches = new List<string>();
            string rootRelative = kind == ESAgentArtifactKind.AICommand
                ? "Assets/Plugins/ES/AICommands"
                : ".agents/skills";
            string root = ResolveProjectPath(rootRelative);
            if (!Directory.Exists(root))
                return matches;
            string pattern = kind == ESAgentArtifactKind.AICommand ? "*.md" : "SKILL.md";
            foreach (string path in ESManagedFileIO.EnumerateFilesSafely(root, pattern))
            {
                if (!TryReadArtifactIdentity(path, out string current)
                    || !string.Equals(current, artifactId, StringComparison.Ordinal))
                    continue;
                string relative = ToProjectRelativePath(path);
                if (kind == ESAgentArtifactKind.AgentSkill)
                    relative = (Path.GetDirectoryName(relative) ?? string.Empty).Replace('\\', '/').TrimEnd('/') + "/";
                matches.Add(relative);
            }
            return matches;
        }

        internal static bool TryReadArtifactIdentity(string path, out string artifactId)
        {
            artifactId = string.Empty;
            try
            {
                string text = ReadUtf8(path);
                int marker = text.IndexOf(ArtifactIdentityMarkerLabel, StringComparison.Ordinal);
                if (marker < 0)
                    return false;
                int start = marker + ArtifactIdentityMarkerLabel.Length;
                int end = text.IndexOf("-->", start, StringComparison.Ordinal);
                if (end < 0)
                    return false;
                artifactId = text.Substring(start, end - start).Trim();
                return !string.IsNullOrWhiteSpace(artifactId);
            }
            catch
            {
                return false;
            }
        }

        private static string ToProjectRelativePath(string fullPath)
        {
            string root = GetProjectRoot().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            string normalized = Path.GetFullPath(fullPath);
            return normalized.StartsWith(root, StringComparison.OrdinalIgnoreCase)
                ? normalized.Substring(root.Length).Replace('\\', '/')
                : string.Empty;
        }

        public static string GetLatestRequestDirectory()
        {
            string saved = EditorPrefs.GetString(LatestRequestEditorPref, string.Empty);
            if (!string.IsNullOrEmpty(saved) && Directory.Exists(ResolveProjectPath(saved))) return saved;
            string root = ResolveProjectPath(CandidateRoot);
            if (!Directory.Exists(root)) return string.Empty;
            DirectoryInfo latest = new DirectoryInfo(root).GetDirectories().OrderByDescending(item => item.Name).FirstOrDefault();
            return latest == null ? string.Empty : CandidateRoot + "/" + latest.Name;
        }

        public static bool TryGetRequestDirectory(ESAgentArtifactGenerationSpec spec,
            out string requestDirectory)
        {
            requestDirectory = string.Empty;
            if (spec == null || !ESGraphIdentity.IsValid(spec.sourceGraphId)
                || string.IsNullOrWhiteSpace(spec.SourceContentSignature))
                return false;
            string saved = EditorPrefs.GetString(BuildRequestEditorPrefKey(spec.sourceGraphId,
                spec.SourceContentSignature), string.Empty);
            if (RequestMatches(saved, spec.sourceGraphId, spec.SourceContentSignature))
            {
                requestDirectory = saved;
                return true;
            }
            if (TryFindLatestRequest(spec.sourceGraphId, spec.SourceContentSignature,
                    out requestDirectory, out _))
            {
                RememberRequest(spec, requestDirectory);
                return true;
            }
            return false;
        }

        public static bool TryReadRequest(string requestDirectory,
            out ESAgentArtifactGenerationRequest request, out string error)
        {
            request = null;
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(requestDirectory))
            {
                error = "候选请求目录不能为空。";
                return false;
            }
            try
            {
                if (!TryResolveProjectPath(requestDirectory, out string fullDirectory, out error))
                    return false;
                string candidateRoot = ResolveProjectPath(CandidateRoot);
                string normalizedRoot = candidateRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    + Path.DirectorySeparatorChar;
                if (!fullDirectory.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
                {
                    error = "候选请求目录必须位于 AgentAuthoring 候选根目录内。";
                    return false;
                }
                string requestPath = Path.Combine(fullDirectory, "generation-request.json");
                if (!File.Exists(requestPath))
                {
                    error = "候选请求缺少 generation-request.json。";
                    return false;
                }
                request = JsonUtility.FromJson<ESAgentArtifactGenerationRequest>(ReadUtf8(requestPath));
                if (request?.spec == null || request.schemaVersion != ESAgentArtifactGenerationRequest.CurrentSchemaVersion)
                {
                    request = null;
                    error = "候选请求结构或版本无效。";
                    return false;
                }
                if (request.spec.contractSchemaVersion != ESAgentArtifactGenerationSpec.CurrentContractSchemaVersion)
                {
                    request = null;
                    error = "候选请求使用旧版 Graph 语义合同，请从当前 Graph 重新生成。";
                    return false;
                }
                string normalizedDirectory = NormalizeProjectRelativePath(requestDirectory);
                if (string.IsNullOrWhiteSpace(request.requestId)
                    || !string.Equals(NormalizeProjectRelativePath(request.requestDirectory),
                        normalizedDirectory, StringComparison.Ordinal)
                    || !string.Equals(NormalizeProjectRelativePath(request.candidateDirectory),
                        normalizedDirectory + "/candidate", StringComparison.Ordinal))
                {
                    request = null;
                    error = "候选请求的 RequestId 或目录身份与当前位置不一致。";
                    return false;
                }
                if (!ESAgentGenerationIntentValidator.TryValidate(request.spec, out error))
                {
                    request = null;
                    error = "候选请求的开发意图合同无效：" + error;
                    return false;
                }
                if (!ESAgentGenerationRiskValidator.TryValidate(request.spec, out error))
                {
                    request = null;
                    error = "候选请求的目标、输出或风险确认无效：" + error;
                    return false;
                }
                return true;
            }
            catch (Exception exception)
            {
                request = null;
                error = "读取候选请求失败：" + exception.Message;
                return false;
            }
        }

        public static ESAgentArtifactRequestStatus GetRequestStatus(ESAgentArtifactGenerationSpec spec)
        {
            if (spec == null || !ESGraphIdentity.IsValid(spec.sourceGraphId)
                || string.IsNullOrWhiteSpace(spec.SourceContentSignature))
                return Status(ESAgentArtifactRequestState.Invalid, string.Empty,
                    "当前 Graph 没有可核对的稳定身份或内容签名。", "先修复图并重新检查。");
            if (!TryGetRequestDirectory(spec, out string requestDirectory))
            {
                if (TryFindLatestRequest(spec.sourceGraphId, null, out string staleDirectory,
                        out ESAgentArtifactGenerationRequest staleRequest)
                    && staleRequest?.spec != null
                    && !string.Equals(staleRequest.spec.SourceContentSignature,
                        spec.SourceContentSignature, StringComparison.Ordinal))
                    return Status(ESAgentArtifactRequestState.Stale, staleDirectory,
                        "当前 Graph 内容已变化，旧候选不能用于本图。", "重新生成候选并再次人工批准。");
                return Status(ESAgentArtifactRequestState.None, string.Empty,
                    "当前 Graph 尚无匹配候选。", "先生成 AICommand / Agent Skill 候选。");
            }
            string full = ResolveProjectPath(requestDirectory);
            if (!TryGetCandidateManifestPath(full, out string manifestPath))
                return Status(ESAgentArtifactRequestState.AwaitingCandidate, requestDirectory,
                    "请求已绑定当前 Graph，正在等待候选文件与清单。", "等待生成完成后查看候选差异。");
            ESAgentArtifactGenerationRequest request;
            ESAgentArtifactCandidateManifest manifest;
            try
            {
                if (!TryReadRequest(requestDirectory, out request, out string requestError))
                    return Status(ESAgentArtifactRequestState.Invalid, requestDirectory,
                        requestError, "从当前 Graph 重新生成候选。");
                manifest = JsonUtility.FromJson<ESAgentArtifactCandidateManifest>(
                    ReadUtf8(manifestPath));
                List<string> candidateErrors = ESAgentArtifactCandidateValidator.Validate(
                    requestDirectory, request, manifest);
                if (candidateErrors.Count > 0)
                    return Status(ESAgentArtifactRequestState.Invalid, requestDirectory,
                        "候选未通过当前 Graph 合同：" + candidateErrors[0],
                        "修复候选或从当前 Graph 重新生成；无效候选不能批准。" );
            }
            catch (Exception exception)
            {
                return Status(ESAgentArtifactRequestState.Invalid, requestDirectory,
                    "候选状态读取失败：" + exception.Message,
                    "检查候选文件完整性，或从当前 Graph 重新生成。" );
            }
            if (!File.Exists(Path.Combine(full, "approval-manifest.json")))
                return Status(ESAgentArtifactRequestState.AwaitingApproval, requestDirectory,
                    "候选已生成，但尚未经过人工 Diff Review 与批准。", "查看候选差异并人工批准。");
            try
            {
                ESAgentArtifactApprovalManifest approval = JsonUtility.FromJson<ESAgentArtifactApprovalManifest>(
                    ReadUtf8(Path.Combine(full, "approval-manifest.json")));
                if (approval == null
                    || approval.schemaVersion != ESAgentArtifactApprovalManifest.CurrentSchemaVersion
                    || !string.Equals(approval.requestId, request.requestId, StringComparison.Ordinal)
                    || !string.Equals(approval.sourceGraphId, spec.sourceGraphId, StringComparison.Ordinal)
                    || !string.Equals(approval.sourceContentSignature, spec.SourceContentSignature,
                        StringComparison.Ordinal)
                    || approval.files == null || approval.files.Length == 0)
                    return Status(ESAgentArtifactRequestState.Invalid, requestDirectory,
                        "批准清单不完整或未绑定当前 Graph、请求与内容签名。",
                        "重新查看候选差异并执行人工批准。" );
                if ((request.spec.riskAcceptance == null) != (approval.riskAcceptance == null)
                    || request.spec.riskAcceptance != null
                    && !request.spec.riskAcceptance.SameAs(approval.riskAcceptance))
                    return Status(ESAgentArtifactRequestState.Invalid, requestDirectory,
                        "批准清单未绑定当前请求的风险确认。",
                        "重新查看候选差异并执行人工批准。" );
                ESAgentArtifactCandidateFile[] candidateFiles = manifest.files
                    ?? Array.Empty<ESAgentArtifactCandidateFile>();
                if (approval.files.Length != candidateFiles.Length)
                    return Status(ESAgentArtifactRequestState.Invalid, requestDirectory,
                        "批准清单没有完整覆盖候选文件。",
                        "重新查看全部候选差异并执行人工批准。" );
                foreach (ESAgentArtifactApprovedFile approved in approval.files)
                {
                    ESAgentArtifactCandidateFile candidateFile = approved == null ? null
                        : candidateFiles.FirstOrDefault(file => file != null
                            && file.artifactKind == approved.artifactKind
                            && string.Equals(NormalizeProjectRelativePath(file.targetProjectPath),
                                NormalizeProjectRelativePath(approved.targetProjectPath),
                                StringComparison.Ordinal));
                    if (approved == null || string.IsNullOrWhiteSpace(approved.sha256)
                        || approved.sha256.Length != 64 || candidateFile == null)
                        return Status(ESAgentArtifactRequestState.Invalid, requestDirectory,
                            "批准清单包含未核验、无哈希或不属于候选的文件。",
                            "重新查看全部候选差异并执行人工批准。" );
                    if (!ESAgentArtifactCandidateValidator.TryGetDeclaredOutput(request.spec,
                            candidateFile, out ESAgentGenerationOutput output, out string outputError))
                        return Status(ESAgentArtifactRequestState.Invalid, requestDirectory,
                            outputError, "从当前 Graph 重新生成候选并批准。" );
                    if (!string.Equals(approved.sourceGraphId, spec.sourceGraphId,
                            StringComparison.Ordinal)
                        || !string.Equals(approved.outputNodeId, output.nodeId,
                            StringComparison.Ordinal)
                        || !string.Equals(approved.artifactId, output.artifactId,
                            StringComparison.Ordinal))
                        return Status(ESAgentArtifactRequestState.Invalid, requestDirectory,
                            "批准文件的 GraphId、Output NodeId 或 ArtifactId 已失配。",
                            "从当前 Graph 重新生成候选并批准。" );
                    if (!ESAgentArtifactCandidateValidator.TryResolveFormalTarget(request,
                            candidateFile, out string targetPath, out string targetError)
                        || !File.Exists(targetPath))
                        return Status(ESAgentArtifactRequestState.Invalid, requestDirectory,
                            string.IsNullOrWhiteSpace(targetError)
                                ? "已批准的正式文件不存在：" + approved.targetProjectPath
                                : targetError,
                            "重新执行 Diff Review 与人工批准导入。" );
                    if (!string.Equals(ComputeSha256(targetPath), approved.sha256,
                            StringComparison.OrdinalIgnoreCase))
                        return Status(ESAgentArtifactRequestState.Invalid, requestDirectory,
                            "正式文件在批准后已经变化：" + approved.targetProjectPath,
                            "重新执行 Diff Review 与人工批准。" );
                    if (ESAgentArtifactCandidateValidator.RequiresIdentityMarker(candidateFile)
                        && (!TryReadArtifactIdentity(targetPath, out string formalArtifactId)
                            || !string.Equals(formalArtifactId, output.artifactId,
                                StringComparison.Ordinal)))
                        return Status(ESAgentArtifactRequestState.Invalid, requestDirectory,
                            "正式产物的稳定 ArtifactId 缺失或已变化：" + approved.targetProjectPath,
                            "重新生成候选并批准导入。" );
                }
            }
            catch (Exception exception)
            {
                return Status(ESAgentArtifactRequestState.Invalid, requestDirectory,
                    "批准清单读取失败：" + exception.Message,
                    "重新查看候选差异并执行人工批准。" );
            }
            return Status(ESAgentArtifactRequestState.Approved, requestDirectory,
                "候选已批准；启动前仍会复核 Graph 身份、签名和正式文件哈希。", "可以打开独立窗口执行实现。");
        }

        private static string NormalizeProjectRelativePath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/').TrimStart('/');
        }

        public static bool TryWriteGraphSnapshot(ESBakedGraphSnapshot snapshot,
            out string relativePath, out string error)
            => TryWriteGraphSnapshot(snapshot, null, out relativePath, out error);

        public static bool TryWriteGraphSnapshot(ESBakedGraphSnapshot snapshot,
            ESGraphRiskAcceptance riskAcceptance, out string relativePath, out string error)
        {
            relativePath = string.Empty;
            if (snapshot == null || !ESGraphIdentity.IsValid(snapshot.GraphId)
                || !IsContentSignature(snapshot.ContentSignature))
            {
                error = "快照缺少有效的 GraphId 或内容签名。";
                return false;
            }
            if (riskAcceptance != null
                && !riskAcceptance.TryValidateStored(snapshot.GraphId, snapshot.ContentSignature, out error))
                return false;
            try
            {
                var artifact = new ESGraphSnapshotArtifact
                {
                    createdAtUtc = DateTime.UtcNow.ToString("O"),
                    graphSchemaVersion = snapshot.SchemaVersion,
                    graphId = snapshot.GraphId,
                    originGraphId = snapshot.OriginGraphId,
                    domainId = snapshot.DomainId,
                    allowCycles = snapshot.AllowCycles,
                    contentSignature = snapshot.ContentSignature,
                    riskAcceptance = riskAcceptance,
                    nodes = snapshot.Nodes.Select(node => new ESGraphSnapshotNodeArtifact
                    {
                        nodeId = node.NodeId,
                        typeId = node.TypeId,
                        version = node.Version,
                        title = node.Title,
                        payloadJson = node.PayloadJson,
                        ports = node.Ports.Select(port => new ESGraphSnapshotPortArtifact
                        {
                            nodeId = port.NodeId,
                            portId = port.PortId,
                            stableKey = port.StableKey,
                            name = port.Name,
                            meaning = port.Meaning,
                            valueTypeId = port.ValueTypeId,
                            direction = port.Direction,
                            capacity = port.Capacity,
                            aggregation = port.Aggregation
                        }).ToArray()
                    }).ToArray(),
                    edges = snapshot.Edges.OrderBy(edge => edge.Order)
                        .ThenBy(edge => edge.EdgeId, StringComparer.Ordinal)
                        .Select(edge => new ESGraphSnapshotEdgeArtifact
                    {
                        edgeId = edge.EdgeId,
                        order = edge.Order,
                        outputPortId = edge.OutputPortId,
                        inputPortId = edge.InputPortId
                    }).ToArray(),
                    routes = snapshot.Routes.OrderBy(route => route.Order)
                        .ThenBy(route => route.EdgeId, StringComparer.Ordinal)
                        .Select(route => new ESGraphSnapshotRouteArtifact
                    {
                        edgeId = route.EdgeId,
                        order = route.Order,
                        sourceNodeId = route.SourceNodeId,
                        sourcePortId = route.SourcePortId,
                        sourcePortKey = route.SourcePortKey,
                        sourceMeaning = route.SourceMeaning,
                        sourceValueTypeId = route.SourceValueTypeId,
                        sourceAggregation = route.SourceAggregation,
                        targetNodeId = route.TargetNodeId,
                        targetPortId = route.TargetPortId,
                        targetPortKey = route.TargetPortKey,
                        targetMeaning = route.TargetMeaning,
                        targetValueTypeId = route.TargetValueTypeId,
                        targetAggregation = route.TargetAggregation
                    }).ToArray()
                };
                relativePath = BuildSnapshotRelativePath(snapshot.GraphId, snapshot.ContentSignature);
                WriteUtf8(ResolveProjectPath(relativePath), JsonUtility.ToJson(artifact, true));
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                relativePath = string.Empty;
                error = "保存 Graph 检查快照失败：" + exception.Message;
                return false;
            }
        }

        public static bool TryGetGraphSnapshot(string graphId, string contentSignature,
            out string relativePath)
        {
            relativePath = string.Empty;
            if (!ESGraphIdentity.IsValid(graphId) || !IsContentSignature(contentSignature)) return false;
            string candidate = BuildSnapshotRelativePath(graphId, contentSignature);
            if (!File.Exists(ResolveProjectPath(candidate))) return false;
            relativePath = candidate;
            return true;
        }

        public static bool TryGetLatestGraphSnapshot(string graphId, out string relativePath)
        {
            relativePath = string.Empty;
            if (!ESGraphIdentity.IsValid(graphId)) return false;
            string directory = ResolveProjectPath(SnapshotRoot + "/" + graphId);
            if (!Directory.Exists(directory)) return false;
            FileInfo latest = new DirectoryInfo(directory).GetFiles("*.json")
                .OrderByDescending(item => item.LastWriteTimeUtc).FirstOrDefault();
            if (latest == null) return false;
            relativePath = SnapshotRoot + "/" + graphId + "/" + latest.Name;
            return true;
        }

        internal static bool TryGetCandidateManifestPath(string requestFullPath, out string manifestPath)
        {
            manifestPath = Path.Combine(requestFullPath ?? string.Empty, "candidate-manifest.json");
            if (File.Exists(manifestPath)) return true;
            manifestPath = Path.Combine(requestFullPath ?? string.Empty, "candidate", "candidate-manifest.json");
            return File.Exists(manifestPath);
        }

        internal static void NotifyStateChanged()
        {
            try { StateChanged?.Invoke(); }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        private static ESAgentArtifactRequestStatus Status(ESAgentArtifactRequestState state,
            string directory, string message, string nextAction)
        {
            return new ESAgentArtifactRequestStatus
            {
                State = state, RequestDirectory = directory ?? string.Empty,
                Message = message ?? string.Empty, NextAction = nextAction ?? string.Empty
            };
        }

        private static void RememberRequest(ESAgentArtifactGenerationSpec spec, string requestDirectory)
        {
            if (spec == null || !ESGraphIdentity.IsValid(spec.sourceGraphId)
                || string.IsNullOrWhiteSpace(spec.SourceContentSignature)) return;
            EditorPrefs.SetString(BuildRequestEditorPrefKey(spec.sourceGraphId,
                spec.SourceContentSignature), requestDirectory ?? string.Empty);
        }

        private static string BuildRequestEditorPrefKey(string graphId, string signature)
        {
            return RequestByGraphEditorPrefPrefix + graphId + "." + signature;
        }

        private static bool RequestMatches(string requestDirectory, string graphId, string signature)
        {
            if (string.IsNullOrWhiteSpace(requestDirectory)) return false;
            string requestPath;
            try { requestPath = Path.Combine(ResolveProjectPath(requestDirectory), "generation-request.json"); }
            catch { return false; }
            if (!File.Exists(requestPath)) return false;
            try
            {
                string relativeDirectory = NormalizeProjectRelativeDirectory(
                    Path.GetDirectoryName(requestPath));
                return TryReadRequest(relativeDirectory, out ESAgentArtifactGenerationRequest request, out _)
                    && string.Equals(request.spec.sourceGraphId, graphId, StringComparison.Ordinal)
                    && string.Equals(request.spec.SourceContentSignature, signature, StringComparison.Ordinal);
            }
            catch { return false; }
        }

        private static bool TryFindLatestRequest(string graphId, string contentSignature,
            out string requestDirectory, out ESAgentArtifactGenerationRequest request)
        {
            requestDirectory = string.Empty;
            request = null;
            string root = ResolveProjectPath(CandidateRoot);
            if (!Directory.Exists(root)) return false;
            foreach (DirectoryInfo directory in new DirectoryInfo(root).GetDirectories()
                .OrderByDescending(item => item.Name))
            {
                string requestPath = Path.Combine(directory.FullName, "generation-request.json");
                if (!File.Exists(requestPath)) continue;
                try
                {
                    string relativeDirectory = CandidateRoot + "/" + directory.Name;
                    if (!TryReadRequest(relativeDirectory, out ESAgentArtifactGenerationRequest candidate, out _)
                        || !string.Equals(candidate.spec.sourceGraphId, graphId, StringComparison.Ordinal)
                        || (contentSignature != null && !string.Equals(candidate.spec.SourceContentSignature,
                            contentSignature, StringComparison.Ordinal))) continue;
                    request = candidate;
                    requestDirectory = CandidateRoot + "/" + directory.Name;
                    return true;
                }
                catch { }
            }
            return false;
        }

        private static string NormalizeProjectRelativeDirectory(string fullPath)
        {
            string projectRoot = GetProjectRoot().TrimEnd(Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string normalized = Path.GetFullPath(fullPath ?? string.Empty);
            return normalized.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase)
                ? normalized.Substring(projectRoot.Length).Replace('\\', '/')
                : string.Empty;
        }

        private static string BuildSnapshotRelativePath(string graphId, string signature)
        {
            return SnapshotRoot + "/" + graphId + "/" + signature + ".json";
        }

        private static bool IsContentSignature(string value)
        {
            return !string.IsNullOrWhiteSpace(value) && value.Length == 64
                && value.All(character => (character >= '0' && character <= '9')
                    || (character >= 'a' && character <= 'f')
                    || (character >= 'A' && character <= 'F'));
        }

        public static string ResolveProjectPath(string relativePath)
        {
            if (!TryResolveProjectPath(relativePath, out string fullPath, out string error))
                throw new UnauthorizedAccessException(error);
            return fullPath;
        }

        public static bool TryResolveProjectPath(string relativePath, out string fullPath, out string error)
        {
            fullPath = string.Empty;
            string normalized = (relativePath ?? string.Empty).Replace('\\', '/').Trim();
            if (string.IsNullOrEmpty(normalized) || Path.IsPathRooted(normalized))
            {
                error = "路径必须是非空的项目相对路径。";
                return false;
            }
            foreach (string segment in normalized.Split('/'))
            {
                if (string.IsNullOrEmpty(segment) || segment == "." || segment == ".." || segment.IndexOf(':') >= 0)
                {
                    error = "路径包含非法片段：" + relativePath;
                    return false;
                }
            }
            string projectRoot = GetProjectRoot();
            string rootWithSeparator = projectRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            fullPath = Path.GetFullPath(Path.Combine(projectRoot, normalized.Replace('/', Path.DirectorySeparatorChar)));
            if (!fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
            {
                error = "路径越过项目根目录：" + relativePath;
                fullPath = string.Empty;
                return false;
            }
            if (ContainsExistingReparsePoint(projectRoot, fullPath))
            {
                error = "路径不能穿过 junction/symlink：" + relativePath;
                fullPath = string.Empty;
                return false;
            }
            error = string.Empty;
            return true;
        }

        public static string GetProjectRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        }

        private static bool ContainsExistingReparsePoint(string root, string candidate)
        {
            string rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string candidateFull = Path.GetFullPath(candidate);
            if (!string.Equals(candidateFull, rootFull, StringComparison.OrdinalIgnoreCase)
                && !candidateFull.StartsWith(rootFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                return true;
            string current = rootFull;
            string relative = candidateFull.Substring(rootFull.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            foreach (string segment in relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            {
                if (string.IsNullOrEmpty(segment)) continue;
                current = Path.Combine(current, segment);
                if (!Directory.Exists(current) && !File.Exists(current)) break;
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) return true;
            }
            return (File.Exists(rootFull) || Directory.Exists(rootFull))
                && (File.GetAttributes(rootFull) & FileAttributes.ReparsePoint) != 0;
        }

        internal static void EnsureProjectWritePath(string path)
        {
            string full = Path.GetFullPath(path);
            string projectRoot = GetProjectRoot().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!string.Equals(full, projectRoot, StringComparison.OrdinalIgnoreCase)
                && !full.StartsWith(projectRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("Agent Artifact 写入路径越出项目根目录：" + path);
            if (ContainsExistingReparsePoint(projectRoot, Path.GetDirectoryName(full) ?? full)
                || (File.Exists(full) && ContainsExistingReparsePoint(projectRoot, full)))
                throw new UnauthorizedAccessException("Agent Artifact 写入路径不能穿过 junction/symlink：" + path);
        }

        internal static void EnsureProjectReadPath(string path)
        {
            string full = Path.GetFullPath(path);
            string projectRoot = GetProjectRoot().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!string.Equals(full, projectRoot, StringComparison.OrdinalIgnoreCase)
                && !full.StartsWith(projectRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("Agent Artifact 读取路径越出项目根目录：" + path);
            if (ContainsExistingReparsePoint(projectRoot, full))
                throw new UnauthorizedAccessException("Agent Artifact 读取路径不能穿过 junction/symlink：" + path);
        }

        private static void DeleteCandidateDirectory(string path)
        {
            string full = Path.GetFullPath(path);
            string root = Path.GetFullPath(Path.Combine(GetProjectRoot(), CandidateRoot.Replace('/', Path.DirectorySeparatorChar)))
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("候选暂存清理路径越界：" + path);
            if (!Directory.Exists(full)) return;
            if (ContainsExistingReparsePoint(root, full))
                throw new UnauthorizedAccessException("候选暂存目录不能穿过 junction/symlink：" + path);
            ESManagedFileIO.DeleteDirectory(full, root);
        }

        public static string BuildPrompt(ESAgentArtifactGenerationRequest request)
        {
            var builder = new StringBuilder();
            builder.AppendLine("你正在执行 ESFramework Agent Artifact Generation 请求。");
            builder.AppendLine();
            builder.AppendLine("硬边界：");
            builder.AppendLine("1. 只允许在候选目录写文件：" + request.candidateDirectory);
            builder.AppendLine("2. 禁止直接写入 Assets/Plugins/ES/AICommands 或 .agents/skills。");
            builder.AppendLine("3. 禁止修改 Unity 运行时、生成的 .csproj、Git staging 或提交状态。");
            builder.AppendLine("4. 输出必须严格 UTF-8，先生成候选，等待用户在 Unity Diff/Review 窗口批准。");
            builder.AppendLine("5. 中文标题、描述、规则、路径和验收文本必须原样保留，不得转写、丢失或替换为 U+FFFD；允许使用中文文件名和中文目录名。");
            builder.AppendLine("6. AICommand 正文或 AISkill 的 SKILL.md 必须原样覆盖其 Graph 分支中的 Goal、Reference、Constraint、Branch、Traversal 与 Validation 语义；候选校验会逐项核对。" );
            builder.AppendLine();
            builder.AppendLine("必须使用项目专用生成合同：");
            builder.AppendLine("- AICommand: Assets/Plugins/ES/AICommands/生成_AgentArtifact候选_AI命令.md");
            builder.AppendLine("- Agent Skill: $es-generate-agent-artifacts");
            builder.AppendLine("- Skill contract: .agents/skills/es-generate-agent-artifacts/references/generation-contract.md");
            builder.AppendLine("先完整读取上述文件；它们不授权写入正式目录。");
            builder.AppendLine();
            builder.AppendLine("请先读取请求文件：" + request.requestDirectory + "/generation-request.json");
            builder.AppendLine("Source GraphId：" + request.spec.sourceGraphId);
            builder.AppendLine("Source OriginGraphId：" + request.spec.sourceOriginGraphId);
            builder.AppendLine("Source ContentSignature：" + request.spec.SourceContentSignature);
            AppendRiskAcceptance(builder, request.spec);
            builder.AppendLine("Goal：" + request.spec.goal.objective);
            if (!string.IsNullOrWhiteSpace(request.spec.goal.context)) builder.AppendLine("Context：" + request.spec.goal.context);
            if (!string.IsNullOrWhiteSpace(request.spec.goal.targetUsers)) builder.AppendLine("Target users / triggers：" + request.spec.goal.targetUsers);
            if (!string.IsNullOrWhiteSpace(request.spec.goal.successCriteria)) builder.AppendLine("Success criteria：" + request.spec.goal.successCriteria);
            AppendMindMap(builder, request.spec);
            AppendControlFlow(builder, request.spec);
            builder.AppendLine();
            builder.AppendLine("必须读取的 References：");
            foreach (ESAgentGenerationReference item in request.spec.references ?? Array.Empty<ESAgentGenerationReference>())
                builder.AppendLine("- [" + item.referenceKind + "] " + item.projectPath + " | " + item.purpose + " | required=" + item.required);
            builder.AppendLine();
            builder.AppendLine("Constraints：");
            builder.AppendLine("- precedence: Forbidden > Required > Permission > Quality; same kind uses descending priority.");
            foreach (ESAgentGenerationConstraint item in OrderedConstraints(request.spec))
            {
                builder.AppendLine("- [" + item.kind + "] scope=" + item.scope + ", combination="
                    + item.combinationMode + ", priority=" + item.priority
                    + (item.combinationMode == ESAgentConstraintCombinationMode.AnyOf
                        ? ", group=" + item.combinationGroup : string.Empty)
                    + " | " + item.statement);
                if (!string.IsNullOrWhiteSpace(item.rationale)) builder.AppendLine("  原因：" + item.rationale);
                if (!string.IsNullOrWhiteSpace(item.verification)) builder.AppendLine("  验证：" + item.verification);
            }
            builder.AppendLine();
            ESAgentSkillBundleContract bundle = request.spec.skillBundle;
            if (bundle != null)
            {
                builder.AppendLine("Skill 能力包（AICommand + AISkill 统一合同）：");
                builder.AppendLine("- bundleId: " + bundle.bundleId);
                builder.AppendLine("- 名称：" + bundle.displayName);
                builder.AppendLine("- 组成：" + bundle.kind);
                builder.AppendLine("- Goal NodeId：" + bundle.goalNodeId);
                builder.AppendLine("- AICommand Output：" + string.Join(", ", bundle.commandOutputNodeIds ?? Array.Empty<string>()));
                builder.AppendLine("- AISkill Output：" + string.Join(", ", bundle.aiSkillOutputNodeIds ?? Array.Empty<string>()));
                builder.AppendLine("- Branch：" + string.Join(", ", bundle.branchNodeIds ?? Array.Empty<string>()));
                builder.AppendLine("- Traversal：" + string.Join(", ", bundle.traversalNodeIds ?? Array.Empty<string>()));
                builder.AppendLine("- 同一能力包中的 AICommand 与 AISkill 必须共享目标、约束、输入语义、验证门禁和批准边界，不得漂移成无关产物。");
                builder.AppendLine();
            }
            builder.AppendLine("Outputs：");
            foreach (ESAgentGenerationOutput item in request.spec.outputs)
            {
                string marker = BuildArtifactIdentityMarker(item.artifactId);
                builder.AppendLine("- " + item.artifactKind + " | " + item.artifactName + " | target=" + item.targetProjectPath + " | " + item.requirements);
                builder.AppendLine("  identity: artifactId=" + item.artifactId + ", outputNodeId=" + item.nodeId
                    + ", requestedOperation=" + item.operationMode + " (" + OperationModeCaption(item.operationMode)
                    + "), resolvedOperation=" + item.resolvedOperation + " ("
                    + ResolvedOperationCaption(item.resolvedOperation) + ")");
                builder.AppendLine("  required marker: " + marker);
                if (item.artifactKind == ESAgentArtifactKind.AICommand)
                {
                    builder.AppendLine("  AICommand 候选正文必须原样包含 required marker；缺失或变更将被 Unity 候选校验拒绝。");
                    builder.AppendLine("  metadata: commandType=" + item.commandType + ", defaultWrite=" + item.defaultWrite + ", riskLevel=" + item.riskLevel);
                    builder.AppendLine("  AICommand 必须原样包含以下元数据行：");
                    builder.AppendLine("  命令类型：" + item.commandType);
                    builder.AppendLine("  默认改文件：" + item.defaultWrite);
                    builder.AppendLine("  风险等级：" + item.riskLevel);
                    builder.AppendLine("  semantic contract: intent=" + item.commandIntent + ", writeAuthorization="
                        + item.writeAuthorization + ", risk=" + item.commandRiskLevel + ", failurePolicy=" + item.failurePolicy);
                    builder.AppendLine("  expected inputs: " + item.expectedInputs);
                    builder.AppendLine("  preconditions: " + item.preconditions);
                    builder.AppendLine("  allowed write scopes: " + item.allowedWriteScopes);
                    builder.AppendLine("  forbidden operations: " + item.forbiddenOperations);
                    builder.AppendLine("  execution outline: " + item.executionOutline);
                    builder.AppendLine("  completion definition: " + item.acceptanceCriteria);
                    builder.AppendLine("  required evidence: " + item.requiredEvidence);
                    builder.AppendLine("  blocked handling: " + item.blockedHandling);
                    builder.AppendLine("  rollback strategy: " + item.rollbackStrategy);
                }
                else
                {
                    builder.AppendLine("  Agent Skill 的 required marker 必须放在 SKILL.md 的 YAML frontmatter 结束之后；其他附属文件不重复写 marker。");
                    builder.AppendLine("  skill: description=" + item.skillDescription + ", workflow=" + item.skillWorkflow
                        + ", openaiYaml=" + item.includeAgentsMetadata + ", references=" + item.includeReferences
                        + ", scripts=" + item.includeScripts + ", defaultPrompt=" + item.defaultPrompt);
                    builder.AppendLine("  semantic contract: effect=" + item.skillEffectKind + ", idempotency=" + item.skillIdempotency);
                    builder.AppendLine("  triggers: " + item.skillTriggerScenarios);
                    builder.AppendLine("  non-triggers: " + item.skillNonTriggerScenarios);
                    builder.AppendLine("  preconditions: " + item.skillPreconditions);
                    builder.AppendLine("  dependencies: " + item.skillRequiredDependencies);
                    builder.AppendLine("  input contract: " + item.skillInputContract);
                    builder.AppendLine("  output contract: " + item.skillOutputContract);
                    builder.AppendLine("  side effects: " + item.skillSideEffects);
                    builder.AppendLine("  non-goals: " + item.skillNonGoals);
                    builder.AppendLine("  failure recovery: " + item.skillFailureRecovery);
                    builder.AppendLine("  validation: " + item.skillValidationSteps);
                    builder.AppendLine("  permission boundary: " + item.skillPermissionBoundary);
                }
                if (item.resolvedOperation == ESAgentArtifactResolvedOperation.Update)
                    builder.AppendLine("  更新规则：只读现有正式目标，基于现有完整内容生成完整替换候选；禁止直接覆盖、局部追加或删除正式文件。");
                else
                    builder.AppendLine("  创建规则：在 candidate/ 中生成完整新候选；禁止提前创建正式目标。");
            }
            builder.AppendLine();
            builder.AppendLine("Validation gates：");
            foreach (ESAgentGenerationValidation item in request.spec.validations ?? Array.Empty<ESAgentGenerationValidation>())
            {
                builder.AppendLine("- AICommand=" + item.validateAICommand + ", AgentSkill=" + item.validateAgentSkill
                    + ", UTF8=" + item.validateUtf8 + ", DiffReview=" + item.requireDiffReview
                    + ", HumanApproval=" + item.requireHumanApproval);
                if (!string.IsNullOrWhiteSpace(item.additionalRequirements)) builder.AppendLine("  附加要求：" + item.additionalRequirements);
                if (!string.IsNullOrWhiteSpace(item.reviewChecklist)) builder.AppendLine("  Review 清单：" + item.reviewChecklist);
            }
            builder.AppendLine();
            builder.AppendLine("在 candidate/ 下生成候选文件，并创建 candidate-manifest.json：");
            builder.AppendLine("{\"schemaVersion\":1,\"requestId\":\"" + request.requestId + "\",\"summary\":\"...\",\"files\":[{\"artifactKind\":0,\"candidateRelativePath\":\"candidate/command.md\",\"targetProjectPath\":\"Assets/Plugins/ES/AICommands/...md\",\"summary\":\"...\"}]}");
            builder.AppendLine("artifactKind: 0=AICommand, 1=AgentSkill。AgentSkill 的每个文件都必须列入 files。");
            builder.AppendLine("candidate-manifest.json 中的 targetProjectPath 必须与对应 Output 的已解析正式路径一致。ArtifactId 不放在路径里，而由正文 marker 建立稳定绑定。");
            builder.AppendLine("同时创建 validation-report.md，说明已执行和未执行的验证；不得声称用户已经批准。");
            return builder.ToString();
        }

        internal static IEnumerable<ESAgentGenerationConstraint> OrderedConstraints(
            ESAgentArtifactGenerationSpec spec)
        {
            return (spec?.constraints ?? Array.Empty<ESAgentGenerationConstraint>())
                .Where(item => item != null)
                .OrderByDescending(item => ESAgentSemanticPresentation.ConstraintKindPrecedence(item.kind))
                .ThenByDescending(item => item.priority)
                .ThenBy(item => item.nodeId, StringComparer.Ordinal);
        }

        private static string OperationModeCaption(ESAgentArtifactOperationMode value)
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

        private static string ResolvedOperationCaption(ESAgentArtifactResolvedOperation value)
        {
            return value == ESAgentArtifactResolvedOperation.Update ? "更新已有正式产物" : "创建新正式产物";
        }

        private static void AppendMindMap(StringBuilder builder, ESAgentArtifactGenerationSpec spec)
        {
            ESAgentGenerationRelation[] relations = (spec.relations
                    ?? Array.Empty<ESAgentGenerationRelation>())
                .OrderBy(relation => relation.order)
                .ThenBy(relation => relation.edgeId, StringComparer.Ordinal)
                .ToArray();
            builder.AppendLine();
            builder.AppendLine("思路图关系（这是需求归属、约束作用和审查链，不是运行时执行图）：");
            if (relations.Length == 0)
            {
                builder.AppendLine("- 无关系数据；拒绝猜测节点归属。 ");
                return;
            }
            for (int i = 0; i < relations.Length; i++)
            {
                ESAgentGenerationRelation relation = relations[i];
                builder.AppendLine("- 顺序 " + relation.order + " | "
                    + SafeTitle(relation.fromNodeTitle, relation.fromNodeTypeId) + " → "
                    + SafeTitle(relation.toNodeTitle, relation.toNodeTypeId) + " ["
                    + ESAgentSemanticPresentation.RelationKind(relation.relationKind)
                    + " / " + relation.fromPortMeaning + " (" + relation.fromPortStableKey + ")"
                    + " → " + relation.toPortMeaning + " (" + relation.toPortStableKey + ")"
                    + " / " + relation.semanticType + "]");
            }
            builder.AppendLine();
            builder.AppendLine("```mermaid");
            builder.AppendLine("flowchart LR");
            var aliases = new Dictionary<string, string>(StringComparer.Ordinal);
            int nextAlias = 0;
            for (int i = 0; i < relations.Length; i++)
            {
                ESAgentGenerationRelation relation = relations[i];
                string fromAlias = GetAlias(aliases, relation.fromNodeId, ref nextAlias);
                string toAlias = GetAlias(aliases, relation.toNodeId, ref nextAlias);
                builder.AppendLine("    " + fromAlias + "[\"" + EscapeMermaid(SafeTitle(relation.fromNodeTitle, relation.fromNodeTypeId)) + "\"]");
                builder.AppendLine("    " + toAlias + "[\"" + EscapeMermaid(SafeTitle(relation.toNodeTitle, relation.toNodeTypeId)) + "\"]");
                builder.AppendLine("    " + fromAlias + " -->|"
                    + EscapeMermaid("#" + relation.order + " "
                        + ESAgentSemanticPresentation.RelationKind(relation.relationKind)
                        + ":" + relation.fromPortMeaning + "→" + relation.toPortMeaning + " ["
                        + (relation.sourceValueTypeId ?? relation.semanticType ?? string.Empty)
                        + "→" + (relation.targetValueTypeId ?? string.Empty)
                        + ";" + relation.sourceAggregation + "→" + relation.targetAggregation + "]")
                    + "| " + toAlias);
            }
            builder.AppendLine("```");
        }

        private static void AppendControlFlow(StringBuilder builder, ESAgentArtifactGenerationSpec spec)
        {
            ESAgentGenerationBranch[] branches = spec.branches ?? Array.Empty<ESAgentGenerationBranch>();
            ESAgentGenerationTraversal[] traversals = spec.traversals
                ?? Array.Empty<ESAgentGenerationTraversal>();
            if (branches.Length == 0 && traversals.Length == 0)
                return;

            builder.AppendLine();
            builder.AppendLine("结构化分支与有界遍历（必须按 Relations 的出口执行，不得扁平化或猜测）：");
            for (int i = 0; i < branches.Length; i++)
            {
                ESAgentGenerationBranch branch = branches[i];
                builder.AppendLine("- Branch " + branch.nodeId + " | 条件：" + branch.condition);
                builder.AppendLine("  " + ESAgentGraphStableIds.BranchMatchedPortKey + "："
                    + branch.matchedPath + " -> " + FormatTargetIds(branch.matchedTargetNodeIds));
                builder.AppendLine("  " + ESAgentGraphStableIds.BranchDefaultPortKey + "："
                    + branch.defaultPath + " -> " + FormatTargetIds(branch.defaultTargetNodeIds));
                builder.AppendLine("  " + ESAgentGraphStableIds.BranchFailurePortKey + "："
                    + branch.failurePath + " -> " + FormatTargetIds(branch.failureTargetNodeIds));
            }
            for (int i = 0; i < traversals.Length; i++)
            {
                ESAgentGenerationTraversal traversal = traversals[i];
                builder.AppendLine("- Traversal " + traversal.nodeId + " | 目标：" + traversal.target
                    + " | item=" + traversal.itemAlias + " | order=" + traversal.order
                    + " | maxDepth=" + traversal.maxDepth + " | maxItems=" + traversal.maxItems);
                builder.AppendLine("  停止条件：" + traversal.stopCondition);
                builder.AppendLine("  空结果：" + traversal.emptyResultAction);
                builder.AppendLine("  失败：" + traversal.failureAction);
            }
            builder.AppendLine("遍历不得创建 Graph 循环；达到任一硬上限必须停止并沿完成或失败出口交付证据。");
        }

        private static string FormatTargetIds(IEnumerable<string> targetNodeIds)
        {
            string[] ids = (targetNodeIds ?? Array.Empty<string>())
                .Where(id => !string.IsNullOrWhiteSpace(id)).ToArray();
            return ids.Length == 0 ? "（缺少稳定目标）" : string.Join("、", ids);
        }

        private static string GetAlias(Dictionary<string, string> aliases, string nodeId, ref int nextAlias)
        {
            string key = nodeId ?? string.Empty;
            if (aliases.TryGetValue(key, out string alias)) return alias;
            alias = "N" + nextAlias++;
            aliases[key] = alias;
            return alias;
        }

        private static string SafeTitle(string title, string fallback)
        {
            return string.IsNullOrWhiteSpace(title) ? fallback ?? "Node" : title.Trim();
        }

        private static string EscapeMermaid(string value)
        {
            return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "'")
                .Replace("\r", " ").Replace("\n", " ").Replace("|", "/");
        }

        private static string BuildRequestReadme(ESAgentArtifactGenerationRequest request)
        {
            return "# Agent Artifact Candidate\n\nRequest: `" + request.requestId
                + "`\n\n候选文件只允许进入 `candidate/`。正式目录写入必须通过 Unity Review 窗口人工批准。\n";
        }

        internal static void WriteUtf8(string path, string text)
        {
            ESAgentArtifactGenerationWorkspace.EnsureProjectWritePath(path);
            ESManagedFileIO.WriteTextAtomic(path, text, new UTF8Encoding(false, true), GetProjectRoot());
        }

        internal static void CopyFileAtomically(string sourcePath, string targetPath)
        {
            EnsureProjectWritePath(sourcePath);
            EnsureProjectWritePath(targetPath);
            ESManagedFileIO.CopyFileAtomic(sourcePath, targetPath, GetProjectRoot());
        }

        internal static string ReadUtf8(string path)
        {
            return new UTF8Encoding(false, true).GetString(File.ReadAllBytes(path));
        }

        internal static string ComputeSha256(string path)
        {
            using (SHA256 sha256 = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
            {
                byte[] hash = sha256.ComputeHash(stream);
                var builder = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++) builder.Append(hash[i].ToString("x2"));
                return builder.ToString();
            }
        }
    }

    public static class ESAgentArtifactImportTransaction
    {
        private sealed class OriginalFileState
        {
            public bool existed;
            public string backupPath;
            public string sha256;
        }

        public static ESAgentArtifactImportResult Execute(
            IReadOnlyList<ESAgentArtifactFileOperation> operations,
            IESAgentArtifactFileIO fileIO,
            Action postApply)
        {
            var result = new ESAgentArtifactImportResult
            {
                State = ESAgentArtifactImportState.FailedBeforeWrite,
                PrimaryError = string.Empty
            };
            if (operations == null || operations.Count == 0)
            {
                result.PrimaryError = "没有可导入的正式文件。";
                return result;
            }
            if (fileIO == null)
            {
                result.PrimaryError = "正式文件导入服务不可用。";
                return result;
            }

            var originals = new Dictionary<string, OriginalFileState>(StringComparer.OrdinalIgnoreCase);
            var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (ESAgentArtifactFileOperation operation in operations)
                {
                    if (operation == null || string.IsNullOrWhiteSpace(operation.SourcePath)
                        || string.IsNullOrWhiteSpace(operation.TargetPath))
                        throw new InvalidOperationException("正式导入文件操作缺少源路径或目标路径。");
                    if (!targets.Add(operation.TargetPath))
                        throw new InvalidOperationException("正式导入包含重复目标：" + operation.TargetPath);
                    if (!fileIO.FileExists(operation.SourcePath))
                        throw new FileNotFoundException("候选源文件不存在。", operation.SourcePath);

                    bool existed = fileIO.FileExists(operation.TargetPath);
                    if (existed && string.IsNullOrWhiteSpace(operation.BackupPath))
                        throw new InvalidOperationException("已有正式文件缺少备份路径：" + operation.TargetPath);
                    originals[operation.TargetPath] = new OriginalFileState
                    {
                        existed = existed,
                        backupPath = operation.BackupPath,
                        sha256 = existed ? fileIO.ComputeSha256(operation.TargetPath) : string.Empty
                    };
                }

                foreach (ESAgentArtifactFileOperation operation in operations)
                {
                    OriginalFileState original = originals[operation.TargetPath];
                    if (!original.existed) continue;
                    fileIO.CopyAtomically(operation.TargetPath, original.backupPath);
                }
            }
            catch (Exception exception)
            {
                result.PrimaryError = "导入准备失败，正式文件尚未写入：" + exception.Message;
                return result;
            }

            var touched = new List<ESAgentArtifactFileOperation>();
            try
            {
                foreach (ESAgentArtifactFileOperation operation in operations)
                {
                    // 即使 CopyAtomically 在目标处于半写状态时抛错，也必须将该文件纳入恢复。
                    touched.Add(operation);
                    fileIO.CopyAtomically(operation.SourcePath, operation.TargetPath);
                }
                postApply?.Invoke();
                result.State = ESAgentArtifactImportState.Applied;
                result.PrimaryError = string.Empty;
                return result;
            }
            catch (Exception exception)
            {
                result.PrimaryError = "正式导入失败：" + exception.Message;
                var recoveryErrors = new List<string>();
                foreach (ESAgentArtifactFileOperation operation in touched)
                {
                    OriginalFileState original = originals[operation.TargetPath];
                    if (original.existed)
                    {
                        try
                        {
                            if (!fileIO.FileExists(original.backupPath))
                                throw new FileNotFoundException("备份文件不存在。", original.backupPath);
                            fileIO.CopyAtomically(original.backupPath, operation.TargetPath);
                        }
                        catch (Exception recoveryException)
                        {
                            recoveryErrors.Add(operation.TargetPath + "：恢复失败 - " + recoveryException.Message);
                        }

                        try
                        {
                            if (!fileIO.FileExists(operation.TargetPath))
                                throw new InvalidOperationException("恢复后目标文件不存在。");
                            string actual = fileIO.ComputeSha256(operation.TargetPath);
                            if (!string.Equals(actual, original.sha256, StringComparison.OrdinalIgnoreCase))
                                throw new InvalidOperationException("恢复后 SHA-256 不匹配，期望 "
                                    + original.sha256 + "，实际 " + actual + "。");
                        }
                        catch (Exception verificationException)
                        {
                            recoveryErrors.Add(operation.TargetPath + "：恢复核对失败 - " + verificationException.Message);
                        }
                    }
                    else
                    {
                        try
                        {
                            if (fileIO.FileExists(operation.TargetPath))
                                fileIO.DeleteFile(operation.TargetPath);
                        }
                        catch (Exception recoveryException)
                        {
                            recoveryErrors.Add(operation.TargetPath + "：删除新文件失败 - " + recoveryException.Message);
                        }
                        try
                        {
                            if (fileIO.FileExists(operation.TargetPath))
                                throw new InvalidOperationException("新文件删除后仍然存在。");
                        }
                        catch (Exception verificationException)
                        {
                            recoveryErrors.Add(operation.TargetPath + "：删除核对失败 - " + verificationException.Message);
                        }
                    }
                }
                result.RecoveryErrors = recoveryErrors.ToArray();
                result.State = recoveryErrors.Count == 0
                    ? ESAgentArtifactImportState.RolledBack
                    : ESAgentArtifactImportState.RollbackUnconfirmed;
                return result;
            }
        }
    }

    /// <summary>
    /// 只在用户显式点击后，把已经人工批准且哈希未变化的 AICommand 交给项目权威会话启动器。
    /// 候选生成、人工批准和真实实现始终是三个独立动作。
    /// </summary>
    public static class ESAgentImplementationSessionLauncher
    {
        private const string ApprovalManifestFileName = "approval-manifest.json";
        private const string ImplementationTaskFileName = "implementation-task.md";
        private const string LauncherRelativePath = ".agents/skills/es-codex-session-bootstrap/scripts/Start-ESCodexSession.ps1";
        private const string ResponsibilityKey = "graph-implementation";
        private const string TabTitle = "ES·Graph实现";
        private static ESManagedEditorProcess activeProcess;
        private static Action<string> activeReport;

        public static bool IsLaunching => activeProcess != null;

        public static bool CanLaunchApprovedImplementation(ESAgentArtifactGenerationSpec currentSpec,
            out string error)
        {
            return TryPrepareApprovedImplementation(currentSpec, out _, out _, out _, out _, out error);
        }

        public static bool TryLaunchApprovedImplementation(ESAgentArtifactGenerationSpec currentSpec,
            Action<string> report, out string error)
        {
            error = string.Empty;
            if (IsLaunching)
            {
                error = "正在启动实现窗口，请稍候。";
                return false;
            }
            if (!TryPrepareApprovedImplementation(currentSpec, out string requestDirectory,
                    out ESAgentArtifactGenerationRequest request, out ESAgentArtifactApprovalManifest approval,
                    out string[] approvedCommands, out error))
                return false;

            string objective = request.spec?.goal?.objective ?? "当前 Graph 实现任务";
            if (!EditorUtility.DisplayDialog("打开独立实现窗口",
                    "将打开一个独立 Codex 窗口并执行已批准的 AICommand。\n\n"
                    + "实现目标：" + objective + "\n"
                    + "AICommand：" + approvedCommands[0] + "\n\n"
                    + "新窗口可在 AICommand 与 Graph 约束的交集内修改项目，并执行相称验证；"
                    + "不会获得 Git 提交、发布、历史/审计写入或删除权限。",
                    "打开并执行", "取消"))
            {
                error = "已取消打开实现窗口。";
                return false;
            }

            string requestFull = ESAgentArtifactGenerationWorkspace.ResolveProjectPath(requestDirectory);
            string implementationTaskPath = Path.Combine(requestFull, ImplementationTaskFileName);
            string implementationTask = BuildImplementationTask(request, approval, approvedCommands);
            try
            {
                ESAgentArtifactGenerationWorkspace.WriteUtf8(implementationTaskPath, implementationTask);
                string[] handoffPaths = BuildHandoffPaths(requestDirectory, approvedCommands);
                string taskPrompt = BuildTaskPrompt();
                string taskKey = BuildTaskKey(currentSpec.SourceContentSignature, request.requestId);
                string command = BuildPowerShellCommand(handoffPaths, taskPrompt, taskKey);
                StartLauncher(command, report);
                report?.Invoke("正在验证会话启动环境，并打开独立实现窗口……");
                return true;
            }
            catch (Exception exception)
            {
                CleanupProcessState();
                error = "打开实现窗口失败：" + exception.Message;
                return false;
            }
        }

        public static string BuildImplementationTask(ESAgentArtifactGenerationRequest request,
            ESAgentArtifactApprovalManifest approval, IReadOnlyList<string> approvedCommands)
        {
            var builder = new StringBuilder();
            builder.AppendLine("# ES Graph 已批准实现任务");
            builder.AppendLine();
            builder.AppendLine("状态：候选已经通过 Unity Diff Review 与人工批准；本文件用于独立 Codex 实现窗口。 ");
            builder.AppendLine("请求：`" + (request?.requestId ?? string.Empty) + "`");
            builder.AppendLine("批准时间：`" + (approval?.approvedAtUtc ?? string.Empty) + "`");
            builder.AppendLine("GraphId：`" + (approval?.sourceGraphId ?? string.Empty) + "`");
            builder.AppendLine("Graph 签名：`" + (approval?.sourceContentSignature ?? string.Empty) + "`");
            if (approval?.riskAcceptance != null)
            {
                builder.AppendLine("风险确认：`" + approval.riskAcceptance.acceptanceHash + "`");
                builder.AppendLine("已确认问题："
                    + string.Join(", ", approval.riskAcceptance.issueCodes ?? Array.Empty<string>()));
                builder.AppendLine("确认者 / 时间：" + approval.riskAcceptance.acceptedBy
                    + " / " + approval.riskAcceptance.acceptedAtUtc);
            }
            builder.AppendLine();
            if (request?.spec?.skillBundle != null)
            {
                ESAgentSkillBundleContract bundle = request.spec.skillBundle;
                builder.AppendLine("## Skill 能力包身份");
                builder.AppendLine();
                builder.AppendLine("- bundleId：`" + bundle.bundleId + "`");
                builder.AppendLine("- 组成：" + bundle.kind + "（AICommand + AISkill 共享同一目标与批准边界）");
                builder.AppendLine("- AICommand Output：" + string.Join(", ", bundle.commandOutputNodeIds ?? Array.Empty<string>()));
                builder.AppendLine("- AISkill Output：" + string.Join(", ", bundle.aiSkillOutputNodeIds ?? Array.Empty<string>()));
                builder.AppendLine();
            }
            builder.AppendLine("## 当前用户授权");
            builder.AppendLine();
            builder.AppendLine("- 用户已在 Unity 中显式点击“打开新窗口执行实现”。");
            builder.AppendLine("- 允许在唯一已批准 AICommand 与下列 Graph 约束的交集内修改其声明的项目源码/资产，并完成相称的本地编译或 Unity 验证。");
            builder.AppendLine("- 不授权 Git stage/commit/push、AI 历史或审计状态写入、发布、上传或删除。");
            builder.AppendLine("- 必须直接完成实现与验证，不得只输出方案或伪造已运行证据。");
            builder.AppendLine();
            builder.AppendLine("## 唯一 AICommand 合同");
            builder.AppendLine();
            foreach (string command in approvedCommands ?? Array.Empty<string>())
                builder.AppendLine("- `" + command + "`");

            ESAgentArtifactGenerationSpec spec = request?.spec;
            if (spec?.goal != null)
            {
                builder.AppendLine();
                builder.AppendLine("## 实现目标");
                builder.AppendLine();
                AppendValue(builder, "标题", spec.goal.title);
                AppendValue(builder, "目标", spec.goal.objective);
                AppendValue(builder, "上下文", spec.goal.context);
                AppendValue(builder, "面向对象/触发场景", spec.goal.targetUsers);
                AppendValue(builder, "成功标准", spec.goal.successCriteria);
            }

            builder.AppendLine();
            builder.AppendLine("## 权威参考");
            builder.AppendLine();
            foreach (ESAgentGenerationReference item in spec?.references ?? Array.Empty<ESAgentGenerationReference>())
                builder.AppendLine("- [" + item.referenceKind + "] `" + item.projectPath + "`：" + item.purpose
                    + "（required=" + item.required + "）");

            builder.AppendLine();
            builder.AppendLine("## 约束");
            builder.AppendLine();
            builder.AppendLine("固定裁决顺序：Forbidden > Required > Permission > Quality；同类型按优先级从高到低。\n");
            foreach (ESAgentGenerationConstraint item in ESAgentArtifactGenerationWorkspace.OrderedConstraints(spec))
            {
                builder.AppendLine("- [" + item.kind + " | "
                    + ESAgentSemanticPresentation.ConstraintScope(item.scope) + " | "
                    + ESAgentSemanticPresentation.ConstraintCombination(item.combinationMode)
                    + " | priority=" + item.priority + "] " + item.statement);
                if (item.combinationMode == ESAgentConstraintCombinationMode.AnyOf)
                    AppendIndentedValue(builder, "组合组", item.combinationGroup);
                AppendIndentedValue(builder, "原因", item.rationale);
                AppendIndentedValue(builder, "验证", item.verification);
            }

            builder.AppendLine();
            builder.AppendLine("## 实现链");
            builder.AppendLine();
            foreach (ESAgentGenerationOutput item in spec?.outputs ?? Array.Empty<ESAgentGenerationOutput>())
            {
                if (item.artifactKind != ESAgentArtifactKind.AICommand) continue;
                AppendValue(builder, "命令名称", item.artifactName);
                AppendValue(builder, "用途", item.requirements);
                AppendValue(builder, "任务意图", ESAgentSemanticPresentation.CommandIntent(item.commandIntent));
                AppendValue(builder, "写入授权", ESAgentSemanticPresentation.WriteAuthorization(item.writeAuthorization));
                AppendValue(builder, "风险等级", ESAgentSemanticPresentation.RiskLevel(item.commandRiskLevel));
                AppendValue(builder, "失败策略", ESAgentSemanticPresentation.FailurePolicy(item.failurePolicy));
                AppendValue(builder, "预期输入", item.expectedInputs);
                AppendValue(builder, "前置条件", item.preconditions);
                AppendValue(builder, "允许写入范围", item.allowedWriteScopes);
                AppendValue(builder, "禁止操作", item.forbiddenOperations);
                AppendValue(builder, "执行步骤", item.executionOutline);
                AppendValue(builder, "完成定义", item.acceptanceCriteria);
                AppendValue(builder, "必须证据", item.requiredEvidence);
                AppendValue(builder, "阻断处理", item.blockedHandling);
                AppendValue(builder, "回滚要求", item.rollbackStrategy);
            }

            builder.AppendLine();
            builder.AppendLine("## 验证门禁");
            builder.AppendLine();
            foreach (ESAgentGenerationValidation item in spec?.validations ?? Array.Empty<ESAgentGenerationValidation>())
            {
                builder.AppendLine("- UTF-8=" + item.validateUtf8 + "，Diff Review=" + item.requireDiffReview
                    + "，人工批准=" + item.requireHumanApproval);
                AppendIndentedValue(builder, "附加要求", item.additionalRequirements);
                AppendIndentedValue(builder, "复核清单", item.reviewChecklist);
            }

            builder.AppendLine();
            builder.AppendLine("## 执行顺序");
            builder.AppendLine();
            builder.AppendLine("1. 验证启动信封，只读取本次信封中的私有 handoff 快照。");
            builder.AppendLine("2. 使用 `$es-use-ai-command`，把上述唯一 AICommand 私有快照作为本次权限合同。");
            builder.AppendLine("3. 重新检查当前 branch、HEAD、工作树和目标源码，保护其他人的并行改动。");
            builder.AppendLine("4. 按实现链直接完成修改，并按证据层级执行验证。");
            builder.AppendLine("5. 中文报告改动文件、验证证据、未完成项和剩余风险。");
            return builder.ToString();
        }

        private static bool TryPrepareApprovedImplementation(ESAgentArtifactGenerationSpec currentSpec,
            out string requestDirectory, out ESAgentArtifactGenerationRequest request,
            out ESAgentArtifactApprovalManifest approval, out string[] approvedCommands, out string error)
        {
            requestDirectory = string.Empty;
            request = null;
            approval = null;
            approvedCommands = Array.Empty<string>();
            if (currentSpec == null || string.IsNullOrWhiteSpace(currentSpec.SourceContentSignature))
            {
                error = "当前 Graph 没有可核对的内容签名，请先修复并重新烘焙。";
                return false;
            }
            if (!ESAgentArtifactGenerationWorkspace.TryGetRequestDirectory(currentSpec, out requestDirectory))
            {
                error = "当前 Graph 没有匹配其 GraphId 与内容签名的候选请求。请重新生成候选。";
                return false;
            }

            string requestFull = ESAgentArtifactGenerationWorkspace.ResolveProjectPath(requestDirectory);
            string requestPath = Path.Combine(requestFull, "generation-request.json");
            string candidateManifestPath = Path.Combine(requestFull, "candidate-manifest.json");
            if (!ESAgentArtifactGenerationWorkspace.TryGetCandidateManifestPath(requestFull,
                    out candidateManifestPath))
                candidateManifestPath = Path.Combine(requestFull, "candidate-manifest.json");
            string approvalPath = Path.Combine(requestFull, ApprovalManifestFileName);
            if (!File.Exists(requestPath) || !File.Exists(candidateManifestPath) || !File.Exists(approvalPath))
            {
                error = "最新请求尚未完成候选生成与人工批准。请先查看候选差异并批准导入。";
                return false;
            }

            try
            {
                request = JsonUtility.FromJson<ESAgentArtifactGenerationRequest>(
                    ESAgentArtifactGenerationWorkspace.ReadUtf8(requestPath));
                ESAgentArtifactCandidateManifest candidate = JsonUtility.FromJson<ESAgentArtifactCandidateManifest>(
                    ESAgentArtifactGenerationWorkspace.ReadUtf8(candidateManifestPath));
                approval = JsonUtility.FromJson<ESAgentArtifactApprovalManifest>(
                    ESAgentArtifactGenerationWorkspace.ReadUtf8(approvalPath));
                if (request?.spec == null || request.schemaVersion != ESAgentArtifactGenerationRequest.CurrentSchemaVersion
                    || request.spec.contractSchemaVersion != ESAgentArtifactGenerationSpec.CurrentContractSchemaVersion
                    || candidate == null || approval == null
                    || approval.schemaVersion != ESAgentArtifactApprovalManifest.CurrentSchemaVersion
                    || !string.Equals(request.requestId, candidate.requestId, StringComparison.Ordinal)
                    || !string.Equals(request.requestId, approval.requestId, StringComparison.Ordinal))
                {
                    error = "请求、候选和批准清单的身份不一致。";
                    return false;
                }
                if (!ESAgentGenerationIntentValidator.TryValidate(request.spec, out string intentError))
                {
                    error = "请求的开发意图合同无效：" + intentError;
                    return false;
                }
                if (!ESAgentGenerationRiskValidator.TryValidate(request.spec, out string semanticError))
                {
                    error = "请求的目标、输出或风险确认无效：" + semanticError;
                    return false;
                }
                if ((request.spec.riskAcceptance == null) != (approval.riskAcceptance == null)
                    || request.spec.riskAcceptance != null
                    && !request.spec.riskAcceptance.SameAs(approval.riskAcceptance))
                {
                    error = "批准清单没有绑定请求中的风险确认，必须重新进行 Diff Review 与人工批准。";
                    return false;
                }
                if (!string.Equals(currentSpec.sourceGraphId, request.spec.sourceGraphId, StringComparison.Ordinal)
                    || !string.Equals(currentSpec.sourceGraphId, approval.sourceGraphId, StringComparison.Ordinal))
                {
                    error = "当前 GraphId 与请求或批准清单不一致。另存为副本不会继承旧图的批准权限。";
                    return false;
                }
                if (!string.Equals(currentSpec.SourceContentSignature, request.spec.SourceContentSignature,
                        StringComparison.Ordinal)
                    || !string.Equals(currentSpec.SourceContentSignature, approval.sourceContentSignature,
                        StringComparison.Ordinal))
                {
                    error = "Graph 在候选批准后已经变化。请重新生成候选并再次人工批准。";
                    return false;
                }

                ESAgentArtifactApprovedFile[] approvedFiles = approval.files ?? Array.Empty<ESAgentArtifactApprovedFile>();
                ESAgentArtifactCandidateFile[] candidateFiles = candidate.files ?? Array.Empty<ESAgentArtifactCandidateFile>();
                if (approvedFiles.Length == 0 || approvedFiles.Length != candidateFiles.Length)
                {
                    error = "批准清单不完整，不能启动真实实现。";
                    return false;
                }

                var commands = new List<string>();
                foreach (ESAgentArtifactApprovedFile approved in approvedFiles)
                {
                    ESAgentArtifactCandidateFile candidateFile = candidateFiles.FirstOrDefault(item => item != null
                        && item.artifactKind == approved.artifactKind
                        && string.Equals(Normalize(item.targetProjectPath), Normalize(approved.targetProjectPath),
                            StringComparison.Ordinal));
                    if (candidateFile == null)
                    {
                        error = "批准文件不在候选清单中：" + approved.targetProjectPath;
                        return false;
                    }
                    if (!ESAgentArtifactCandidateValidator.TryGetDeclaredOutput(request.spec, candidateFile,
                            out ESAgentGenerationOutput output, out error))
                        return false;
                    if (!string.Equals(approved.sourceGraphId, currentSpec.sourceGraphId, StringComparison.Ordinal)
                        || !string.Equals(approved.outputNodeId, output.nodeId, StringComparison.Ordinal)
                        || !string.Equals(approved.artifactId, output.artifactId, StringComparison.Ordinal))
                    {
                        error = "批准文件的 GraphId、输出节点或 ArtifactId 已失配：" + approved.targetProjectPath;
                        return false;
                    }
                    if (!ESAgentArtifactCandidateValidator.TryResolveFormalTarget(request, candidateFile,
                            out string targetPath, out error))
                        return false;
                    if (!File.Exists(targetPath))
                    {
                        error = "已批准的正式文件不存在：" + approved.targetProjectPath;
                        return false;
                    }
                    string currentHash = ESAgentArtifactGenerationWorkspace.ComputeSha256(targetPath);
                    if (!string.Equals(currentHash, approved.sha256, StringComparison.OrdinalIgnoreCase))
                    {
                        error = "正式文件在批准后已经变化，请重新 Diff Review：" + approved.targetProjectPath;
                        return false;
                    }
                    if (ESAgentArtifactCandidateValidator.RequiresIdentityMarker(candidateFile)
                        && (!ESAgentArtifactGenerationWorkspace.TryReadArtifactIdentity(targetPath,
                                out string formalArtifactId)
                            || !string.Equals(formalArtifactId, output.artifactId, StringComparison.Ordinal)))
                    {
                        error = "正式产物的稳定 ArtifactId 缺失或已变化，请重新生成并批准："
                            + approved.targetProjectPath;
                        return false;
                    }
                    if (approved.artifactKind == ESAgentArtifactKind.AICommand)
                        commands.Add(Normalize(approved.targetProjectPath));
                }
                if (commands.Count != 1)
                {
                    error = commands.Count == 0
                        ? "当前已批准产物没有 AICommand，无法启动真实实现窗口。"
                        : "一次实现窗口只能执行一个 AICommand，请拆分 Graph 输出。";
                    return false;
                }
                approvedCommands = commands.ToArray();
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                error = "读取批准状态失败：" + exception.Message;
                return false;
            }
        }

        private static string[] BuildHandoffPaths(string requestDirectory, IReadOnlyList<string> approvedCommands)
        {
            var paths = new List<string>
            {
                requestDirectory + "/" + ImplementationTaskFileName,
                requestDirectory + "/generation-request.json",
                requestDirectory + "/" + ApprovalManifestFileName
            };
            string approvalReport = requestDirectory + "/approval-report.md";
            if (File.Exists(ESAgentArtifactGenerationWorkspace.ResolveProjectPath(approvalReport)))
                paths.Add(approvalReport);
            paths.AddRange(approvedCommands);
            return paths.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }

        private static string BuildTaskPrompt()
        {
            return "执行本次不可变 handoff 中 implementation-task.md 描述的已批准 Graph 实现任务。"
                + "把其中列出的唯一 AICommand 私有快照作为本次任务合同，并使用 $es-use-ai-command。"
                + "用户已通过 Unity 按钮明确授权在 AICommand 与 Graph 约束交集内修改项目源码/资产，"
                + "并执行相称的本地编译或 Unity 验证；不授权 Git 操作、历史/审计写入、发布、上传或删除。"
                + "请直接完成实现，不要只给方案。";
        }

        private static string BuildTaskKey(string signature, string requestId)
        {
            string source = string.IsNullOrWhiteSpace(signature) ? requestId ?? "unknown" : signature;
            return "graph-implementation-" + source.Substring(0, Math.Min(16, source.Length));
        }

        private static string BuildPowerShellCommand(IReadOnlyList<string> handoffPaths, string taskPrompt,
            string taskKey)
        {
            string root = ESAgentArtifactGenerationWorkspace.GetProjectRoot();
            string launcher = Path.Combine(root, LauncherRelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(launcher)) throw new FileNotFoundException("项目会话启动器不存在。", launcher);
            string handoff = "@(" + string.Join(",", handoffPaths.Select(QuotePowerShell)) + ")";
            var builder = new StringBuilder();
            builder.AppendLine("$ErrorActionPreference = 'Stop'");
            builder.AppendLine("[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)");
            builder.AppendLine("$launcher = " + QuotePowerShell(launcher));
            builder.AppendLine("$handoff = " + handoff);
            builder.AppendLine("& $launcher -Mode Validate -DryRun -ProjectPath " + QuotePowerShell(root)
                + " -TerminalMode ProjectWindow -HandoffPath $handoff | Out-Null");
            builder.AppendLine("$result = & $launcher -Mode New -ProjectPath " + QuotePowerShell(root)
                + " -TaskKey " + QuotePowerShell(taskKey)
                + " -ResponsibilityKey " + QuotePowerShell(ResponsibilityKey)
                + " -TabTitle " + QuotePowerShell(TabTitle)
                + " -TerminalMode ProjectWindow -HandoffPath $handoff -TaskPrompt " + QuotePowerShell(taskPrompt));
            builder.AppendLine("$result | ConvertTo-Json -Depth 8 -Compress");
            return builder.ToString();
        }

        private static void StartLauncher(string command, Action<string> report)
        {
            string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(command));
            var start = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "powershell.exe"),
                "-NoProfile -ExecutionPolicy Bypass -EncodedCommand " + encoded)
            {
                WorkingDirectory = ESAgentArtifactGenerationWorkspace.GetProjectRoot(),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            activeProcess = ESManagedEditorProcessRunner.StartPowerShell(
                start, ESAgentArtifactGenerationWorkspace.GetProjectRoot(), 120);
            activeReport = report;
            EditorApplication.update -= PollLauncher;
            EditorApplication.update += PollLauncher;
            AssemblyReloadEvents.beforeAssemblyReload -= DetachForReload;
            AssemblyReloadEvents.beforeAssemblyReload += DetachForReload;
            EditorApplication.quitting -= DetachForReload;
            EditorApplication.quitting += DetachForReload;
        }

        private static void PollLauncher()
        {
            if (activeProcess == null)
            {
                CleanupProcessState();
                return;
            }
            try
            {
                if (activeProcess.HasTimedOut(DateTimeOffset.UtcNow))
                {
                    Action<string> timeoutReport = activeReport;
                    activeProcess.Terminate();
                    CleanupProcessState();
                    timeoutReport?.Invoke("实现窗口启动验证超时，受管 PowerShell 进程树已终止；不能视为任务已送达。");
                    return;
                }
                if (!activeProcess.HasExited) return;
                if (!activeProcess.WaitForExit(5000))
                    throw new InvalidOperationException("实现窗口启动器退出状态未能及时确认。");
                activeProcess.TryGetExitCode(out int exitCode);
                string output = activeProcess.ReadStandardOutputToEnd().Trim();
                string stderr = activeProcess.ReadStandardErrorToEnd().Trim();
                Action<string> report = activeReport;
                CleanupProcessState();
                if (exitCode != 0)
                {
                    report?.Invoke("实现窗口启动失败（exit=" + exitCode + "）：" + stderr);
                    return;
                }
                ESCodexSessionLaunchResult result = string.IsNullOrEmpty(output)
                    ? null
                    : JsonUtility.FromJson<ESCodexSessionLaunchResult>(output);
                if (result == null)
                {
                    report?.Invoke("会话启动器已返回，但结果无法解析：" + output);
                    return;
                }
                report?.Invoke(BuildLaunchReport(result, stderr));
            }
            catch (Exception exception)
            {
                Action<string> report = activeReport;
                CleanupProcessState();
                report?.Invoke("读取实现窗口启动结果失败：" + exception.Message);
            }
        }

        public static string BuildLaunchReport(ESCodexSessionLaunchResult result, string stderr = "")
        {
            if (result == null) return "会话启动器没有返回可验证结果。";
            string identity = result.tabTitle + "（PID " + result.processId + "）";
            if (result.contextAccepted)
            {
                string prefix = result.alreadyRunning ? "相同 Graph 实现任务已有已接收窗口：" : "已确认独立实现窗口完成初始化：";
                return prefix + identity + "。不可变启动信封已被该会话接收，接收回执：" + result.acceptanceReceiptPath;
            }
            if (result.startupFailed)
            {
                return "实现窗口启动失败：" + identity + "。" + result.startupFailureReason
                    + (string.IsNullOrEmpty(result.startupDiagnosticPath) ? string.Empty : " 诊断：" + result.startupDiagnosticPath);
            }
            if (result.startupTimedOut)
            {
                string observed = result.promptObserved
                    ? "初始化 Prompt 已进入 Codex，但信封接收回执仍未出现"
                    : "终端已创建，但尚无证据证明初始化 Prompt 已进入 Codex";
                return "实现窗口启动证据超时：" + identity + "。" + observed
                    + "；不能视为任务已送达或开始执行。信封：" + result.envelopePath;
            }
            if (result.promptObserved)
            {
                return "实现窗口初始化尚未确认：" + identity
                    + "。初始化 Prompt 已进入 Codex，但尚无信封接收回执，不能视为任务已开始执行。";
            }
            if (result.terminalStarted || result.launched || result.alreadyRunning)
            {
                return "实现窗口仅完成终端创建：" + identity
                    + "。必要初始化消息尚未确认送达，不能视为任务已开始执行。";
            }
            return "会话启动器没有打开可见窗口。" + (string.IsNullOrEmpty(stderr) ? string.Empty : " " + stderr);
        }

        private static void DetachForReload()
        {
            try
            {
                if (activeProcess != null && !activeProcess.HasExited)
                    activeProcess.Terminate();
            }
            catch (Exception exception)
            {
                activeReport?.Invoke("域重载前无法确认实现窗口启动器已终止：" + exception.Message);
            }
            CleanupProcessState();
        }

        private static void CleanupProcessState()
        {
            EditorApplication.update -= PollLauncher;
            AssemblyReloadEvents.beforeAssemblyReload -= DetachForReload;
            EditorApplication.quitting -= DetachForReload;
            activeProcess?.Dispose();
            activeProcess = null;
            activeReport = null;
        }

        private static void AppendValue(StringBuilder builder, string label, string value)
        {
            if (!string.IsNullOrWhiteSpace(value)) builder.AppendLine("- " + label + "：" + value.Trim());
        }

        private static void AppendIndentedValue(StringBuilder builder, string label, string value)
        {
            if (!string.IsNullOrWhiteSpace(value)) builder.AppendLine("  - " + label + "：" + value.Trim());
        }

        private static string QuotePowerShell(string value)
        {
            return "'" + (value ?? string.Empty).Replace("'", "''") + "'";
        }

        private static string Normalize(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/').TrimStart('/');
        }
    }

    public static class ESAgentArtifactCandidateValidator
    {
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        public static List<string> Validate(string requestDirectory, ESAgentArtifactGenerationRequest request,
            ESAgentArtifactCandidateManifest manifest)
        {
            var errors = new List<string>();
            if (request == null || manifest == null) { errors.Add("请求或候选 Manifest 无法读取。"); return errors; }
            if (request.schemaVersion != ESAgentArtifactGenerationRequest.CurrentSchemaVersion
                || request.spec?.contractSchemaVersion != ESAgentArtifactGenerationSpec.CurrentContractSchemaVersion)
                errors.Add("generation-request.json 的语义契约版本不受支持，请从当前 Graph 重新生成请求。");
            else if (!ESAgentGenerationIntentValidator.TryValidate(request.spec, out string intentError))
                errors.Add("generation-request.json 的开发意图合同无效：" + intentError);
            else if (!ESAgentGenerationRiskValidator.TryValidate(request.spec, out string semanticError))
                errors.Add("generation-request.json 的目标、输出或风险确认无效：" + semanticError);
            if (manifest.schemaVersion != 1 || !string.Equals(manifest.requestId, request.requestId, StringComparison.Ordinal))
                errors.Add("candidate-manifest.json 与请求身份不匹配。");
            if (manifest.files == null || manifest.files.Length == 0) errors.Add("候选 Manifest 没有文件。\n");
            try
            {
                string reportPath = Path.Combine(ESAgentArtifactGenerationWorkspace.ResolveProjectPath(requestDirectory),
                    "validation-report.md");
                if (!File.Exists(reportPath))
                    errors.Add("候选缺少 validation-report.md。");
                else
                {
                    string report = StrictUtf8.GetString(File.ReadAllBytes(reportPath));
                    if (report.IndexOf('\uFFFD') >= 0)
                        errors.Add("validation-report.md 包含 U+FFFD。");
                }
            }
            catch (Exception exception)
            {
                errors.Add("validation-report.md 严格 UTF-8 检查失败：" + exception.Message);
            }
            var candidatePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var targetPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ESAgentArtifactCandidateFile file in manifest.files ?? Array.Empty<ESAgentArtifactCandidateFile>())
            {
                if (file == null) { errors.Add("Manifest 包含空文件记录。"); continue; }
                if (!candidatePaths.Add(Normalize(file.candidateRelativePath)))
                    errors.Add("Manifest 包含重复候选路径：" + file.candidateRelativePath);
                if (!TryResolveCandidate(requestDirectory, file.candidateRelativePath, out string candidate, out string pathError))
                { errors.Add(pathError); continue; }
                if (!File.Exists(candidate)) { errors.Add("候选文件不存在：" + file.candidateRelativePath); continue; }
                if (!TryResolveFormalTarget(request, file, out string target, out string targetError))
                { errors.Add(targetError); continue; }
                if (!targetPaths.Add(target))
                    errors.Add("Manifest 包含重复正式目标：" + file.targetProjectPath);
                if (!TryGetDeclaredOutput(request.spec, file, out ESAgentGenerationOutput output,
                        out string outputError))
                {
                    errors.Add(outputError);
                    continue;
                }
                try
                {
                    string text = StrictUtf8.GetString(File.ReadAllBytes(candidate));
                    if (text.IndexOf('\uFFFD') >= 0) errors.Add("包含 U+FFFD：" + file.candidateRelativePath);
                    if (file.artifactKind == ESAgentArtifactKind.AICommand)
                    {
                        ValidateAICommand(text, file, output, errors);
                        ValidateGraphSemanticCoverage(text, request.spec, output, file.targetProjectPath, errors);
                    }
                    else
                    {
                        ValidateAgentSkillFile(text, file, output, errors);
                        if (Normalize(file.targetProjectPath).EndsWith("/SKILL.md", StringComparison.Ordinal))
                            ValidateGraphSemanticCoverage(text, request.spec, output, file.targetProjectPath, errors);
                    }
                    ValidateArtifactIdentity(text, file, output, errors);
                }
                catch (Exception exception) { errors.Add("严格 UTF-8 失败 " + file.candidateRelativePath + "：" + exception.Message); }
            }
            ValidateDeclaredOutputCoverage(request.spec, manifest, errors);
            ValidateSkillBundles(manifest, errors);
            return errors;
        }

        private static void ValidateDeclaredOutputCoverage(ESAgentArtifactGenerationSpec spec,
            ESAgentArtifactCandidateManifest manifest, List<string> errors)
        {
            ESAgentArtifactCandidateFile[] files = manifest?.files
                ?? Array.Empty<ESAgentArtifactCandidateFile>();
            foreach (ESAgentGenerationOutput output in spec?.outputs
                ?? Array.Empty<ESAgentGenerationOutput>())
            {
                if (output == null)
                    continue;
                string declared = Normalize(output.targetProjectPath);
                bool found = files.Any(file => file != null && file.artifactKind == output.artifactKind
                    && (output.artifactKind == ESAgentArtifactKind.AICommand
                        ? Normalize(file.targetProjectPath) == declared
                        : Normalize(file.targetProjectPath) == declared.TrimEnd('/') + "/SKILL.md"));
                if (!found)
                    errors.Add("候选 Manifest 未覆盖 Graph 声明的主产物：" + output.artifactName
                        + "（" + output.nodeId + "）");
            }
        }

        private static void ValidateGraphSemanticCoverage(string text,
            ESAgentArtifactGenerationSpec spec, ESAgentGenerationOutput output,
            string path, List<string> errors)
        {
            if (spec?.goal == null || output == null)
                return;

            RequireContractText(text, spec.goal.title, "Goal 标题", path, errors);
            RequireContractText(text, spec.goal.objective, "Goal 最终目的", path, errors);
            RequireContractText(text, spec.goal.successCriteria, "Goal 成功标准", path, errors);
            if (!string.IsNullOrWhiteSpace(spec.goal.context))
                RequireContractText(text, spec.goal.context, "Goal 上下文", path, errors);
            if (!string.IsNullOrWhiteSpace(spec.goal.targetUsers))
                RequireContractText(text, spec.goal.targetUsers, "Goal 使用者或触发场景", path, errors);

            HashSet<string> relevantNodeIds = CollectUpstreamNodeIds(spec, output.nodeId);
            foreach (ESAgentGenerationReference reference in spec.references
                ?? Array.Empty<ESAgentGenerationReference>())
            {
                if (reference == null || !relevantNodeIds.Contains(reference.nodeId)) continue;
                RequireContractText(text, reference.projectPath, "Reference 路径", path, errors);
                RequireContractText(text, reference.purpose, "Reference 用途", path, errors);
            }
            foreach (ESAgentGenerationConstraint constraint in spec.constraints
                ?? Array.Empty<ESAgentGenerationConstraint>())
            {
                if (constraint == null || !relevantNodeIds.Contains(constraint.nodeId)) continue;
                RequireContractText(text, constraint.statement, "Constraint 规则", path, errors);
                RequireContractText(text, constraint.rationale, "Constraint 原因", path, errors);
                RequireContractText(text, constraint.verification, "Constraint 验证", path, errors);
            }
            foreach (ESAgentGenerationBranch branch in spec.branches
                ?? Array.Empty<ESAgentGenerationBranch>())
            {
                if (branch == null || !relevantNodeIds.Contains(branch.nodeId)) continue;
                RequireContractText(text, branch.condition, "Branch 条件", path, errors);
                RequireContractText(text, branch.matchedPath, "Branch 命中路径", path, errors);
                RequireContractText(text, branch.defaultPath, "Branch 默认路径", path, errors);
                RequireContractText(text, branch.failurePath, "Branch 失败路径", path, errors);
            }
            foreach (ESAgentGenerationTraversal traversal in spec.traversals
                ?? Array.Empty<ESAgentGenerationTraversal>())
            {
                if (traversal == null || !relevantNodeIds.Contains(traversal.nodeId)) continue;
                RequireContractText(text, traversal.target, "Traversal 目标", path, errors);
                RequireContractText(text, traversal.itemAlias, "Traversal 元素名称", path, errors);
                RequireContractText(text, "maxDepth=" + traversal.maxDepth, "Traversal 最大深度", path, errors);
                RequireContractText(text, "maxItems=" + traversal.maxItems, "Traversal 最大数量", path, errors);
                RequireContractText(text, traversal.stopCondition, "Traversal 停止条件", path, errors);
                RequireContractText(text, traversal.emptyResultAction, "Traversal 空结果", path, errors);
                RequireContractText(text, traversal.failureAction, "Traversal 失败行为", path, errors);
            }
            foreach (ESAgentGenerationValidation validation in spec.validations
                ?? Array.Empty<ESAgentGenerationValidation>())
            {
                if (validation == null || !IsValidationForOutput(spec, output.nodeId, validation.nodeId))
                    continue;
                if (!string.IsNullOrWhiteSpace(validation.additionalRequirements))
                    RequireContractText(text, validation.additionalRequirements,
                        "Validation 附加要求", path, errors);
                if (!string.IsNullOrWhiteSpace(validation.reviewChecklist))
                    RequireContractText(text, validation.reviewChecklist,
                        "Validation 审查清单", path, errors);
            }
        }

        private static HashSet<string> CollectUpstreamNodeIds(ESAgentArtifactGenerationSpec spec,
            string outputNodeId)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            var queue = new Queue<string>();
            if (!string.IsNullOrWhiteSpace(outputNodeId) && result.Add(outputNodeId))
                queue.Enqueue(outputNodeId);
            ESAgentGenerationRelation[] relations = spec?.relations
                ?? Array.Empty<ESAgentGenerationRelation>();
            while (queue.Count > 0)
            {
                string current = queue.Dequeue();
                for (int i = 0; i < relations.Length; i++)
                {
                    ESAgentGenerationRelation relation = relations[i];
                    if (relation != null && string.Equals(relation.toNodeId, current, StringComparison.Ordinal)
                        && result.Add(relation.fromNodeId))
                        queue.Enqueue(relation.fromNodeId);
                }
            }
            return result;
        }

        private static bool IsValidationForOutput(ESAgentArtifactGenerationSpec spec,
            string outputNodeId, string validationNodeId)
        {
            return (spec?.relations ?? Array.Empty<ESAgentGenerationRelation>()).Any(relation =>
                relation != null && relation.relationKind == ESAgentRelationKind.RequiresValidation
                && string.Equals(relation.fromNodeId, outputNodeId, StringComparison.Ordinal)
                && string.Equals(relation.toNodeId, validationNodeId, StringComparison.Ordinal));
        }

        public static bool TryResolveCandidate(string requestDirectory, string candidateRelativePath,
            out string fullPath, out string error)
        {
            fullPath = string.Empty;
            string normalized = Normalize(candidateRelativePath);
            if (string.IsNullOrEmpty(normalized) || Path.IsPathRooted(normalized) || normalized.Contains(".."))
            { error = "候选路径必须位于 candidate/ 且不能包含 ..：" + candidateRelativePath; return false; }
            if (!ESAgentArtifactGenerationWorkspace.TryResolveProjectPath(requestDirectory, out string requestFull, out error))
                return false;
            fullPath = Path.GetFullPath(Path.Combine(requestFull, (candidateRelativePath ?? string.Empty).Replace('/', Path.DirectorySeparatorChar)));
            string candidateRoot = Path.GetFullPath(Path.Combine(requestFull, "candidate")) + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(candidateRoot, StringComparison.OrdinalIgnoreCase))
            { error = "候选路径越过 candidate/：" + candidateRelativePath; return false; }
            error = string.Empty;
            return true;
        }

        public static bool TryResolveFormalTarget(ESAgentArtifactGenerationRequest request,
            ESAgentArtifactCandidateFile file, out string fullPath, out string error)
        {
            fullPath = string.Empty;
            if (request?.spec == null || file == null)
            {
                error = "请求或候选文件记录为空。";
                return false;
            }
            if (!IsTargetDeclared(request.spec, file, out error))
                return false;
            return ESAgentArtifactGenerationWorkspace.TryResolveProjectPath(file.targetProjectPath, out fullPath, out error);
        }

        private static bool IsTargetDeclared(ESAgentArtifactGenerationSpec spec, ESAgentArtifactCandidateFile file,
            out string error)
        {
            return TryGetDeclaredOutput(spec, file, out _, out error);
        }

        internal static bool TryGetDeclaredOutput(ESAgentArtifactGenerationSpec spec,
            ESAgentArtifactCandidateFile file, out ESAgentGenerationOutput output, out string error)
        {
            output = null;
            if (spec == null || file == null)
            {
                error = "请求规格或候选文件记录为空。";
                return false;
            }
            string target = Normalize(file.targetProjectPath);
            if (file.artifactKind == ESAgentArtifactKind.AICommand)
            {
                if (!ESAgentArtifactPathPolicy.IsAllowedTarget(file.artifactKind, target, out error))
                    return false;
            }
            else if (file.artifactKind == ESAgentArtifactKind.AgentSkill)
            {
                if (!IsAllowedAgentSkillFileTarget(target, out error))
                    return false;
            }
            else
            {
                error = "不支持的候选产物类型：" + file.artifactKind;
                return false;
            }
            foreach (ESAgentGenerationOutput candidateOutput in spec.outputs ?? Array.Empty<ESAgentGenerationOutput>())
            {
                if (candidateOutput == null || candidateOutput.artifactKind != file.artifactKind) continue;
                string declared = Normalize(candidateOutput.targetProjectPath);
                bool commandMatch = file.artifactKind == ESAgentArtifactKind.AICommand && target == declared;
                bool skillMatch = file.artifactKind == ESAgentArtifactKind.AgentSkill
                    && !string.IsNullOrEmpty(declared) && target.StartsWith(declared, StringComparison.Ordinal);
                if (commandMatch || skillMatch)
                {
                    output = candidateOutput;
                    error = string.Empty;
                    return true;
                }
            }
            error = "候选目标未在 Graph OutputArtifact 中声明：" + target;
            return false;
        }

        internal static bool RequiresIdentityMarker(ESAgentArtifactCandidateFile file)
        {
            if (file == null)
                return false;
            return file.artifactKind == ESAgentArtifactKind.AICommand
                || file.artifactKind == ESAgentArtifactKind.AgentSkill
                && Normalize(file.targetProjectPath).EndsWith("/SKILL.md", StringComparison.Ordinal);
        }

        public static void ValidateArtifactIdentity(string text, ESAgentArtifactCandidateFile file,
            ESAgentGenerationOutput output, List<string> errors)
        {
            if (!RequiresIdentityMarker(file))
                return;
            if (output == null || string.IsNullOrWhiteSpace(output.artifactId))
            {
                errors.Add("请求缺少稳定 ArtifactId：" + file.targetProjectPath);
                return;
            }
            string expected = ESAgentArtifactGenerationWorkspace.BuildArtifactIdentityMarker(output.artifactId);
            int markerIndex = (text ?? string.Empty).IndexOf(expected, StringComparison.Ordinal);
            if (markerIndex < 0)
            {
                errors.Add("候选缺少或写错稳定身份标记：" + expected + " " + file.targetProjectPath);
                return;
            }
            if (file.artifactKind != ESAgentArtifactKind.AgentSkill)
                return;
            string normalized = (text ?? string.Empty).Replace("\r\n", "\n");
            int frontmatterEnd = normalized.IndexOf("\n---", 3, StringComparison.Ordinal);
            int normalizedMarkerIndex = normalized.IndexOf(expected, StringComparison.Ordinal);
            if (frontmatterEnd < 0 || normalizedMarkerIndex <= frontmatterEnd)
                errors.Add("Agent Skill 的 ArtifactId 标记必须放在 SKILL.md YAML frontmatter 之后："
                    + file.targetProjectPath);
        }

        private static bool IsAllowedAgentSkillFileTarget(string target, out string error)
        {
            const string prefix = ".agents/skills/";
            if (string.IsNullOrEmpty(target) || target.Contains("..") || !target.StartsWith(prefix, StringComparison.Ordinal))
            {
                error = "Agent Skill 正式路径非法：" + target;
                return false;
            }
            string remainder = target.Substring(prefix.Length);
            int separator = remainder.IndexOf('/');
            if (separator <= 3 || !remainder.Substring(0, separator).StartsWith("es-", StringComparison.Ordinal))
            {
                error = "Agent Skill 必须位于 .agents/skills/es-*/ 的直接子目录：" + target;
                return false;
            }
            string directory = remainder.Substring(0, separator);
            for (int i = 0; i < directory.Length; i++)
            {
                char value = directory[i];
                if (!(value >= 'a' && value <= 'z') && !(value >= '0' && value <= '9') && value != '-')
                {
                    error = "Agent Skill 目录只能使用小写字母、数字和连字符：" + target;
                    return false;
                }
            }
            string relativeFile = remainder.Substring(separator + 1);
            if (relativeFile == "SKILL.md" || relativeFile == "agents/openai.yaml"
                || (relativeFile.StartsWith("references/", StringComparison.Ordinal) && relativeFile.Length > "references/".Length)
                || (relativeFile.StartsWith("scripts/", StringComparison.Ordinal) && relativeFile.Length > "scripts/".Length)
                || (relativeFile.StartsWith("assets/", StringComparison.Ordinal) && relativeFile.Length > "assets/".Length))
            {
                error = string.Empty;
                return true;
            }
            error = "Agent Skill 候选只能写入 SKILL.md、agents/openai.yaml、references/、scripts/ 或 assets/：" + target;
            return false;
        }

        private static void ValidateAICommand(string text, ESAgentArtifactCandidateFile file,
            ESAgentGenerationOutput output, List<string> errors)
        {
            string path = Normalize(file.targetProjectPath);
            if (!path.StartsWith("Assets/Plugins/ES/AICommands/", StringComparison.Ordinal) || !path.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                errors.Add("AICommand 正式路径非法：" + path);
            foreach (string metadata in new[] { "命令类型：", "默认改文件：", "风险等级：" })
                if (text.IndexOf(metadata, StringComparison.Ordinal) < 0) errors.Add("AICommand 缺少元数据 " + metadata + " " + path);
            if (output != null)
            {
                RequireContractText(text, "命令类型：" + output.commandType, "命令类型", path, errors);
                RequireContractText(text, "默认改文件：" + output.defaultWrite, "写入授权与范围", path, errors);
                RequireContractText(text, "风险等级：" + output.riskLevel, "风险等级", path, errors);
                RequireContractText(text, output.expectedInputs, "输入契约", path, errors);
                RequireContractText(text, output.preconditions, "前置条件", path, errors);
                RequireContractText(text, output.allowedWriteScopes, "允许写入范围", path, errors);
                RequireContractText(text, output.forbiddenOperations, "禁止操作", path, errors);
                RequireContractText(text, output.executionOutline, "执行步骤", path, errors);
                RequireContractText(text, output.acceptanceCriteria, "完成定义", path, errors);
                RequireContractText(text, output.requiredEvidence, "证据要求", path, errors);
                RequireContractText(text, output.blockedHandling, "阻断处理", path, errors);
                RequireContractText(text, output.rollbackStrategy, "回滚策略", path, errors);
            }
            foreach (string line in text.Replace("\r\n", "\n").Split('\n'))
            {
                string value = line.Trim().Trim('`');
                if (!LooksLikeProjectPath(value)) continue;
                string referenced = ESAgentArtifactGenerationWorkspace.ResolveProjectPath(value);
                if (!File.Exists(referenced) && !Directory.Exists(referenced)) errors.Add("AICommand 引用路径不存在：" + value);
            }
        }

        private static void ValidateAgentSkillFile(string text, ESAgentArtifactCandidateFile file,
            ESAgentGenerationOutput output, List<string> errors)
        {
            string target = Normalize(file.targetProjectPath);
            if (!IsAllowedAgentSkillFileTarget(target, out string targetError)) errors.Add(targetError);
            if (target.EndsWith("/SKILL.md", StringComparison.Ordinal))
            {
                if (!text.StartsWith("---", StringComparison.Ordinal) || text.IndexOf("\nname:", StringComparison.Ordinal) < 0
                    || text.IndexOf("\ndescription:", StringComparison.Ordinal) < 0)
                    errors.Add("SKILL.md 缺少标准 YAML frontmatter：" + target);
                if (output != null)
                {
                    RequireContractText(text, output.skillDescription, "能力说明", target, errors);
                    RequireContractText(text, output.skillTriggerScenarios, "触发场景", target, errors);
                    RequireContractText(text, output.skillNonTriggerScenarios, "非触发场景", target, errors);
                    RequireContractText(text, output.skillPreconditions, "前置条件", target, errors);
                    RequireContractText(text, output.skillRequiredDependencies, "必要依赖", target, errors);
                    RequireContractText(text, output.skillInputContract, "输入契约", target, errors);
                    RequireContractText(text, output.skillWorkflow, "工作流", target, errors);
                    RequireContractText(text, output.skillOutputContract, "输出契约", target, errors);
                    RequireContractText(text, output.skillSideEffects, "副作用", target, errors);
                    RequireContractText(text, output.skillNonGoals, "非目标", target, errors);
                    RequireContractText(text, output.skillFailureRecovery, "失败恢复", target, errors);
                    RequireContractText(text, output.skillValidationSteps, "验证要求", target, errors);
                    RequireContractText(text, output.skillPermissionBoundary, "权限边界", target, errors);
                }
            }
        }

        private static void RequireContractText(string text, string required, string label,
            string path, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(required))
            {
                errors.Add("GenerationSpec 缺少" + label + "语义：" + path);
                return;
            }
            string candidate = (text ?? string.Empty).Replace("\r\n", "\n");
            string[] clauses = required.Replace("\r\n", "\n")
                .Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < clauses.Length; i++)
            {
                string clause = clauses[i].Trim();
                if (!string.IsNullOrEmpty(clause)
                    && candidate.IndexOf(clause, StringComparison.Ordinal) < 0)
                {
                    errors.Add("候选未原样保留" + label + "语义：" + path + "；缺少：" + clause);
                    return;
                }
            }
        }

        private static void ValidateSkillBundles(ESAgentArtifactCandidateManifest manifest, List<string> errors)
        {
            string[] roots = (manifest.files ?? Array.Empty<ESAgentArtifactCandidateFile>())
                .Where(file => file != null && file.artifactKind == ESAgentArtifactKind.AgentSkill)
                .Select(file => SkillRoot(Normalize(file.targetProjectPath))).Where(root => !string.IsNullOrEmpty(root)).Distinct().ToArray();
            foreach (string root in roots)
            {
                if (!(manifest.files ?? Array.Empty<ESAgentArtifactCandidateFile>()).Any(file => file != null
                    && Normalize(file.targetProjectPath) == root + "SKILL.md")) errors.Add("Agent Skill 缺少 SKILL.md：" + root);
                if (!(manifest.files ?? Array.Empty<ESAgentArtifactCandidateFile>()).Any(file => file != null
                    && Normalize(file.targetProjectPath) == root + "agents/openai.yaml")) errors.Add("Agent Skill 缺少 agents/openai.yaml：" + root);
            }
        }

        private static string SkillRoot(string target)
        {
            if (!target.StartsWith(".agents/skills/", StringComparison.Ordinal)) return string.Empty;
            int slash = target.IndexOf('/', ".agents/skills/".Length);
            return slash < 0 ? string.Empty : target.Substring(0, slash + 1);
        }

        private static bool LooksLikeProjectPath(string value)
        {
            return value.StartsWith("Assets/", StringComparison.Ordinal) || value.StartsWith("Documentation/", StringComparison.Ordinal)
                || value.StartsWith("ES/", StringComparison.Ordinal) || value.StartsWith("Packages/", StringComparison.Ordinal)
                || value.StartsWith(".agents/", StringComparison.Ordinal);
        }

        private static string Normalize(string path) { return (path ?? string.Empty).Replace('\\', '/').TrimStart('/'); }
    }

    [ESWindowSleepContract(ESWindowSleepMode.Full, ESWindowSurfaceKind.Workspace)]
    public sealed class ESAgentArtifactCandidateReviewWindow : EditorWindow, IESWindowPresentationShortTitle
    {
        public string ESWindow_PresentationShortTitle => "候选";

        private sealed class DiffRow
        {
            public int oldLine;
            public int newLine;
            public string oldText;
            public string newText;
            public bool IsContext => oldText != null && newText != null
                && string.Equals(oldText, newText, StringComparison.Ordinal);
        }

        private const long MaximumLcsCells = 2000000L;
        private string requestDirectory;
        private ESAgentArtifactGenerationRequest request;
        private ESAgentArtifactCandidateManifest manifest;
        private List<string> validationErrors = new List<string>();
        private int selectedIndex;
        private Vector2 scroll;
        private readonly List<DiffRow> diffRows = new List<DiffRow>();
        private string diffMessage = string.Empty;
        private string diffPath = string.Empty;
        private int addedLines;
        private int removedLines;
        private int changedLines;
        private float diffContentWidth = 1000f;
        private bool showOnlyChanges;
        private GUIStyle diffTextStyle;
        private GUIStyle lineNumberStyle;

        public static void Open(string requestDirectory)
        {
            var window = GetWindow<ESAgentArtifactCandidateReviewWindow>();
            window.titleContent = new GUIContent("智能助手候选审查");
            window.minSize = new Vector2(820f, 560f);
            window.maxSize = new Vector2(1800f, 1200f);
            window.requestDirectory = requestDirectory ?? string.Empty;
            window.Refresh();
            window.Show();
        }

        public static void OpenLatest()
        {
            string latest = ESAgentArtifactGenerationWorkspace.GetLatestRequestDirectory();
            if (string.IsNullOrEmpty(latest)) EditorUtility.DisplayDialog("智能助手候选审查", "尚无候选请求。", "确定");
            else Open(latest);
        }

        public static void OpenForGraph(ESAgentArtifactGenerationSpec spec)
        {
            if (ESAgentArtifactGenerationWorkspace.TryGetRequestDirectory(spec, out string requestDirectory))
            {
                Open(requestDirectory);
                return;
            }
            EditorUtility.DisplayDialog("智能助手候选审查",
                "当前 Graph 没有匹配其 GraphId 与内容签名的候选。请先生成或重新生成候选。", "确定");
        }

        private void OnEnable()
        {
            ESWindowFoundation.BindWithStandardSystemHost(
                this,
                ESWindowFoundation.EnsureStandardSystemActionBar(this));
        }

        private void OnDisable()
        {
            ESWindowFoundation.Suspend(this);
        }

        private void OnDestroy()
        {
            ESWindowFoundation.Close(this);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("候选请求", EditorStyles.boldLabel);
            EditorGUILayout.SelectableLabel(requestDirectory ?? string.Empty, GUILayout.Height(18f));
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("刷新候选文件", GUILayout.Width(110f))) Refresh();
                if (GUILayout.Button("打开候选目录", GUILayout.Width(110f))) EditorUtility.RevealInFinder(ESAgentArtifactGenerationWorkspace.ResolveProjectPath(requestDirectory));
                GUILayout.FlexibleSpace();
                GUI.enabled = manifest != null && validationErrors.Count == 0;
                if (GUILayout.Button("人工批准并导入", GUILayout.Width(150f))) Approve();
                GUI.enabled = true;
            }
            if (manifest == null)
            {
                EditorGUILayout.HelpBox("等待生成工具在候选目录生成文件清单。", MessageType.Info);
                return;
            }
            if (validationErrors.Count > 0) EditorGUILayout.HelpBox(string.Join("\n", validationErrors), MessageType.Error);
            else EditorGUILayout.HelpBox("候选文件检查通过；请继续查看文件差异并人工批准。", MessageType.Info);
            string[] labels = manifest.files.Select((file, index) => index + " · " + file.targetProjectPath).ToArray();
            if (labels.Length > 0)
            {
                int next = EditorGUILayout.Popup("候选文件", Mathf.Clamp(selectedIndex, 0, labels.Length - 1), labels);
                if (next != selectedIndex) { selectedIndex = next; RefreshDiff(); }
            }
            DrawDiffToolbar();
            DrawDiffViewer();
        }

        private void DrawDiffToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("文件差异", EditorStyles.boldLabel);
                GUILayout.Space(8f);
                GUILayout.Label("新增 " + addedLines, GUILayout.Width(64f));
                GUILayout.Label("删除 " + removedLines, GUILayout.Width(64f));
                GUILayout.Label("修改 " + changedLines, GUILayout.Width(64f));
                GUILayout.FlexibleSpace();
                showOnlyChanges = GUILayout.Toggle(showOnlyChanges, "仅显示改动", EditorStyles.toolbarButton,
                    GUILayout.Width(88f));
                using (new EditorGUI.DisabledScope(diffRows.Count == 0))
                    if (GUILayout.Button("复制统一 Diff", EditorStyles.toolbarButton, GUILayout.Width(96f)))
                        EditorGUIUtility.systemCopyBuffer = BuildUnifiedDiff(diffPath, diffRows);
            }
        }

        private void DrawDiffViewer()
        {
            if (!string.IsNullOrEmpty(diffMessage))
            {
                EditorGUILayout.HelpBox(diffMessage, MessageType.Warning);
                return;
            }
            if (diffRows.Count == 0 || addedLines + removedLines + changedLines == 0)
            {
                EditorGUILayout.HelpBox("当前文件与候选文件内容一致。", MessageType.Info);
                return;
            }

            EnsureDiffStyles();
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("当前版本", GUILayout.Width(Mathf.Max(200f, (position.width - 30f) * 0.5f)));
                GUILayout.Label("候选版本");
            }
            scroll = EditorGUILayout.BeginScrollView(scroll, true, true);
            for (int i = 0; i < diffRows.Count; i++)
            {
                DiffRow row = diffRows[i];
                if (showOnlyChanges && row.IsContext)
                    continue;
                Rect rect = GUILayoutUtility.GetRect(diffContentWidth, 20f, GUILayout.ExpandWidth(false));
                DrawDiffRow(rect, row, i);
            }
            EditorGUILayout.EndScrollView();
        }

        private void EnsureDiffStyles()
        {
            if (diffTextStyle != null)
                return;
            diffTextStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
                padding = new RectOffset(5, 5, 1, 1)
            };
            lineNumberStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleRight,
                clipping = TextClipping.Clip,
                padding = new RectOffset(2, 6, 1, 1)
            };
        }

        private void DrawDiffRow(Rect rect, DiffRow row, int index)
        {
            float half = (rect.width - 2f) * 0.5f;
            Rect oldRect = new Rect(rect.x, rect.y, half, rect.height);
            Rect newRect = new Rect(rect.x + half + 2f, rect.y, half, rect.height);
            bool hasOld = row.oldText != null;
            bool hasNew = row.newText != null;
            bool context = row.IsContext;
            Color neutral = EditorGUIUtility.isProSkin
                ? new Color(0.13f, 0.14f, 0.16f, index % 2 == 0 ? 0.72f : 0.48f)
                : new Color(0.92f, 0.93f, 0.95f, index % 2 == 0 ? 0.78f : 0.58f);
            Color removed = EditorGUIUtility.isProSkin ? new Color(0.38f, 0.12f, 0.14f, 0.9f) : new Color(1f, 0.78f, 0.8f, 0.95f);
            Color added = EditorGUIUtility.isProSkin ? new Color(0.1f, 0.31f, 0.18f, 0.9f) : new Color(0.76f, 0.94f, 0.8f, 0.95f);
            EditorGUI.DrawRect(oldRect, context || !hasOld ? neutral : removed);
            EditorGUI.DrawRect(newRect, context || !hasNew ? neutral : added);
            EditorGUI.DrawRect(new Rect(oldRect.xMax, rect.y, 2f, rect.height),
                EditorGUIUtility.isProSkin ? new Color(0.28f, 0.3f, 0.34f) : new Color(0.68f, 0.7f, 0.74f));
            DrawDiffCell(oldRect, row.oldLine, row.oldText);
            DrawDiffCell(newRect, row.newLine, row.newText);
        }

        private void DrawDiffCell(Rect rect, int line, string value)
        {
            Rect numberRect = new Rect(rect.x, rect.y, 46f, rect.height);
            Rect textRect = new Rect(numberRect.xMax, rect.y, rect.width - numberRect.width, rect.height);
            GUI.Label(numberRect, line > 0 ? line.ToString() : string.Empty, lineNumberStyle);
            GUI.Label(textRect, (value ?? string.Empty).Replace("\t", "    "), diffTextStyle);
        }

        private void Refresh()
        {
            request = null; manifest = null; validationErrors.Clear(); ResetDiff();
            if (string.IsNullOrWhiteSpace(requestDirectory)) return;
            string full = ESAgentArtifactGenerationWorkspace.ResolveProjectPath(requestDirectory);
            try
            {
                string requestPath = Path.Combine(full, "generation-request.json");
                if (File.Exists(requestPath)) request = JsonUtility.FromJson<ESAgentArtifactGenerationRequest>(File.ReadAllText(requestPath, Encoding.UTF8));
                string manifestPath = Path.Combine(full, "candidate-manifest.json");
                if (!File.Exists(manifestPath)) manifestPath = Path.Combine(full, "candidate", "candidate-manifest.json");
                if (File.Exists(manifestPath)) manifest = JsonUtility.FromJson<ESAgentArtifactCandidateManifest>(File.ReadAllText(manifestPath, Encoding.UTF8));
                if (manifest != null) validationErrors = ESAgentArtifactCandidateValidator.Validate(requestDirectory, request, manifest);
                selectedIndex = 0; RefreshDiff(); Repaint();
            }
            catch (Exception exception) { validationErrors.Add("刷新失败：" + exception.Message); }
        }

        private void RefreshDiff()
        {
            ResetDiff();
            if (manifest?.files == null || manifest.files.Length == 0) return;
            selectedIndex = Mathf.Clamp(selectedIndex, 0, manifest.files.Length - 1);
            ESAgentArtifactCandidateFile file = manifest.files[selectedIndex];
            if (!ESAgentArtifactCandidateValidator.TryResolveCandidate(requestDirectory, file.candidateRelativePath,
                out string candidate, out string error)) { diffMessage = error; return; }
            if (!ESAgentArtifactCandidateValidator.TryResolveFormalTarget(request, file,
                out string beforePath, out error)) { diffMessage = error; return; }
            string before = File.Exists(beforePath) ? File.ReadAllText(beforePath, Encoding.UTF8) : string.Empty;
            string after = File.Exists(candidate) ? File.ReadAllText(candidate, Encoding.UTF8) : string.Empty;
            diffPath = file.targetProjectPath;
            BuildDiffRows(before, after, diffRows, out addedLines, out removedLines, out changedLines);
            int longestLine = 0;
            for (int i = 0; i < diffRows.Count; i++)
            {
                longestLine = Math.Max(longestLine, diffRows[i].oldText?.Length ?? 0);
                longestLine = Math.Max(longestLine, diffRows[i].newText?.Length ?? 0);
            }
            diffContentWidth = Mathf.Clamp(760f + longestLine * 5.5f, 1000f, 3000f);
            scroll = Vector2.zero;
        }

        private void ResetDiff()
        {
            diffRows.Clear();
            diffMessage = string.Empty;
            diffPath = string.Empty;
            addedLines = 0;
            removedLines = 0;
            changedLines = 0;
            diffContentWidth = 1000f;
            scroll = Vector2.zero;
        }

        private void Approve()
        {
            validationErrors = ESAgentArtifactCandidateValidator.Validate(requestDirectory, request, manifest);
            if (validationErrors.Count > 0) { Repaint(); return; }
            if (!EditorUtility.DisplayDialog("批准 Agent 产物",
                "将导入 " + manifest.files.Length + " 个候选文件到正式目录。已有文件会先备份到请求目录。是否继续？", "批准并导入", "取消")) return;
            string requestFull = ESAgentArtifactGenerationWorkspace.ResolveProjectPath(requestDirectory);
            string backupRoot = Path.Combine(requestFull, "backup-before-approval");
            var operations = new List<ESAgentArtifactFileOperation>();
            foreach (ESAgentArtifactCandidateFile file in manifest.files)
            {
                if (!ESAgentArtifactCandidateValidator.TryResolveCandidate(requestDirectory, file.candidateRelativePath,
                    out string source, out string sourceError))
                {
                    validationErrors.Add(sourceError);
                    continue;
                }
                if (!ESAgentArtifactCandidateValidator.TryResolveFormalTarget(request, file,
                    out string target, out string targetError))
                {
                    validationErrors.Add(targetError);
                    continue;
                }
                string backup = Path.Combine(backupRoot, file.targetProjectPath.Replace('/', Path.DirectorySeparatorChar));
                operations.Add(new ESAgentArtifactFileOperation
                {
                    SourcePath = source,
                    TargetPath = target,
                    BackupPath = backup
                });
            }
            if (validationErrors.Count > 0)
            {
                Repaint();
                return;
            }

            ESAgentArtifactImportResult importResult = ESAgentArtifactImportTransaction.Execute(
                operations,
                new ESAgentArtifactPhysicalFileIO(),
                () =>
                {
                    AssetDatabase.Refresh();
                    string report = RunExistingValidators(manifest.files.Select(file => file.targetProjectPath).ToArray());
                    string approvedAtUtc = DateTime.UtcNow.ToString("O");
                    ESAgentArtifactGenerationWorkspace.WriteUtf8(Path.Combine(requestFull, "approval-report.md"),
                        "# Approval Report\n\nApproved at: " + approvedAtUtc
                        + (request.spec.riskAcceptance == null ? string.Empty
                            : "\nRisk acceptance: " + request.spec.riskAcceptance.acceptanceHash
                                + "\nAccepted by: " + request.spec.riskAcceptance.acceptedBy
                                + "\nAccepted issues: " + string.Join(", ",
                                    request.spec.riskAcceptance.issueCodes ?? Array.Empty<string>()))
                        + "\n\n" + report);
                    var approvedFiles = new List<ESAgentArtifactApprovedFile>();
                    foreach (ESAgentArtifactCandidateFile file in manifest.files)
                    {
                        if (!ESAgentArtifactCandidateValidator.TryGetDeclaredOutput(request.spec, file,
                                out ESAgentGenerationOutput output, out string outputError))
                            throw new InvalidOperationException(outputError);
                        approvedFiles.Add(new ESAgentArtifactApprovedFile
                        {
                            artifactKind = file.artifactKind,
                            sourceGraphId = request.spec.sourceGraphId,
                            outputNodeId = output.nodeId,
                            artifactId = output.artifactId,
                            targetProjectPath = file.targetProjectPath,
                            sha256 = ESAgentArtifactGenerationWorkspace.ComputeSha256(
                                ESAgentArtifactGenerationWorkspace.ResolveProjectPath(file.targetProjectPath))
                        });
                    }
                    var approval = new ESAgentArtifactApprovalManifest
                    {
                        requestId = request.requestId,
                        approvedAtUtc = approvedAtUtc,
                        sourceGraphId = request.spec.sourceGraphId,
                        sourceContentSignature = request.spec.SourceContentSignature,
                        riskAcceptance = request.spec.riskAcceptance,
                        files = approvedFiles.ToArray()
                    };
                    ESAgentArtifactGenerationWorkspace.WriteUtf8(Path.Combine(requestFull, "approval-manifest.json"),
                        JsonUtility.ToJson(approval, true));
                });
            try
            {
                AssetDatabase.Refresh();
                if (importResult.Succeeded)
                {
                    ESAgentArtifactGenerationWorkspace.NotifyStateChanged();
                    string reportPath = Path.Combine(requestFull, "approval-report.md");
                    string report = File.Exists(reportPath)
                        ? ESAgentArtifactGenerationWorkspace.ReadUtf8(reportPath)
                        : "正式文件已导入。";
                    EditorUtility.DisplayDialog("导入完成", "候选已导入。\n\n" + report, "确定");
                    Refresh();
                    return;
                }

                Debug.LogError(importResult.BuildDiagnostic());
                string title;
                string message;
                switch (importResult.State)
                {
                    case ESAgentArtifactImportState.FailedBeforeWrite:
                        title = "导入失败";
                        message = "正式文件尚未写入。\n\n" + importResult.BuildDiagnostic();
                        break;
                    case ESAgentArtifactImportState.RolledBack:
                        title = "导入失败，已确认回滚";
                        message = "所有已触碰的正式文件均已恢复并通过 SHA-256 核对。\n\n"
                            + importResult.BuildDiagnostic();
                        break;
                    default:
                        title = "回滚未确认 / 状态不确定";
                        message = "至少一个正式文件无法恢复或核对。请立即人工检查以下路径，禁止继续假定已回滚。\n\n"
                            + importResult.BuildDiagnostic();
                        break;
                }
                EditorUtility.DisplayDialog(title, message, "关闭");
            }
            catch (Exception exception)
            {
                // UI 刷新/报告展示异常不能覆盖事务已经给出的恢复结论。
                Debug.LogException(exception);
            }
        }

        private static string RunExistingValidators(string[] targetPaths)
        {
            var report = new StringBuilder();
            string root = ESAgentArtifactGenerationWorkspace.GetProjectRoot();
            string aiScript = Path.Combine(root, ".agents/skills/es-use-ai-command/scripts/Test-ESAICommands.ps1");
            if (targetPaths.Any(path => path.StartsWith("Assets/Plugins/ES/AICommands/", StringComparison.Ordinal)) && File.Exists(aiScript))
                report.AppendLine(RunPowerShell(aiScript, "-ProjectRoot \"" + root + "\""));
            string utf8Script = Path.Combine(root, ".agents/skills/es-utf8-guard/scripts/Test-ESUtf8.ps1");
            if (File.Exists(utf8Script))
            {
                string paths = string.Join(",", targetPaths.Select(path => "'" + path.Replace("'", "''") + "'"));
                report.AppendLine(RunPowerShell(utf8Script, "-ProjectRoot \"" + root + "\" -Path " + paths));
            }
            report.AppendLine("Agent Skill：已执行项目结构验证；官方 quick_validate.py 当前未在项目内提供，仍需补证据。");
            return report.ToString().Trim();
        }

        private static string RunPowerShell(string script, string arguments)
        {
            var start = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "powershell.exe"),
                "-NoProfile -ExecutionPolicy Bypass -File \"" + script + "\" " + arguments)
            { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
                CreateNoWindow = true, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 };
            using (ESManagedEditorProcess execution = ESManagedEditorProcessRunner.StartPowerShell(
                start, ESAgentArtifactGenerationWorkspace.GetProjectRoot(), 120))
            {
                if (!execution.WaitForExit(120000))
                {
                    execution.Terminate();
                    throw new InvalidOperationException("受管 PowerShell 验证器超时，进程树已终止：" + script);
                }
                execution.TryGetExitCode(out int exitCode);
                string output = execution.ReadStandardOutputToEnd();
                string error = execution.ReadStandardErrorToEnd();
                string report = Path.GetFileName(script) + " exit=" + exitCode + "\n" + output + error;
                if (exitCode != 0)
                    throw new InvalidOperationException("正式验证器失败：\n" + report);
                return report;
            }
        }

        private static void BuildDiffRows(string before, string after, List<DiffRow> rows,
            out int added, out int removed, out int changed)
        {
            rows.Clear();
            added = 0;
            removed = 0;
            changed = 0;
            string[] oldLines = NormalizeDiffLines(before);
            string[] newLines = NormalizeDiffLines(after);
            if ((long)oldLines.Length * newLines.Length <= MaximumLcsCells)
                BuildLcsDiffRows(oldLines, newLines, rows);
            else
                BuildGreedyDiffRows(oldLines, newLines, rows);

            for (int i = 0; i < rows.Count; i++)
            {
                DiffRow row = rows[i];
                if (row.IsContext)
                    continue;
                if (row.oldText != null && row.newText != null) changed++;
                else if (row.oldText != null) removed++;
                else if (row.newText != null) added++;
            }
        }

        private static string[] NormalizeDiffLines(string value)
        {
            if (string.IsNullOrEmpty(value))
                return Array.Empty<string>();
            return (value ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        }

        private static void BuildLcsDiffRows(string[] oldLines, string[] newLines, List<DiffRow> rows)
        {
            int oldCount = oldLines.Length;
            int newCount = newLines.Length;
            var lcs = new int[oldCount + 1, newCount + 1];
            for (int oldIndex = oldCount - 1; oldIndex >= 0; oldIndex--)
                for (int newIndex = newCount - 1; newIndex >= 0; newIndex--)
                    lcs[oldIndex, newIndex] = string.Equals(oldLines[oldIndex], newLines[newIndex], StringComparison.Ordinal)
                        ? lcs[oldIndex + 1, newIndex + 1] + 1
                        : Math.Max(lcs[oldIndex + 1, newIndex], lcs[oldIndex, newIndex + 1]);

            int i = 0;
            int j = 0;
            int oldLineNumber = 1;
            int newLineNumber = 1;
            while (i < oldCount || j < newCount)
            {
                if (i < oldCount && j < newCount
                    && string.Equals(oldLines[i], newLines[j], StringComparison.Ordinal))
                {
                    rows.Add(new DiffRow { oldLine = oldLineNumber++, newLine = newLineNumber++,
                        oldText = oldLines[i++], newText = newLines[j++] });
                    continue;
                }

                var removedBlock = new List<string>();
                var addedBlock = new List<string>();
                while ((i < oldCount || j < newCount)
                    && !(i < oldCount && j < newCount
                        && string.Equals(oldLines[i], newLines[j], StringComparison.Ordinal)))
                {
                    if (j >= newCount || (i < oldCount && lcs[i + 1, j] >= lcs[i, j + 1]))
                        removedBlock.Add(oldLines[i++]);
                    else
                        addedBlock.Add(newLines[j++]);
                }
                AppendChangedBlock(rows, removedBlock, addedBlock, ref oldLineNumber, ref newLineNumber);
            }
        }

        private static void BuildGreedyDiffRows(string[] oldLines, string[] newLines, List<DiffRow> rows)
        {
            const int lookAhead = 32;
            int i = 0;
            int j = 0;
            int oldLineNumber = 1;
            int newLineNumber = 1;
            while (i < oldLines.Length || j < newLines.Length)
            {
                if (i < oldLines.Length && j < newLines.Length
                    && string.Equals(oldLines[i], newLines[j], StringComparison.Ordinal))
                {
                    rows.Add(new DiffRow { oldLine = oldLineNumber++, newLine = newLineNumber++,
                        oldText = oldLines[i++], newText = newLines[j++] });
                    continue;
                }

                int oldMatch = FindLine(oldLines, i, newLines, j, lookAhead);
                int newMatch = FindLine(newLines, j, oldLines, i, lookAhead);
                var removedBlock = new List<string>();
                var addedBlock = new List<string>();
                if (oldMatch >= 0 && (newMatch < 0 || oldMatch <= newMatch))
                    while (i < oldMatch) removedBlock.Add(oldLines[i++]);
                else if (newMatch >= 0)
                    while (j < newMatch) addedBlock.Add(newLines[j++]);
                else
                {
                    if (i < oldLines.Length) removedBlock.Add(oldLines[i++]);
                    if (j < newLines.Length) addedBlock.Add(newLines[j++]);
                }
                AppendChangedBlock(rows, removedBlock, addedBlock, ref oldLineNumber, ref newLineNumber);
            }
        }

        private static int FindLine(string[] source, int sourceIndex, string[] target, int targetIndex, int lookAhead)
        {
            if (targetIndex >= target.Length)
                return -1;
            int end = Math.Min(source.Length, sourceIndex + lookAhead + 1);
            for (int i = sourceIndex + 1; i < end; i++)
                if (string.Equals(source[i], target[targetIndex], StringComparison.Ordinal))
                    return i;
            return -1;
        }

        private static void AppendChangedBlock(List<DiffRow> rows, List<string> removedBlock,
            List<string> addedBlock, ref int oldLineNumber, ref int newLineNumber)
        {
            int count = Math.Max(removedBlock.Count, addedBlock.Count);
            for (int i = 0; i < count; i++)
            {
                string oldText = i < removedBlock.Count ? removedBlock[i] : null;
                string newText = i < addedBlock.Count ? addedBlock[i] : null;
                rows.Add(new DiffRow
                {
                    oldLine = oldText != null ? oldLineNumber++ : 0,
                    newLine = newText != null ? newLineNumber++ : 0,
                    oldText = oldText,
                    newText = newText
                });
            }
        }

        private static string BuildUnifiedDiff(string path, List<DiffRow> rows)
        {
            var builder = new StringBuilder();
            builder.AppendLine("--- " + path + " (current)");
            builder.AppendLine("+++ " + path + " (candidate)");
            for (int i = 0; i < rows.Count; i++)
            {
                DiffRow row = rows[i];
                if (row.IsContext) builder.AppendLine("  " + row.oldText);
                else
                {
                    if (row.oldText != null) builder.AppendLine("- " + row.oldText);
                    if (row.newText != null) builder.AppendLine("+ " + row.newText);
                }
            }
            return builder.ToString();
        }
    }
}
