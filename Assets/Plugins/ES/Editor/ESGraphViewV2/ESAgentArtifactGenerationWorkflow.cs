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

namespace ES.EditorInternal
{
    public enum ESAgentAuthoringPresetKind : byte
    {
        Paired = 0,
        AICommandOnly = 1,
        AgentSkillOnly = 2,
        MindMapPaired = 3
    }

    public static class ESAgentAuthoringGraphPreset
    {
        public const string DefaultGraphFolder = "Assets/ESNormalAssets/Data/AgentAuthoring/Graphs";

        [MenuItem(MenuItemPathDefine.ASSET_CREATE_CONTEXT_PATH + MenuItemPathDefine.CONTENT_CREATION + "/图与流程/智能助手编排/配套命令 + 技能", false, 201)]
        public static void CreatePairedFromAssetsMenu() { TryCreateAsset(ESAgentAuthoringPresetKind.Paired, out _, out _); }
        [MenuItem(MenuItemPathDefine.ASSET_CREATE_CONTEXT_PATH + MenuItemPathDefine.CONTENT_CREATION + "/图与流程/智能助手编排/AICommand 实现链", false, 202)]
        public static void CreateAICommandFromAssetsMenu() { TryCreateAsset(ESAgentAuthoringPresetKind.AICommandOnly, out _, out _); }
        [MenuItem(MenuItemPathDefine.ASSET_CREATE_CONTEXT_PATH + MenuItemPathDefine.CONTENT_CREATION + "/图与流程/智能助手编排/Agent Skill 能力链", false, 203)]
        public static void CreateAgentSkillFromAssetsMenu() { TryCreateAsset(ESAgentAuthoringPresetKind.AgentSkillOnly, out _, out _); }
        [MenuItem(MenuItemPathDefine.ASSET_CREATE_CONTEXT_PATH + MenuItemPathDefine.CONTENT_CREATION + "/图与流程/智能助手编排/完整需求思路图", false, 204)]
        public static void CreateMindMapFromAssetsMenu() { TryCreateAsset(ESAgentAuthoringPresetKind.MindMapPaired, out _, out _); }

        public static bool TryCreateAsset(out ESGraphAsset asset, out string error)
        {
            return TryCreateAsset(ESAgentAuthoringPresetKind.Paired, out asset, out error);
        }

        public static bool TryCreateAsset(ESAgentAuthoringPresetKind kind, out ESGraphAsset asset, out string error)
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
                asset = ScriptableObject.CreateInstance<ESGraphAsset>();
                if (!asset.TrySetDomain(ESGraphDomainKey.FromKind(ESGraphDomainKind.AgentAuthoring), out error))
                {
                    UnityEngine.Object.DestroyImmediate(asset);
                    asset = null;
                    return false;
                }
                Populate(asset, kind);
                AssetDatabase.CreateAsset(asset, path);
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssets();
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

        public static void Populate(ESGraphAsset asset)
        {
            Populate(asset, ESAgentAuthoringPresetKind.Paired);
        }

        public static void Populate(ESGraphAsset asset, ESAgentAuthoringPresetKind kind)
        {
            if (asset == null || asset.DomainKind != ESGraphDomainKind.AgentAuthoring)
                throw new ArgumentException("必须提供空的 Agent Authoring Graph。", nameof(asset));
            if (asset.Nodes.Count > 0 || asset.Edges.Count > 0)
                throw new InvalidOperationException("Agent Authoring 预设只能填充空 Graph。");
            if (kind == ESAgentAuthoringPresetKind.MindMapPaired)
            {
                PopulateMindMap(asset);
                return;
            }
            ESGraphNodeRecord goal = Add(asset, ESGraphBuiltInNodeKind.AgentGoal, new Vector2(0f, 100f));
            ESGraphNodeRecord reference = Add(asset, ESGraphBuiltInNodeKind.AgentReference, new Vector2(220f, 100f));
            ESGraphNodeRecord constraint = Add(asset, ESGraphBuiltInNodeKind.AgentConstraint, new Vector2(440f, 100f));
            ESGraphNodeRecord command = kind != ESAgentAuthoringPresetKind.AgentSkillOnly
                ? Add(asset, ESGraphBuiltInNodeKind.AgentAICommandOutput, new Vector2(680f, kind == ESAgentAuthoringPresetKind.Paired ? 0f : 100f)) : null;
            ESGraphNodeRecord skill = kind != ESAgentAuthoringPresetKind.AICommandOnly
                ? Add(asset, ESGraphBuiltInNodeKind.AgentSkillOutput, new Vector2(680f, kind == ESAgentAuthoringPresetKind.Paired ? 220f : 100f)) : null;
            ESGraphNodeRecord validation = Add(asset, ESGraphBuiltInNodeKind.AgentValidation, new Vector2(940f, 100f));
            asset.UpdateNode(goal.nodeId, goal.typeId, goal.version, goal.title, JsonUtility.ToJson(new ESAgentGoalPayload
            {
                title = GetGoalTitle(kind),
                objective = GetGoalObjective(kind),
                context = kind == ESAgentAuthoringPresetKind.AICommandOnly
                    ? "把中文需求整理成一条可执行的实现链：读取权威资料、核对现状、按权限修改目标、运行验证并交付真实证据。"
                    : "先产出隔离候选，通过 Diff Review 后再导入正式目录。"
            }), out _);
            if (kind == ESAgentAuthoringPresetKind.AICommandOnly)
            {
                asset.UpdateNode(reference.nodeId, reference.typeId, reference.version, reference.title,
                    JsonUtility.ToJson(new ESAgentReferencePayload
                    {
                        referenceKind = ESAgentReferenceKind.AIWarning,
                        projectPath = "Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md",
                        purpose = "为真实实现任务选择必须读取的 P0 与领域专项规则。",
                        required = true
                    }), out _);
                asset.UpdateNode(constraint.nodeId, constraint.typeId, constraint.version, constraint.title,
                    JsonUtility.ToJson(new ESAgentConstraintPayload
                    {
                        kind = ESAgentConstraintKind.Permission,
                        statement = "AI 必须按实现链真正修改用户授权范围内的目标文件；不得只给方案、伪造完成或越过候选与验证边界。",
                        rationale = "让 AICommand 从文本描述升级为可执行的实现合同。",
                        verification = "交付中必须列出实际改动文件、真实编译/测试结果、未执行验证与剩余风险。"
                    }), out _);
            }
            if (command != null) asset.UpdateNode(command.nodeId, command.typeId, command.version, command.title, JsonUtility.ToJson(new ESAgentAICommandOutputPayload
            {
                commandName = "生成_新模块工作流_AI命令",
                targetProjectPath = "Assets/Plugins/ES/AICommands/生成_新模块工作流_AI命令.md",
                commandType = "明确执行", defaultWrite = "由用户在 Graph Constraint 中限定", riskLevel = "L2",
                purpose = kind == ESAgentAuthoringPresetKind.AICommandOnly
                    ? "把文本需求转换为 AI 可以真正执行的 ESFramework 实现合同。"
                    : "生成一个有明确权限、必读规则、执行步骤和交付格式的任务合同。",
                expectedInputs = "用户目标、当前实现事实、必读规则、允许修改路径、验收标准。",
                executionOutline = "读取权威规则\n核对分支、HEAD 与工作树\n按实现链修改目标文件\n运行相关编译和测试\n交付真实证据与剩余风险",
                acceptanceCriteria = "不得只输出建议；必须完成授权范围内的真实实现，并逐项报告改动、验证和未完成项。",
                requiredSections = "必须先读\n执行边界\n实现步骤\n验证结果\n改动文件\n剩余风险"
            }), out _);
            if (skill != null) asset.UpdateNode(skill.nodeId, skill.typeId, skill.version, skill.title, JsonUtility.ToJson(new ESAgentSkillOutputPayload
            {
                skillName = "es-new-module-workflow", targetProjectPath = ".agents/skills/es-new-module-workflow/",
                description = "Execute a repeatable ESFramework module workflow with live rule reading, scoped writes, validation, and evidence-backed delivery.",
                workflow = "读取 Graph References\n核对工作树与模块事实\n执行受控修改\n运行验证\n交付证据与剩余风险",
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
            Connect(asset, goal, reference); Connect(asset, reference, constraint);
            if (command != null) { Connect(asset, constraint, command); Connect(asset, command, validation); }
            if (skill != null) { Connect(asset, constraint, skill); Connect(asset, skill, validation); }
        }

        private static ESGraphNodeRecord Add(ESGraphAsset graph, ESGraphBuiltInNodeKind nodeKind, Vector2 position)
        {
            ESGraphNodeTypeKey nodeType = ESGraphNodeTypeKey.FromKind(nodeKind);
            if (!ESGraphAuthoringRegistry.TryGetNodeDefinition(graph.DomainKey, nodeType,
                    out IESGraphNodeDefinition definition))
                throw new InvalidOperationException("未注册预设节点定义：" + nodeType.StableId);
            ESGraphNodeRecord node = graph.AddNode(definition.NodeType, definition.DisplayName, position, definition.Ports);
            graph.UpdateNode(node.nodeId, definition.NodeType, definition.CurrentVersion, node.title,
                definition.CreateDefaultPayload(), out _);
            return node;
        }

        private static void Connect(ESGraphAsset graph, ESGraphNodeRecord from, ESGraphNodeRecord to)
        {
            ESGraphPortRecord output = from.ports.First(port => port.direction == ESGraphPortDirection.Output);
            ESGraphPortRecord input = to.ports.First(port => port.direction == ESGraphPortDirection.Input);
            if (!graph.TryAddEdge(output.portId, input.portId, out _, out string error))
                throw new InvalidOperationException(error);
        }

        private static void PopulateMindMap(ESGraphAsset asset)
        {
            ESGraphNodeRecord goal = Add(asset, ESGraphBuiltInNodeKind.AgentGoal, new Vector2(0f, 240f));
            ESGraphNodeRecord rules = Add(asset, ESGraphBuiltInNodeKind.AgentReference, new Vector2(240f, 160f));
            ESGraphNodeRecord contract = Add(asset, ESGraphBuiltInNodeKind.AgentReference, new Vector2(480f, 160f));
            ESGraphNodeRecord required = Add(asset, ESGraphBuiltInNodeKind.AgentConstraint, new Vector2(720f, 0f));
            ESGraphNodeRecord forbidden = Add(asset, ESGraphBuiltInNodeKind.AgentConstraint, new Vector2(720f, 160f));
            ESGraphNodeRecord permission = Add(asset, ESGraphBuiltInNodeKind.AgentConstraint, new Vector2(720f, 320f));
            ESGraphNodeRecord quality = Add(asset, ESGraphBuiltInNodeKind.AgentConstraint, new Vector2(720f, 480f));
            ESGraphNodeRecord command = Add(asset, ESGraphBuiltInNodeKind.AgentAICommandOutput, new Vector2(1020f, 120f));
            ESGraphNodeRecord skill = Add(asset, ESGraphBuiltInNodeKind.AgentSkillOutput, new Vector2(1020f, 360f));
            ESGraphNodeRecord validation = Add(asset, ESGraphBuiltInNodeKind.AgentValidation, new Vector2(1320f, 240f));

            asset.UpdateNode(goal.nodeId, goal.typeId, goal.version, goal.title, JsonUtility.ToJson(new ESAgentGoalPayload
            {
                title = "完整 Agent Artifact 需求思路图",
                objective = "把用户目标、权威上下文、权限边界、质量门禁和最终 AICommand/Agent Skill 产物组织为可审查的生成合同。",
                context = "所有分支最终汇合到候选产物与人工批准，不形成玩法运行时。",
                targetUsers = "需要通过 Graph 编排复杂 AI 协作任务的 ESFramework 开发者。",
                successCriteria = "AI 能按关系图理解需求归属，生成结构清晰且可通过 Diff Review 的配套候选。"
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
                JsonUtility.ToJson(new ESAgentAICommandOutputPayload()), out _);
            asset.UpdateNode(skill.nodeId, skill.typeId, skill.version, skill.title,
                JsonUtility.ToJson(new ESAgentSkillOutputPayload()), out _);
            asset.UpdateNode(validation.nodeId, validation.typeId, validation.version, validation.title,
                JsonUtility.ToJson(new ESAgentValidationPayload()), out _);

            Connect(asset, goal, rules);
            Connect(asset, rules, contract);
            foreach (ESGraphNodeRecord constraint in new[] { required, forbidden, permission, quality })
            {
                Connect(asset, contract, constraint);
                Connect(asset, constraint, command);
                Connect(asset, constraint, skill);
            }
            Connect(asset, command, validation);
            Connect(asset, skill, validation);
        }

        private static void ConfigureConstraint(ESGraphAsset asset, ESGraphNodeRecord node,
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
                case ESAgentAuthoringPresetKind.AgentSkillOnly: return "智能助手技能编排图";
                case ESAgentAuthoringPresetKind.MindMapPaired: return "智能助手完整思路图";
                default: return "智能助手产物编排图";
            }
        }

        private static string GetCreateDescription(ESAgentAuthoringPresetKind kind)
        {
            switch (kind)
            {
                case ESAgentAuthoringPresetKind.AICommandOnly: return "创建以文本需求驱动真实实现的 AICommand 链图";
                case ESAgentAuthoringPresetKind.AgentSkillOnly: return "创建只生成技能候选文件的预设图";
                case ESAgentAuthoringPresetKind.MindMapPaired: return "创建包含完整需求分支和配套产物的思路图";
                default: return "创建配套生成命令与技能候选文件的预设图";
            }
        }

        private static string GetGoalTitle(ESAgentAuthoringPresetKind kind)
        {
            switch (kind)
            {
                case ESAgentAuthoringPresetKind.AICommandOnly: return "实现目标";
                case ESAgentAuthoringPresetKind.AgentSkillOnly: return "生成技能";
                default: return "生成配套命令 + 技能";
            }
        }

        private static string GetGoalObjective(ESAgentAuthoringPresetKind kind)
        {
            switch (kind)
            {
                case ESAgentAuthoringPresetKind.AICommandOnly:
                    return "根据项目规则，把中文需求整理成一条可以交给 AI 真正执行的实现链，并生成可审查的 AICommand 候选。";
                case ESAgentAuthoringPresetKind.AgentSkillOnly:
                    return "根据项目规则生成一个可复用、触发边界明确并带验证步骤的 Agent Skill 候选。";
                default:
                    return "根据项目规则生成一条单次任务权限合同和一个可复用执行 Skill 候选。";
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

    [Serializable]
    public sealed class ESAgentArtifactGenerationRequest
    {
        public int schemaVersion = 1;
        public string requestId;
        public string createdAtUtc;
        public string requestDirectory;
        public string candidateDirectory;
        public ESAgentArtifactGenerationSpec spec;
    }

    [Serializable]
    public sealed class ESAgentGraphClipboardPackage
    {
        public int schemaVersion = 1;
        public ESAgentGraphCopyFormat format;
        public string requestId;
        public string generatedAtUtc;
        public ESAgentArtifactGenerationSpec spec;
    }

    [Serializable]
    public sealed class ESAgentArtifactCandidateManifest
    {
        public int schemaVersion = 1;
        public string requestId;
        public string summary;
        public ESAgentArtifactCandidateFile[] files = Array.Empty<ESAgentArtifactCandidateFile>();
    }

    [Serializable]
    public sealed class ESAgentArtifactCandidateFile
    {
        public ESAgentArtifactKind artifactKind;
        public string candidateRelativePath;
        public string targetProjectPath;
        public string summary;
    }

    [Serializable]
    public sealed class ESAgentArtifactApprovalManifest
    {
        public int schemaVersion = 2;
        public string requestId;
        public string approvedAtUtc;
        public string sourceGraphId;
        public string sourceContentSignature;
        public ESAgentArtifactApprovedFile[] files = Array.Empty<ESAgentArtifactApprovedFile>();
    }

    [Serializable]
    public sealed class ESAgentArtifactApprovedFile
    {
        public ESAgentArtifactKind artifactKind;
        public string sourceGraphId;
        public string outputNodeId;
        public string artifactId;
        public string targetProjectPath;
        public string sha256;
    }

    public static class ESAgentArtifactGenerationWorkspace
    {
        public const string CandidateRoot = "ES/Automation/Candidates/AgentAuthoring";
        private const string LatestRequestEditorPref = "ES.AgentAuthoring.LatestRequest";
        private const string ArtifactIdentityMarkerLabel = "ES-AGENT-ARTIFACT-ID:";

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
            ESCmdAgentPromptDispatchResult dispatch = ESCmdAgentWindow.OpenAndSendPromptWithReceipt(prompt);
            dispatchMessage = dispatch.Message;
            if (dispatch.State == ESCmdAgentPromptDispatchState.HeldForUser)
            {
                error = "候选请求已创建，但提示仅保留在 Cmd Agent 输入框，尚未发送：" + dispatch.Message;
                return false;
            }
            if (dispatch.State == ESCmdAgentPromptDispatchState.Rejected)
            {
                error = "候选请求已创建，但 Cmd Agent 未能接收提示（同步启动/配置失败）：" + dispatch.Message;
                return false;
            }
            if (!dispatch.IsDispatched)
            {
                error = "候选请求已创建，但 Cmd Agent 未返回可确认的发送状态：" + dispatch.Message;
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
                || string.IsNullOrWhiteSpace(spec.goal.successCriteria))
            {
                error = "即时执行失败：最终目的和成功标准不能为空。";
                return false;
            }
            requestId = CreateRequestId("run");
            string prompt = BuildImmediateExecutionPrompt(spec, requestId);
            ESCmdAgentPromptDispatchResult dispatch = ESCmdAgentWindow.OpenAndSendPromptWithReceipt(prompt);
            dispatchMessage = dispatch.Message;
            if (dispatch.State == ESCmdAgentPromptDispatchState.HeldForUser)
            {
                error = "即时执行尚未发送：" + dispatch.Message;
                return false;
            }
            if (!dispatch.IsDispatched)
            {
                error = "即时执行未能发送（同步启动/配置失败）：" + dispatch.Message;
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
            builder.AppendLine("直接执行这张图描述的任务。不要把本次动作误解为生成永久 AICommand/Agent Skill；"
                + "除非最终目的本身明确要求，否则不要创建或更新永久 Agent Artifact。");
            builder.AppendLine("Graph 不能扩大当前用户授权；仍须遵守项目 AIWarnings、当前工作树保护和真实证据边界。"
                + "未经当前明确授权，不执行 Git、历史/审计写入、发布、上传或删除。");
            AppendExecutionPlan(builder, spec);
            AppendMindMap(builder, spec);
            builder.AppendLine();
            builder.AppendLine("完成后用中文报告：实际执行内容、改动文件、验证证据、未完成项和剩余风险。不得只复述 Graph。 ");
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
            builder.AppendLine();
            builder.AppendLine("## 最终目的");
            builder.AppendLine();
            builder.AppendLine(spec?.goal?.objective ?? string.Empty);
            builder.AppendLine();
            builder.AppendLine("成功标准：" + (spec?.goal?.successCriteria ?? string.Empty));
            AppendExecutionPlan(builder, spec);
            AppendMindMap(builder, spec);
            return builder.ToString();
        }

        public static string BuildArtifactIdentityMarker(string artifactId)
        {
            return "<!-- " + ArtifactIdentityMarkerLabel + " " + (artifactId ?? string.Empty) + " -->";
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
            foreach (ESAgentGenerationConstraint item in spec?.constraints ?? Array.Empty<ESAgentGenerationConstraint>())
            {
                builder.AppendLine("- [" + item.kind + "] " + item.statement);
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
                    if (!string.IsNullOrWhiteSpace(item.expectedInputs)) builder.AppendLine("  - 预期输入：" + item.expectedInputs);
                    if (!string.IsNullOrWhiteSpace(item.executionOutline)) builder.AppendLine("  - 执行步骤：" + item.executionOutline);
                    if (!string.IsNullOrWhiteSpace(item.acceptanceCriteria)) builder.AppendLine("  - 验收标准：" + item.acceptanceCriteria);
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(item.skillTriggerScenarios)) builder.AppendLine("  - 触发场景：" + item.skillTriggerScenarios);
                    if (!string.IsNullOrWhiteSpace(item.skillWorkflow)) builder.AppendLine("  - 工作流程：" + item.skillWorkflow);
                    if (!string.IsNullOrWhiteSpace(item.skillValidationSteps)) builder.AppendLine("  - 验证步骤：" + item.skillValidationSteps);
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
            builder.AppendLine("Goal：" + request.spec.goal.objective);
            if (!string.IsNullOrWhiteSpace(request.spec.goal.context)) builder.AppendLine("Context：" + request.spec.goal.context);
            if (!string.IsNullOrWhiteSpace(request.spec.goal.targetUsers)) builder.AppendLine("Target users / triggers：" + request.spec.goal.targetUsers);
            if (!string.IsNullOrWhiteSpace(request.spec.goal.successCriteria)) builder.AppendLine("Success criteria：" + request.spec.goal.successCriteria);
            AppendMindMap(builder, request.spec);
            builder.AppendLine();
            builder.AppendLine("必须读取的 References：");
            foreach (ESAgentGenerationReference item in request.spec.references ?? Array.Empty<ESAgentGenerationReference>())
                builder.AppendLine("- [" + item.referenceKind + "] " + item.projectPath + " | " + item.purpose + " | required=" + item.required);
            builder.AppendLine();
            builder.AppendLine("Constraints：");
            foreach (ESAgentGenerationConstraint item in request.spec.constraints ?? Array.Empty<ESAgentGenerationConstraint>())
            {
                builder.AppendLine("- [" + item.kind + "] " + item.statement);
                if (!string.IsNullOrWhiteSpace(item.rationale)) builder.AppendLine("  原因：" + item.rationale);
                if (!string.IsNullOrWhiteSpace(item.verification)) builder.AppendLine("  验证：" + item.verification);
            }
            builder.AppendLine();
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
                    builder.AppendLine("  expected inputs: " + item.expectedInputs);
                    builder.AppendLine("  execution outline: " + item.executionOutline);
                    builder.AppendLine("  acceptance criteria: " + item.acceptanceCriteria);
                }
                else
                {
                    builder.AppendLine("  Agent Skill 的 required marker 必须放在 SKILL.md 的 YAML frontmatter 结束之后；其他附属文件不重复写 marker。");
                    builder.AppendLine("  skill: description=" + item.skillDescription + ", workflow=" + item.skillWorkflow
                        + ", openaiYaml=" + item.includeAgentsMetadata + ", references=" + item.includeReferences
                        + ", scripts=" + item.includeScripts + ", defaultPrompt=" + item.defaultPrompt);
                    builder.AppendLine("  triggers: " + item.skillTriggerScenarios);
                    builder.AppendLine("  non-goals: " + item.skillNonGoals);
                    builder.AppendLine("  validation: " + item.skillValidationSteps);
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
            ESAgentGenerationRelation[] relations = spec.relations ?? Array.Empty<ESAgentGenerationRelation>();
            builder.AppendLine();
            builder.AppendLine("思路图关系（这是需求归属与生成顺序，不是运行时执行图）：");
            if (relations.Length == 0)
            {
                builder.AppendLine("- 无关系数据；拒绝猜测节点归属。 ");
                return;
            }
            for (int i = 0; i < relations.Length; i++)
            {
                ESAgentGenerationRelation relation = relations[i];
                builder.AppendLine("- " + SafeTitle(relation.fromNodeTitle, relation.fromNodeTypeId) + " → "
                    + SafeTitle(relation.toNodeTitle, relation.toNodeTypeId) + " [" + relation.semanticType + "]");
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
                builder.AppendLine("    " + fromAlias + " -->|" + EscapeMermaid(relation.semanticType) + "| " + toAlias);
            }
            builder.AppendLine("```");
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

    public enum ESAgentArtifactImportState : byte
    {
        Applied = 0,
        FailedBeforeWrite = 1,
        RolledBack = 2,
        RollbackUnconfirmed = 3
    }

    public sealed class ESAgentArtifactFileOperation
    {
        public string SourcePath { get; set; }
        public string TargetPath { get; set; }
        public string BackupPath { get; set; }
    }

    public interface IESAgentArtifactFileIO
    {
        bool FileExists(string path);
        void CopyAtomically(string sourcePath, string targetPath);
        void DeleteFile(string path);
        string ComputeSha256(string path);
    }

    public sealed class ESAgentArtifactImportResult
    {
        public ESAgentArtifactImportState State { get; internal set; }
        public string PrimaryError { get; internal set; }
        public string[] RecoveryErrors { get; internal set; } = Array.Empty<string>();
        public bool Succeeded => State == ESAgentArtifactImportState.Applied;
        public bool RollbackConfirmed => State == ESAgentArtifactImportState.RolledBack;

        public string BuildDiagnostic()
        {
            var builder = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(PrimaryError))
                builder.AppendLine(PrimaryError.Trim());
            if (RecoveryErrors != null && RecoveryErrors.Length > 0)
            {
                builder.AppendLine("恢复核对错误：");
                foreach (string error in RecoveryErrors)
                    if (!string.IsNullOrWhiteSpace(error)) builder.AppendLine("- " + error.Trim());
            }
            return builder.ToString().Trim();
        }
    }

    public sealed class ESAgentArtifactPhysicalFileIO : IESAgentArtifactFileIO
    {
        public bool FileExists(string path) => File.Exists(path);

        public void CopyAtomically(string sourcePath, string targetPath)
        {
            ESAgentArtifactGenerationWorkspace.CopyFileAtomically(sourcePath, targetPath);
        }

        public void DeleteFile(string path)
        {
            ESAgentArtifactGenerationWorkspace.EnsureProjectWritePath(path);
            if (File.Exists(path)) File.Delete(path);
        }

        public string ComputeSha256(string path)
        {
            return ESAgentArtifactGenerationWorkspace.ComputeSha256(path);
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

    [Serializable]
    public sealed class ESCodexSessionLaunchResult
    {
        public string terminalMode;
        public string terminalWindowName;
        public string tabTitle;
        public string responsibilityKey;
        public string envelopePath;
        public string handoffSnapshotDirectory;
        public string sessionId;
        public int processId;
        public bool alreadyRunning;
        public bool launched;
        public bool terminalStarted;
        public bool promptObserved;
        public bool contextAccepted;
        public bool startupFailed;
        public bool startupTimedOut;
        public string launchPhase;
        public string acceptanceReceiptPath;
        public string startupDiagnosticPath;
        public string startupFailureReason;
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
            builder.AppendLine();
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
            foreach (ESAgentGenerationConstraint item in spec?.constraints ?? Array.Empty<ESAgentGenerationConstraint>())
            {
                builder.AppendLine("- [" + item.kind + "] " + item.statement);
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
                AppendValue(builder, "预期输入", item.expectedInputs);
                AppendValue(builder, "执行步骤", item.executionOutline);
                AppendValue(builder, "验收标准", item.acceptanceCriteria);
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
            requestDirectory = ESAgentArtifactGenerationWorkspace.GetLatestRequestDirectory();
            request = null;
            approval = null;
            approvedCommands = Array.Empty<string>();
            if (currentSpec == null || string.IsNullOrWhiteSpace(currentSpec.SourceContentSignature))
            {
                error = "当前 Graph 没有可核对的内容签名，请先修复并重新烘焙。";
                return false;
            }
            if (string.IsNullOrEmpty(requestDirectory))
            {
                error = "尚无候选请求。请先点击“交给 AI 生成候选”。";
                return false;
            }

            string requestFull = ESAgentArtifactGenerationWorkspace.ResolveProjectPath(requestDirectory);
            string requestPath = Path.Combine(requestFull, "generation-request.json");
            string candidateManifestPath = Path.Combine(requestFull, "candidate-manifest.json");
            if (!File.Exists(candidateManifestPath))
                candidateManifestPath = Path.Combine(requestFull, "candidate", "candidate-manifest.json");
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
                if (request?.spec == null || candidate == null || approval == null || approval.schemaVersion != 2
                    || !string.Equals(request.requestId, candidate.requestId, StringComparison.Ordinal)
                    || !string.Equals(request.requestId, approval.requestId, StringComparison.Ordinal))
                {
                    error = "请求、候选和批准清单的身份不一致。";
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
            if (manifest.schemaVersion != 1 || !string.Equals(manifest.requestId, request.requestId, StringComparison.Ordinal))
                errors.Add("candidate-manifest.json 与请求身份不匹配。");
            if (manifest.files == null || manifest.files.Length == 0) errors.Add("候选 Manifest 没有文件。\n");
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
                    if (file.artifactKind == ESAgentArtifactKind.AICommand) ValidateAICommand(text, file, errors);
                    else ValidateAgentSkillFile(text, file, errors);
                    ValidateArtifactIdentity(text, file, output, errors);
                }
                catch (Exception exception) { errors.Add("严格 UTF-8 失败 " + file.candidateRelativePath + "：" + exception.Message); }
            }
            ValidateSkillBundles(manifest, errors);
            return errors;
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

        private static void ValidateAICommand(string text, ESAgentArtifactCandidateFile file, List<string> errors)
        {
            string path = Normalize(file.targetProjectPath);
            if (!path.StartsWith("Assets/Plugins/ES/AICommands/", StringComparison.Ordinal) || !path.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                errors.Add("AICommand 正式路径非法：" + path);
            foreach (string metadata in new[] { "命令类型：", "默认改文件：", "风险等级：" })
                if (text.IndexOf(metadata, StringComparison.Ordinal) < 0) errors.Add("AICommand 缺少元数据 " + metadata + " " + path);
            foreach (string line in text.Replace("\r\n", "\n").Split('\n'))
            {
                string value = line.Trim().Trim('`');
                if (!LooksLikeProjectPath(value)) continue;
                string referenced = ESAgentArtifactGenerationWorkspace.ResolveProjectPath(value);
                if (!File.Exists(referenced) && !Directory.Exists(referenced)) errors.Add("AICommand 引用路径不存在：" + value);
            }
        }

        private static void ValidateAgentSkillFile(string text, ESAgentArtifactCandidateFile file, List<string> errors)
        {
            string target = Normalize(file.targetProjectPath);
            if (!IsAllowedAgentSkillFileTarget(target, out string targetError)) errors.Add(targetError);
            if (target.EndsWith("/SKILL.md", StringComparison.Ordinal))
            {
                if (!text.StartsWith("---", StringComparison.Ordinal) || text.IndexOf("\nname:", StringComparison.Ordinal) < 0
                    || text.IndexOf("\ndescription:", StringComparison.Ordinal) < 0)
                    errors.Add("SKILL.md 缺少标准 YAML frontmatter：" + target);
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

    public sealed class ESAgentArtifactCandidateReviewWindow : EditorWindow
    {
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
                        "# Approval Report\n\nApproved at: " + approvedAtUtc + "\n\n" + report);
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
