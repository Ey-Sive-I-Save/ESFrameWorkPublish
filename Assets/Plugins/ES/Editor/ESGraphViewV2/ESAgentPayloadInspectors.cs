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

}