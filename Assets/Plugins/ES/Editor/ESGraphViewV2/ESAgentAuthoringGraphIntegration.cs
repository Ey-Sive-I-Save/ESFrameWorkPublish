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
                ? "命令" : "技能";
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
                ? "单次命令" : "临时技能";
            context.Report(useName + "节点请求 " + requestId + " 的受控会话启动流程已创建；状态："
                + singleUseDispatchMessage + "。只有出现 Codex 接收事件后才代表已接收，当前不代表开始执行或完成。");
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
            r.Add(FieldSummary(p, "技能核心字段"));
            var a = Text("技能名称（英文小写）", p.skillName, fieldName: nameof(p.skillName));
            var b = Text("正式目录（系统）", p.targetProjectPath,
                fieldName: nameof(p.targetProjectPath));
            b.tooltip = b.tooltip + "\n正式技能目录，优先从下拉列表选择。";
            VisualElement picker = SearchPicker(
                "选择已有技能",
                "搜索已有技能",
                "按技能目录名搜索已有技能；选择后会同步目录和技能名称。",
                () => ESAgentAuthoringAssetCatalog.GetAgentSkillTargets(b.value, true),
                out Button pickerButton);
            pickerButton.clicked += () =>
            {
                ESSearchDropdown.Open(
                    pickerButton,
                        "选择已有技能",
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
            AddToggle(r, "检查命令格式", () => p.validateAICommand,
                v => p.validateAICommand = v, commit, nameof(p.validateAICommand));
            AddToggle(r, "检查技能结构", () => p.validateAgentSkill,
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
            root.Add(CardToggle(context, "es-node-card-validation-command", "检查命令", p.validateAICommand,
                value => p.validateAICommand = value, commit, nameof(p.validateAICommand)));
            root.Add(CardToggle(context, "es-node-card-validation-skill", "检查技能", p.validateAgentSkill,
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
