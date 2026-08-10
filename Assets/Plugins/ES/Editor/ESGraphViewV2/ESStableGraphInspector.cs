using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ES.EditorInternal
{
    internal static class ESGraphInspectorVisuals
    {
        private const float FieldLabelWidth = 108f;

        public static VisualElement CreateCard(string title, string subtitle = null, int depth = 1,
            string badge = null)
        {
            var card = new VisualElement();
            card.style.marginTop = 5f;
            card.style.marginBottom = 5f;
            card.style.paddingLeft = 10f;
            card.style.paddingRight = 10f;
            card.style.paddingTop = 8f;
            card.style.paddingBottom = 9f;
            card.style.backgroundColor = ESEditorPresentation.GetDepthBackground(depth + 1);
            card.style.borderLeftWidth = 3f;
            card.style.borderLeftColor = ESEditorPresentation.GetDepthAccent(Mathf.Max(0, depth - 1));
            card.style.borderTopWidth = 1f;
            card.style.borderRightWidth = 1f;
            card.style.borderBottomWidth = 1f;
            card.style.borderTopColor = ESEditorPresentation.DividerColor;
            card.style.borderRightColor = ESEditorPresentation.DividerColor;
            card.style.borderBottomColor = ESEditorPresentation.DividerColor;
            card.style.borderTopLeftRadius = 5f;
            card.style.borderTopRightRadius = 5f;
            card.style.borderBottomLeftRadius = 5f;
            card.style.borderBottomRightRadius = 5f;

            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.alignItems = Align.Center;
            headerRow.style.marginBottom = string.IsNullOrWhiteSpace(subtitle) ? 6f : 2f;
            var header = new Label(title ?? string.Empty);
            header.style.flexGrow = 1f;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.fontSize = 12f;
            header.style.color = ESEditorPresentation.SectionSelectedTextColor;
            headerRow.Add(header);
            if (!string.IsNullOrWhiteSpace(badge))
                headerRow.Add(CreateBadge(badge, ESEditorPresentation.GetDepthAccent(depth)));
            card.Add(headerRow);

            if (!string.IsNullOrWhiteSpace(subtitle) && ESEditorPresentation.ShowSectionSubtitle)
            {
                var description = new Label(subtitle);
                description.style.whiteSpace = WhiteSpace.Normal;
                description.style.color = ESEditorPresentation.EmptyTextColor;
                description.style.fontSize = 10f;
                description.style.marginBottom = 7f;
                card.Add(description);
            }
            return card;
        }

        public static Label CreateBadge(string text, Color accent)
        {
            var badge = new Label(text ?? string.Empty);
            badge.style.paddingLeft = 6f;
            badge.style.paddingRight = 6f;
            badge.style.paddingTop = 2f;
            badge.style.paddingBottom = 2f;
            badge.style.marginLeft = 6f;
            badge.style.fontSize = 9f;
            badge.style.unityFontStyleAndWeight = FontStyle.Bold;
            badge.style.color = Color.white;
            badge.style.backgroundColor = new Color(accent.r, accent.g, accent.b, 0.86f);
            badge.style.borderTopLeftRadius = 4f;
            badge.style.borderTopRightRadius = 4f;
            badge.style.borderBottomLeftRadius = 4f;
            badge.style.borderBottomRightRadius = 4f;
            return badge;
        }

        public static VisualElement CreateNotice(string text, HelpBoxMessageType type)
        {
            Color accent = GetMessageAccent(type);
            var notice = new VisualElement();
            notice.style.marginTop = 5f;
            notice.style.marginBottom = 5f;
            notice.style.paddingLeft = 9f;
            notice.style.paddingRight = 8f;
            notice.style.paddingTop = 7f;
            notice.style.paddingBottom = 7f;
            notice.style.backgroundColor = Color.Lerp(ESEditorPresentation.GetDepthBackground(2), accent,
                ESEditorPresentation.IsProSkin ? 0.13f : 0.08f);
            notice.style.borderLeftWidth = 3f;
            notice.style.borderLeftColor = accent;
            notice.style.borderTopLeftRadius = 4f;
            notice.style.borderTopRightRadius = 4f;
            notice.style.borderBottomLeftRadius = 4f;
            notice.style.borderBottomRightRadius = 4f;
            var label = new Label(text ?? string.Empty);
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.fontSize = 10f;
            label.style.color = ESEditorPresentation.SectionTextColor;
            notice.Add(label);
            return notice;
        }

        public static VisualElement CreateActionRow()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            row.style.marginTop = 5f;
            row.style.marginBottom = 2f;
            return row;
        }

        public static Button CreateButton(string text, string tooltip, Action action,
            bool primary = false, bool danger = false)
        {
            var button = new Button(action) { text = text, tooltip = tooltip };
            StyleButton(button, primary, danger);
            return button;
        }

        public static void StyleButton(Button button, bool primary = false, bool danger = false)
        {
            if (button == null)
                return;
            button.style.minHeight = 29f;
            button.style.minWidth = 132f;
            button.style.flexGrow = 1f;
            button.style.marginLeft = 2f;
            button.style.marginRight = 2f;
            button.style.marginTop = 2f;
            button.style.marginBottom = 2f;
            button.style.unityFontStyleAndWeight = primary ? FontStyle.Bold : FontStyle.Normal;
            if (danger)
            {
                Color dangerColor = ESEditorPresentation.GetStatusAccent(0, ESStatusKind.Error);
                button.style.color = dangerColor;
                button.style.backgroundColor = Color.Lerp(ESEditorPresentation.GetDepthBackground(2),
                    dangerColor, 0.12f);
            }
            else if (primary)
            {
                button.style.backgroundColor = ESEditorPresentation.GetSelectorBackground(0);
                button.style.color = ESEditorPresentation.SelectedTextColor;
            }
        }

        public static void StyleField<T>(BaseField<T> field, bool stacked = false)
        {
            if (field == null)
                return;
            field.style.marginTop = 3f;
            field.style.marginBottom = 3f;
            field.style.minHeight = 23f;
            field.labelElement.style.color = ESEditorPresentation.SectionTextColor;
            field.labelElement.style.fontSize = 10f;
            if (stacked)
            {
                field.style.flexDirection = FlexDirection.Column;
                field.labelElement.style.width = StyleKeyword.Auto;
                field.labelElement.style.minWidth = 0f;
                field.labelElement.style.marginBottom = 3f;
            }
            else
            {
                field.labelElement.style.width = FieldLabelWidth;
                field.labelElement.style.minWidth = FieldLabelWidth;
            }
        }

        public static void StyleTextField(TextField field)
        {
            if (field == null)
                return;
            StyleField(field, field.multiline);
            if (field.multiline)
                field.style.minHeight = 74f;
        }

        public static void StylePickerRow(VisualElement row, Label label, Button pickerButton)
        {
            if (row == null || label == null || pickerButton == null)
                return;
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.minHeight = 27f;
            row.style.marginTop = 3f;
            row.style.marginBottom = 3f;
            label.style.width = FieldLabelWidth;
            label.style.minWidth = FieldLabelWidth;
            label.style.fontSize = 10f;
            label.style.color = ESEditorPresentation.SectionTextColor;
            pickerButton.style.flexGrow = 1f;
            pickerButton.style.minWidth = 0f;
            pickerButton.style.minHeight = 24f;
            pickerButton.style.unityTextAlign = TextAnchor.MiddleLeft;
            pickerButton.style.backgroundColor = ESEditorPresentation.NeutralSelectorBackground;
        }

        public static void StyleFoldout(Foldout foldout, int depth = 2)
        {
            if (foldout == null)
                return;
            foldout.style.marginTop = 5f;
            foldout.style.marginBottom = 5f;
            foldout.style.paddingLeft = 7f;
            foldout.style.paddingRight = 7f;
            foldout.style.paddingTop = 5f;
            foldout.style.paddingBottom = 7f;
            foldout.style.backgroundColor = ESEditorPresentation.GetDepthBackground(depth);
            foldout.style.borderLeftWidth = 2f;
            foldout.style.borderLeftColor = ESEditorPresentation.GetDepthAccent(Mathf.Max(0, depth - 1));
            foldout.style.borderBottomWidth = 1f;
            foldout.style.borderBottomColor = ESEditorPresentation.DividerColor;
        }

        public static void StylePayloadRoot(VisualElement root)
        {
            if (root == null)
                return;
            root.style.marginTop = 2f;
            root.style.marginBottom = 2f;
            root.Query<TextField>().ForEach(StyleTextField);
            root.Query<Toggle>().ForEach(field => StyleField(field));
            root.Query<PopupField<string>>().ForEach(field => StyleField(field));
            root.Query<ObjectField>().ForEach(field => StyleField(field));
            root.Query<HelpBox>().ForEach(helpBox =>
            {
                helpBox.style.marginTop = 4f;
                helpBox.style.marginBottom = 5f;
            });
        }

        public static Color GetMessageAccent(HelpBoxMessageType type)
        {
            switch (type)
            {
                case HelpBoxMessageType.Error:
                    return ESEditorPresentation.GetStatusAccent(0, ESStatusKind.Error);
                case HelpBoxMessageType.Warning:
                    return ESEditorPresentation.GetStatusAccent(0, ESStatusKind.Warning);
                case HelpBoxMessageType.Info:
                    return ESEditorPresentation.GetDepthAccent(0);
                default:
                    return ESEditorPresentation.SectionMarkerColor;
            }
        }
    }

    internal sealed class ESStableGraphInspector : VisualElement
    {
        private enum InspectorTargetKind : byte
        {
            Graph,
            Node,
            Edge,
            Multiple
        }

        private sealed class PickerValue<T>
        {
            public T Value;

            public PickerValue(T value)
            {
                Value = value;
            }
        }

        private readonly struct PortRelationTarget
        {
            public readonly string NodeId;
            public readonly string NodeName;
            public readonly string PortName;

            public PortRelationTarget(string nodeId, string nodeName, string portName)
            {
                NodeId = nodeId;
                NodeName = nodeName;
                PortName = portName;
            }
        }

        private const long ValidationDelayMilliseconds = 250L;
        private static readonly ESGraphPortValueKind[] EditablePortValueKinds =
        {
            ESGraphPortValueKind.Flow,
            ESGraphPortValueKind.Any,
            ESGraphPortValueKind.Boolean,
            ESGraphPortValueKind.Number,
            ESGraphPortValueKind.Text,
            ESGraphPortValueKind.Object,
            ESGraphPortValueKind.AgentContext,
            ESGraphPortValueKind.AgentRequirement,
            ESGraphPortValueKind.AgentArtifact,
            ESGraphPortValueKind.Custom
        };
        private readonly Action rebuildGraph;
        private readonly Action<string> report;
        private readonly Action<string> locate;
        private readonly Action requestAutoSave;
        private readonly ESGraphEditService editService;
        private readonly EditorWindow hostWindow;
        private readonly VisualElement headerContainer;
        private readonly Label contextLabel;
        private readonly Label titleLabel;
        private readonly Label subtitleLabel;
        private readonly ScrollView details;
        private readonly VisualElement validationPanel;
        private readonly ScrollView issueList;
        private readonly Label issueSummary;
        private readonly Label issueMeta;
        private readonly Button issueToggle;
        private IVisualElementScheduledItem validationSchedule;
        private ESGraphAsset asset;
        private InspectorTargetKind currentTarget;
        private string currentElementId;
        private int selectedNodeCount;
        private int selectedEdgeCount;
        private int validationRevision;
        private int validatedRevision = -1;
        private bool issueListExpanded;
        private bool issueExpansionInitialized;
        private ESGraphOdinPayloadSession odinPayloadSession;

        public ESStableGraphInspector(EditorWindow hostWindow, Action rebuildGraph, Action<string> report, Action<string> locate,
            Action requestAutoSave = null, ESGraphEditService editService = null)
        {
            this.hostWindow = hostWindow;
            this.rebuildGraph = rebuildGraph;
            this.report = report;
            this.locate = locate;
            this.requestAutoSave = requestAutoSave;
            this.editService = editService;
            style.minWidth = 340f;
            style.flexGrow = 1f;
            style.backgroundColor = ESEditorPresentation.GetDepthBackground(3);
            style.borderLeftWidth = 1f;
            style.borderLeftColor = ESEditorPresentation.DividerColor;

            headerContainer = new VisualElement();
            headerContainer.style.paddingLeft = 13f;
            headerContainer.style.paddingRight = 12f;
            headerContainer.style.paddingTop = 9f;
            headerContainer.style.paddingBottom = 9f;
            headerContainer.style.minHeight = 67f;
            headerContainer.style.backgroundColor = ESEditorPresentation.GetDepthBackground(1);
            headerContainer.style.borderLeftWidth = 4f;
            headerContainer.style.borderLeftColor = ESEditorPresentation.GetDepthAccent(0);
            headerContainer.style.borderBottomWidth = 1f;
            headerContainer.style.borderBottomColor = ESEditorPresentation.DividerColor;
            contextLabel = new Label("图资产");
            contextLabel.style.fontSize = 9f;
            contextLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            contextLabel.style.color = ESEditorPresentation.SectionMutedTextColor;
            contextLabel.style.marginBottom = 1f;
            headerContainer.Add(contextLabel);

            titleLabel = new Label("图属性");
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.fontSize = 14f;
            titleLabel.style.color = ESEditorPresentation.SectionSelectedTextColor;
            titleLabel.style.whiteSpace = WhiteSpace.NoWrap;
            titleLabel.style.overflow = Overflow.Hidden;
            titleLabel.style.textOverflow = TextOverflow.Ellipsis;
            headerContainer.Add(titleLabel);

            subtitleLabel = new Label("选择图资产后显示业务设置与检查结果");
            subtitleLabel.style.fontSize = 10f;
            subtitleLabel.style.color = ESEditorPresentation.EmptyTextColor;
            subtitleLabel.style.whiteSpace = WhiteSpace.NoWrap;
            subtitleLabel.style.overflow = Overflow.Hidden;
            subtitleLabel.style.textOverflow = TextOverflow.Ellipsis;
            subtitleLabel.style.marginTop = 2f;
            headerContainer.Add(subtitleLabel);
            Add(headerContainer);

            details = new ScrollView(ScrollViewMode.Vertical);
            details.style.flexGrow = 1f;
            details.style.paddingLeft = 7f;
            details.style.paddingRight = 7f;
            details.style.paddingTop = 5f;
            details.style.paddingBottom = 7f;

            validationPanel = new VisualElement();
            validationPanel.style.marginLeft = 7f;
            validationPanel.style.marginRight = 7f;
            validationPanel.style.marginBottom = 7f;
            validationPanel.style.backgroundColor = ESEditorPresentation.GetDepthBackground(1);
            validationPanel.style.borderLeftWidth = 3f;
            validationPanel.style.borderLeftColor = ESEditorPresentation.GetDepthAccent(0);
            validationPanel.style.borderTopWidth = 1f;
            validationPanel.style.borderRightWidth = 1f;
            validationPanel.style.borderBottomWidth = 1f;
            validationPanel.style.borderTopColor = ESEditorPresentation.DividerColor;
            validationPanel.style.borderRightColor = ESEditorPresentation.DividerColor;
            validationPanel.style.borderBottomColor = ESEditorPresentation.DividerColor;
            validationPanel.style.borderTopLeftRadius = 5f;
            validationPanel.style.borderTopRightRadius = 5f;
            validationPanel.style.borderBottomLeftRadius = 5f;
            validationPanel.style.borderBottomRightRadius = 5f;
            var validationHeader = new VisualElement();
            validationHeader.style.flexDirection = FlexDirection.Row;
            validationHeader.style.alignItems = Align.Center;
            validationHeader.style.minHeight = 42f;
            validationHeader.style.paddingLeft = 9f;
            validationHeader.style.paddingRight = 6f;
            var validationText = new VisualElement();
            validationText.style.flexGrow = 1f;
            issueSummary = new Label("质量检查");
            issueSummary.style.unityFontStyleAndWeight = FontStyle.Bold;
            issueSummary.style.color = ESEditorPresentation.SectionTextColor;
            validationText.Add(issueSummary);
            issueMeta = new Label("等待检查");
            issueMeta.style.fontSize = 9f;
            issueMeta.style.color = ESEditorPresentation.SectionMutedTextColor;
            validationText.Add(issueMeta);
            validationHeader.Add(validationText);
            issueToggle = new Button(() => SetIssueListExpanded(!issueListExpanded))
            {
                text = "展开",
                tooltip = "展开或折叠详细问题列表。"
            };
            issueToggle.style.width = 54f;
            issueToggle.style.minHeight = 24f;
            validationHeader.Add(issueToggle);
            validationPanel.Add(validationHeader);

            issueList = new ScrollView(ScrollViewMode.Vertical);
            issueList.style.height = 0f;
            issueList.style.minHeight = 0f;
            issueList.style.maxHeight = 220f;
            issueList.style.borderTopWidth = 1f;
            issueList.style.borderTopColor = ESEditorPresentation.DividerColor;
            issueList.style.display = DisplayStyle.None;
            validationPanel.Add(issueList);
            Add(validationPanel);
            // 质量结论必须出现在首屏，业务详情随后展开；避免用户先滚到底部才知道图能否继续。
            Add(details);
            RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                CancelScheduledValidation();
                ClearOdinPayloadSession();
            });
        }

        public void SetAsset(ESGraphAsset value)
        {
            asset = value;
            validationRevision++;
            currentTarget = InspectorTargetKind.Graph;
            currentElementId = null;
            selectedNodeCount = 0;
            selectedEdgeCount = 0;
            ShowGraphInspector();
            RefreshValidation();
        }

        public void SetSelection(IEnumerable<ISelectable> selection)
        {
            List<ISelectable> selected = selection?.Where(item => item != null).ToList()
                ?? new List<ISelectable>();
            selectedNodeCount = selected.Count(item => item is ESStableGraphNodeView);
            selectedEdgeCount = selected.Count(item => item is Edge);
            if (selected.Count > 1)
            {
                currentTarget = InspectorTargetKind.Multiple;
                currentElementId = null;
                ShowMultipleSelectionInspector();
            }
            else if (selected.Count == 1 && selected[0] is ESStableGraphNodeView nodeView)
            {
                currentTarget = InspectorTargetKind.Node;
                currentElementId = nodeView.NodeId;
                ShowNodeInspector(currentElementId);
            }
            else if (selected.Count == 1 && selected[0] is Edge edge && edge.userData is string edgeId)
            {
                currentTarget = InspectorTargetKind.Edge;
                currentElementId = edgeId;
                ShowEdgeInspector(currentElementId);
            }
            else
            {
                currentTarget = InspectorTargetKind.Graph;
                currentElementId = null;
                ShowGraphInspector();
            }
        }

        public void RefreshFromAsset()
        {
            validationRevision++;
            if (currentTarget == InspectorTargetKind.Node)
                ShowNodeInspector(currentElementId);
            else if (currentTarget == InspectorTargetKind.Edge)
                ShowEdgeInspector(currentElementId);
            else if (currentTarget == InspectorTargetKind.Multiple)
                ShowMultipleSelectionInspector();
            else
                ShowGraphInspector();
            RequestValidation();
        }

        public void NotifyAssetChanged()
        {
            validationRevision++;
            RequestValidation();
        }

        public void ShowIssues(List<ESGraphValidationIssue> issues)
        {
            validatedRevision = validationRevision;
            issueList.Clear();
            if (asset == null)
            {
                issueSummary.text = "等待图资产";
                issueSummary.style.color = ESEditorPresentation.SectionTextColor;
                issueMeta.text = "打开或拖入图资产后自动检查";
                validationPanel.style.borderLeftColor = ESEditorPresentation.GetDepthAccent(0);
                issueToggle.text = "—";
                issueToggle.SetEnabled(false);
                issueExpansionInitialized = false;
                SetIssueListExpanded(false);
                return;
            }
            if (issues == null || issues.Count == 0)
            {
                Color success = new Color(0.38f, 0.78f, 0.58f, 1f);
                issueSummary.text = "质量检查通过";
                issueSummary.style.color = success;
                issueMeta.text = "0 个问题 · 可以继续执行或生成";
                validationPanel.style.borderLeftColor = success;
                issueToggle.text = "通过";
                issueToggle.SetEnabled(false);
                issueExpansionInitialized = false;
                SetIssueListExpanded(false);
                return;
            }

            int errorCount = 0;
            int warningCount = 0;
            int infoCount = 0;
            for (int i = 0; i < issues.Count; i++)
            {
                ESGraphValidationIssue issue = issues[i];
                if (issue == null)
                    continue;
                if (issue.severity == ESGraphValidationSeverity.Error)
                    errorCount++;
                else if (issue.severity == ESGraphValidationSeverity.Warning)
                    warningCount++;
                else
                    infoCount++;
            }

            Color statusColor = errorCount > 0
                ? ESEditorPresentation.GetStatusAccent(0, ESStatusKind.Error)
                : ESEditorPresentation.GetStatusAccent(0, ESStatusKind.Warning);
            issueSummary.text = errorCount > 0 ? "存在阻断问题" : "存在改进建议";
            issueSummary.style.color = statusColor;
            issueMeta.text = errorCount + " 错误 · " + warningCount + " 提醒"
                + (infoCount > 0 ? " · " + infoCount + " 信息" : string.Empty);
            validationPanel.style.borderLeftColor = statusColor;
            issueToggle.SetEnabled(true);
            for (int i = 0; i < issues.Count; i++)
            {
                ESGraphValidationIssue issue = issues[i];
                if (issue == null)
                    continue;
                Color severityColor = issue.severity == ESGraphValidationSeverity.Error
                    ? ESEditorPresentation.GetStatusAccent(0, ESStatusKind.Error)
                    : issue.severity == ESGraphValidationSeverity.Warning
                        ? ESEditorPresentation.GetStatusAccent(0, ESStatusKind.Warning)
                        : ESEditorPresentation.GetDepthAccent(0);
                Button button = new Button(() =>
                {
                    if (!string.IsNullOrEmpty(issue.elementId))
                        locate?.Invoke(issue.elementId);
                }) { tooltip = issue.code + (string.IsNullOrEmpty(issue.elementId) ? string.Empty : "\n元素：" + issue.elementId) };
                button.style.flexDirection = FlexDirection.Column;
                button.style.alignItems = Align.Stretch;
                button.style.unityTextAlign = TextAnchor.MiddleLeft;
                button.style.minHeight = 43f;
                button.style.marginLeft = 6f;
                button.style.marginRight = 6f;
                button.style.marginTop = 4f;
                button.style.marginBottom = 3f;
                button.style.paddingLeft = 9f;
                button.style.paddingRight = 8f;
                button.style.paddingTop = 5f;
                button.style.paddingBottom = 5f;
                button.style.borderLeftWidth = 3f;
                button.style.borderLeftColor = severityColor;
                button.style.backgroundColor = Color.Lerp(ESEditorPresentation.GetDepthBackground(2),
                    severityColor, 0.08f);
                var message = new Label(issue.message ?? string.Empty);
                message.style.whiteSpace = WhiteSpace.Normal;
                message.style.color = ESEditorPresentation.SectionTextColor;
                message.style.fontSize = 10f;
                button.Add(message);
                var code = new Label((issue.severity == ESGraphValidationSeverity.Error ? "错误" :
                    issue.severity == ESGraphValidationSeverity.Warning ? "提醒" : "信息")
                    + " · " + (issue.code ?? "未分类"));
                code.style.fontSize = 9f;
                code.style.color = severityColor;
                code.style.marginTop = 2f;
                button.Add(code);
                issueList.Add(button);
            }
            if (!issueExpansionInitialized)
            {
                issueExpansionInitialized = true;
                issueListExpanded = errorCount > 0;
            }
            SetIssueListExpanded(issueListExpanded);
        }

        private void SetHeader(string context, string title, string subtitle, Color accent)
        {
            contextLabel.text = context ?? string.Empty;
            titleLabel.text = title ?? string.Empty;
            titleLabel.tooltip = title ?? string.Empty;
            subtitleLabel.text = subtitle ?? string.Empty;
            subtitleLabel.tooltip = subtitle ?? string.Empty;
            headerContainer.style.borderLeftColor = accent;
            titleLabel.style.color = accent;
        }

        private void SetIssueListExpanded(bool expanded)
        {
            issueListExpanded = expanded && issueList.childCount > 0;
            issueList.style.display = issueListExpanded ? DisplayStyle.Flex : DisplayStyle.None;
            issueList.style.height = issueListExpanded
                ? Mathf.Clamp(18f + Mathf.Min(issueList.childCount, 4) * 52f, 76f, 220f)
                : 0f;
            issueList.style.minHeight = issueListExpanded ? 64f : 0f;
            if (issueToggle.enabledSelf)
                issueToggle.text = issueListExpanded ? "收起" : "展开";
        }

        private void ShowGraphInspector()
        {
            ClearDetails();
            if (asset == null)
            {
                SetHeader("图资产", "尚未打开图", "新建、打开或拖入图资产后开始编辑",
                    ESEditorPresentation.GetDepthAccent(0));
                details.Add(ESGraphInspectorVisuals.CreateNotice(
                    "请先点击顶部“新建图”，或从 Project 窗口拖入一张图资产。",
                    HelpBoxMessageType.Info));
                return;
            }

            IReadOnlyList<IESGraphAuthoringProfile> profiles = ESGraphAuthoringRegistry.AllProfiles;
            IESGraphAuthoringProfile currentProfile = profiles.FirstOrDefault(profile =>
                profile.Domain.Equals(asset.DomainKey));
            string domainName = currentProfile?.DisplayName
                ?? ESGraphChinesePresentation.GetDomainKindName(asset.DomainKind);
            SetHeader("图资产", asset.name,
                domainName + " · " + asset.Nodes.Count + " 个节点 · " + asset.Edges.Count + " 条关系",
                ESEditorPresentation.GetDepthAccent(0));

            VisualElement overview = CreateSection("图概览", "先确认用途与规模，再进入业务配置和执行流程。", 1,
                asset.Nodes.Count + " 节点");
            AddKeyValue(overview, "图用途", domainName);
            AddKeyValue(overview, "内容规模", asset.Nodes.Count + " 个节点 · " + asset.Edges.Count + " 条关系");
            overview.Add(ESGraphInspectorVisuals.CreateNotice(
                "添加节点 → 拖线建立关系 → 填写业务内容 → 质量检查。修改会进入自动保存队列。",
                HelpBoxMessageType.Info));
            details.Add(overview);

            VisualElement settings = CreateSection("业务设置", "领域方案决定可用节点、端口语义和校验规则。", 1);
            if (profiles.Count > 0)
            {
                VisualElement profileField = CreateSearchPickerField(
                    "领域方案",
                    currentProfile?.DisplayName ?? ESGraphChinesePresentation.GetDomainKindName(asset.DomainKind),
                    "领域方案决定可以添加哪些节点，以及这些节点的端口规则。普通使用保持默认即可。",
                    out Button profileButton);
                profileButton.clicked += () =>
                {
                    ESSearchDropdown.Open(
                        profileButton,
                        hostWindow,
                        "选择图领域方案",
                        () => profiles.Select(profile =>
                        {
                            IESGraphAuthoringProfile selected = profile;
                            bool isCurrent = asset.DomainKey.Equals(selected.Domain);
                            return ESSearchDropdown.Entry.Item(
                                selected.DisplayName,
                                () => SelectProfile(selected),
                                subtitle: selected.Description,
                                badge: isCurrent ? "当前" : null,
                                selected: isCurrent);
                        }),
                        minimumWindowSize: new Vector2(520f, 320f));
                };
                settings.Add(profileField);
                if (currentProfile != null)
                    settings.Add(ESGraphInspectorVisuals.CreateNotice(currentProfile.Description,
                        HelpBoxMessageType.Info));
                else
                    settings.Add(ESGraphInspectorVisuals.CreateNotice(
                        "当前领域没有注册方案，但已注册的独立节点定义仍可正常使用。",
                        HelpBoxMessageType.Warning));
            }
            details.Add(settings);

            Foldout advanced = CreateAdvancedFoldout("高级图结构与身份");
            AddReadOnlyText(advanced, "资产路径", AssetDatabase.GetAssetPath(asset));
            AddReadOnlyText(advanced, "数据版本", asset.schemaVersion.ToString());
            AddReadOnlyText(advanced, "领域标识", asset.DomainId);
            Toggle cycles = new Toggle("允许循环（高级）") { value = asset.allowCycles };
            cycles.tooltip = "允许节点关系形成闭环。Agent 产物编排和行为树通常不应开启。";
            ESGraphInspectorVisuals.StyleField(cycles);
            cycles.RegisterValueChangedCallback(evt =>
            {
                Undo.RecordObject(asset, "修改图循环规则");
                asset.allowCycles = evt.newValue;
                MarkChanged("已修改循环规则");
            });
            advanced.Add(cycles);
            details.Add(advanced);

            VisualElement workflow = CreateSection("检查、执行与产物",
                "先消除阻断问题，再选择即时使用或永久化。", 1, "业务出口");
            VisualElement checkActions = ESGraphInspectorVisuals.CreateActionRow();
            checkActions.Add(ESGraphInspectorVisuals.CreateButton("立即检查", "检查图的完整性、连线和领域规则。",
                ForceValidation, true));
            checkActions.Add(ESGraphInspectorVisuals.CreateButton("生成检查快照",
                "生成只读检查结果，供后续流程使用，不会直接运行图。", BakeSnapshot));
            workflow.Add(checkActions);
            if (asset.DomainKind == ESGraphDomainKind.AgentAuthoring)
            {
                if (ESAgentAuthoringGraphValidator.TryGetFinalPurpose(asset,
                        out string finalPurpose, out string successCriteria))
                {
                    workflow.Add(ESGraphInspectorVisuals.CreateNotice(
                        "最终目的：" + finalPurpose + "\n成功标准：" + successCriteria,
                        HelpBoxMessageType.Info));
                }
                else
                {
                    workflow.Add(ESGraphInspectorVisuals.CreateNotice(
                        "请先填写唯一的“最终目的”和“成功标准”。未明确最终目的时，即时执行、复制和永久产物生成都会被阻断。",
                        HelpBoxMessageType.Error));
                }
                VisualElement immediate = CreateSection("即时使用",
                    "同类 Output 唯一时可从这里使用；多个 Output 请直接使用目标节点卡片。", 2, "本次任务");
                int commandOutputCount = asset.Nodes.Count(node => node != null
                    && node.BuiltInKind == ESGraphBuiltInNodeKind.AgentAICommandOutput);
                int skillOutputCount = asset.Nodes.Count(node => node != null
                    && node.BuiltInKind == ESGraphBuiltInNodeKind.AgentSkillOutput);
                bool hasCommandOutput = commandOutputCount > 0;
                bool hasSkillOutput = skillOutputCount > 0;
                VisualElement immediateActions = ESGraphInspectorVisuals.CreateActionRow();
                Button useAsCommand = ESGraphInspectorVisuals.CreateButton("作为单次 Command",
                    "只使用 AICommand Output 关联的整图分支执行一次；不生成或安装永久产物。",
                    () => SendSingleUseAgentArtifact(ESAgentArtifactKind.AICommand), true);
                useAsCommand.SetEnabled(commandOutputCount == 1);
                immediateActions.Add(useAsCommand);
                Button useAsSkill = ESGraphInspectorVisuals.CreateButton("作为临时 Skill",
                    "只在本次任务中使用 Agent Skill Output 关联的整图分支；不会写入 .agents/skills。",
                    () => SendSingleUseAgentArtifact(ESAgentArtifactKind.AgentSkill));
                useAsSkill.SetEnabled(skillOutputCount == 1);
                immediateActions.Add(useAsSkill);
                Button copyGraph = null;
                copyGraph = new Button(() => OpenAgentCopyMenu(copyGraph))
                {
                    text = "复制整图…",
                    tooltip = "复制即时执行提示、生成/更新请求 JSON，或包含 Mermaid 的完整中文图说明。"
                };
                ESGraphInspectorVisuals.StyleButton(copyGraph);
                immediateActions.Add(copyGraph);
                immediate.Add(immediateActions);
                if (commandOutputCount > 1 || skillOutputCount > 1)
                {
                    immediate.Add(ESGraphInspectorVisuals.CreateNotice(
                        "检测到多个同类 Output。为避免执行错误分支，全局即时入口已禁用；请在目标 Output 节点卡片中执行局部使用。",
                        HelpBoxMessageType.Warning));
                }
                workflow.Add(immediate);

                VisualElement permanent = CreateSection("永久化流程",
                    "候选隔离 → Diff 审查 → 人工批准 → 独立窗口实现。", 2, "可持续更新");
                VisualElement saveActions = ESGraphInspectorVisuals.CreateActionRow();
                Button saveCommand = ESGraphInspectorVisuals.CreateButton("保存为 AICommand 候选",
                    "只保留 AICommand Output 关联分支，写入隔离候选目录。",
                    () => SendAgentGenerationRequest(ESAgentArtifactKind.AICommand), true);
                saveCommand.SetEnabled(hasCommandOutput);
                saveActions.Add(saveCommand);
                Button saveSkill = ESGraphInspectorVisuals.CreateButton("保存为 Agent Skill 候选",
                    "只保留 Agent Skill Output 关联分支，写入隔离候选目录。",
                    () => SendAgentGenerationRequest(ESAgentArtifactKind.AgentSkill));
                saveSkill.SetEnabled(hasSkillOutput);
                saveActions.Add(saveSkill);
                Button saveAll = ESGraphInspectorVisuals.CreateButton("同时保存",
                    "把整图声明的全部 AICommand 与 Agent Skill Output 放入同一候选请求。",
                    SendAgentGenerationRequest);
                saveAll.SetEnabled(hasCommandOutput && hasSkillOutput);
                saveActions.Add(saveAll);
                permanent.Add(saveActions);
                permanent.Add(CreateWorkflowStep("2", "审查并批准",
                    "查看新增、删除与修改差异，确认后才允许导入正式位置。",
                    ESGraphInspectorVisuals.CreateButton("查看候选差异",
                        "打开候选 Diff 审查窗口。", ESAgentArtifactCandidateReviewWindow.OpenLatest)));
                if (hasCommandOutput)
                {
                    Button launchImplementation = null;
                    launchImplementation = new Button(() => LaunchApprovedAgentImplementation(launchImplementation))
                    {
                        text = "打开新窗口执行实现",
                        tooltip = "验证批准清单与正式 AICommand 的 SHA-256 后，使用项目权威启动器打开独立 Codex 窗口。"
                    };
                    launchImplementation.SetEnabled(!ESAgentImplementationSessionLauncher.IsLaunching);
                    ESGraphInspectorVisuals.StyleButton(launchImplementation);
                    permanent.Add(CreateWorkflowStep("3", "执行正式实现",
                        "仅对已批准且哈希未变化的 AICommand 开启独立实现窗口。", launchImplementation));
                }
                workflow.Add(permanent);

                Button repairPorts = ESGraphInspectorVisuals.CreateButton("修复节点端口规则",
                    "按当前领域方案恢复端口名称、类型、方向，并保留稳定身份。", () =>
                {
                    Undo.RecordObject(asset, "修复 Agent Authoring 端口规则");
                    if (!ESAgentAuthoringGraphSchema.TryRepairPorts(asset, out string error))
                    {
                        report?.Invoke(error);
                        return;
                    }
                    EditorUtility.SetDirty(asset);
                    requestAutoSave?.Invoke();
                    rebuildGraph?.Invoke();
                    RefreshValidation();
                    report?.Invoke("已修复 Agent Authoring 节点端口规则，并保留 Node/Port/Edge 稳定身份。");
                });
                workflow.Add(repairPorts);
            }
            details.Add(workflow);
            // 主要动作紧跟图概览，技术设置与身份详情后置，减少“先读实现细节才能执行”的认知负担。
            details.Remove(workflow);
            details.Insert(1, workflow);
        }

        private void ShowMultipleSelectionInspector()
        {
            ClearDetails();
            int total = selectedNodeCount + selectedEdgeCount;
            SetHeader("批量选择", total + " 个图元素",
                selectedNodeCount + " 个节点 · " + selectedEdgeCount + " 条关系",
                ESEditorPresentation.GetDepthAccent(1));
            VisualElement summary = CreateSection("选择概览",
                "位置整理使用顶部或右键菜单中的“整理”；复制只处理节点。", 1);
            AddKeyValue(summary, "已选择", total + " 个图元素");
            AddKeyValue(summary, "节点", selectedNodeCount.ToString());
            AddKeyValue(summary, "关系", selectedEdgeCount.ToString());
            summary.Add(ESGraphInspectorVisuals.CreateNotice(
                "快捷键：Ctrl/Cmd+D 复制选中节点，F 聚焦选择，Delete 删除。批量操作只写入一次 Undo 事务。",
                HelpBoxMessageType.None));
            details.Add(summary);
        }

        private void SelectProfile(IESGraphAuthoringProfile selected)
        {
            if (asset == null || selected == null
                || asset.DomainKey.Equals(selected.Domain))
                return;
            if (asset.Nodes.Count > 0 || asset.Edges.Count > 0)
            {
                report?.Invoke("已有内容的图不能直接切换领域；请先迁移或创建新资产。");
                return;
            }

            Undo.RecordObject(asset, "修改图领域");
            if (!asset.TrySetDomain(selected.Domain, out string error))
            {
                report?.Invoke(error);
                return;
            }

            MarkChanged("领域已切换：" + selected.DisplayName);
            ShowGraphInspector();
            rebuildGraph?.Invoke();
        }

        private static VisualElement CreateSearchPickerField(string labelText, string currentValue, string tooltip,
            out Button pickerButton)
        {
            var row = new VisualElement();
            var label = new Label(labelText);
            row.Add(label);

            pickerButton = new Button
            {
                text = (string.IsNullOrWhiteSpace(currentValue) ? "请选择" : currentValue) + "  ▼",
                tooltip = tooltip
            };
            row.Add(pickerButton);
            ESGraphInspectorVisuals.StylePickerRow(row, label, pickerButton);
            return row;
        }

        private void ShowNodeInspector(string nodeId)
        {
            ClearDetails();
            ESGraphNodeRecord node = asset?.FindNode(nodeId);
            if (node == null)
            {
                SetHeader("节点", "节点不可用", "节点不存在或已删除",
                    ESEditorPresentation.GetStatusAccent(0, ESStatusKind.Warning));
                details.Add(ESGraphInspectorVisuals.CreateNotice("节点不存在或已删除。",
                    HelpBoxMessageType.Warning));
                return;
            }

            bool schemaLocked = ESGraphAuthoringRegistry.TryGetNodeDefinition(asset.DomainKey, node.TypeKey,
                out IESGraphNodeDefinition definition);
            string typeName = definition?.DisplayName
                ?? ESGraphChinesePresentation.GetNodeTypeName(asset.DomainId, node.typeId);
            string categoryName = definition == null ? "自定义节点"
                : ESGraphChinesePresentation.GetNodeCategoryName(definition.Category);
            SetHeader("节点", string.IsNullOrWhiteSpace(node.title) ? typeName : node.title,
                typeName + " · " + categoryName, ESGraphNodeThemePalette.GetAccentColor(definition));

            if (!string.IsNullOrWhiteSpace(definition?.Description))
                details.Add(ESGraphInspectorVisuals.CreateNotice(definition.Description,
                    HelpBoxMessageType.Info));

            Dictionary<string, List<PortRelationTarget>> relationSummaries =
                BuildPortRelationSummaries(node);
            int inputCount = 0;
            int outputCount = 0;
            int connectedPortCount = 0;
            int relationCount = 0;
            if (node.ports != null)
            {
                for (int i = 0; i < node.ports.Count; i++)
                {
                    ESGraphPortRecord port = node.ports[i];
                    if (port == null)
                        continue;
                    if (port.direction == ESGraphPortDirection.Input)
                        inputCount++;
                    else
                        outputCount++;
                    if (relationSummaries.TryGetValue(port.portId,
                            out List<PortRelationTarget> connectedNodes)
                        && connectedNodes != null && connectedNodes.Count > 0)
                    {
                        connectedPortCount++;
                        relationCount += connectedNodes.Count;
                    }
                }
            }
            VisualElement snapshot = CreateSection("节点摘要",
                "先确认节点职责和连接状态，再编辑业务内容。", 1, categoryName);
            AddKeyValue(snapshot, "节点类型", typeName);
            AddKeyValue(snapshot, "端口", inputCount + " 输入 · " + outputCount + " 输出");
            AddKeyValue(snapshot, "关系", relationCount == 0
                ? "未连接" : relationCount + " 条关系 · " + connectedPortCount + " 个端口已连接");
            details.Add(snapshot);

            VisualElement business = CreateSection("业务内容",
                "这里决定节点在实现链中具体要完成什么。", 1, categoryName);
            TextField title = new TextField("标题") { value = node.title ?? string.Empty };
            title.tooltip = "画布和关系说明中显示的业务名称。";
            ESGraphInspectorVisuals.StyleTextField(title);
            business.Add(title);
            TextField payload = null;
            bool hasOdinPayload = TryCreateOdinPayloadInspector(node, out VisualElement odinPayload);
            bool hasSpecializedPayload = false;
            if (hasOdinPayload)
            {
                business.Add(odinPayload);
            }
            else
            {
                hasSpecializedPayload = ESGraphAuthoringRegistry.TryCreatePayloadInspector(
                    asset.DomainKey, node.TypeKey, node.payloadJson,
                    value => CommitPayload(node.nodeId, value), out VisualElement specializedPayload);
                if (hasSpecializedPayload)
                {
                    business.Add(specializedPayload);
                }
                else
                {
                    payload = new TextField("业务内容（JSON）")
                    {
                        value = node.payloadJson ?? string.Empty,
                        multiline = true
                    };
                    payload.tooltip = "当前节点没有注册中文业务编辑器，因此暂时显示结构化内容。";
                    payload.style.minHeight = 100f;
                    ESGraphInspectorVisuals.StyleTextField(payload);
                    business.Add(payload);
                }
            }

            TextField typeId = new TextField("节点类型") { value = node.typeId ?? string.Empty };
            typeId.tooltip = "系统识别节点用途的稳定类型。领域节点通常保持不变。";
            IntegerField version = new IntegerField("数据版本") { value = node.version };
            version.tooltip = "节点数据格式版本。除非进行迁移，否则保持默认值。";
            ESGraphInspectorVisuals.StyleTextField(typeId);
            ESGraphInspectorVisuals.StyleField(version);
            typeId.SetEnabled(!schemaLocked);
            version.SetEnabled(!schemaLocked);
            void CommitNodeEdits(bool includeAdvanced, string successMessage)
            {
                ESGraphNodeRecord current = asset?.FindNode(node.nodeId);
                if (current == null)
                    return;
                string nextTypeId = !includeAdvanced || schemaLocked ? current.typeId : typeId.value;
                int nextVersion = !includeAdvanced || schemaLocked ? current.version : version.value;
                if (!ValidateNodeInput(nextTypeId, nextVersion, out string error))
                {
                    report?.Invoke(error);
                    return;
                }
                string nextTitle = title.value ?? string.Empty;
                string payloadValue = payload != null ? payload.value : current.payloadJson;
                bool projectionChanged = !string.Equals(current.title, nextTitle, StringComparison.Ordinal)
                    || !string.Equals(current.typeId, nextTypeId, StringComparison.Ordinal)
                    || current.version != nextVersion;
                bool payloadChanged = !string.Equals(current.payloadJson, payloadValue, StringComparison.Ordinal);
                if (!projectionChanged && !payloadChanged)
                    return;
                ESGraphEditResult result;
                if (editService != null)
                {
                    result = editService.SetNodeContent(
                        asset, node.nodeId, nextTypeId, nextVersion, nextTitle, payloadValue);
                }
                else
                {
                    Undo.RecordObject(asset, "修改图节点");
                    if (!asset.UpdateNode(node.nodeId, nextTypeId, nextVersion, nextTitle,
                            payloadValue, out error))
                    {
                        report?.Invoke(string.IsNullOrWhiteSpace(error) ? "节点内容更新失败。" : error);
                        return;
                    }
                    result = new ESGraphEditResult
                    {
                        changed = true,
                        rebuildRequired = projectionChanged || payloadChanged
                    };
                }
                if (!result.changed)
                {
                    report?.Invoke(string.IsNullOrWhiteSpace(result.error)
                        ? "节点内容更新失败。" : result.error);
                    return;
                }
                MarkChanged(successMessage);
                if (result.rebuildRequired)
                {
                    rebuildGraph?.Invoke();
                    if (projectionChanged)
                        locate?.Invoke(node.nodeId);
                }
            }

            title.RegisterCallback<FocusOutEvent>(_ =>
                CommitNodeEdits(false, "节点标题已更新，并进入自动保存队列。"));
            if (payload != null)
                payload.RegisterCallback<FocusOutEvent>(_ =>
                    CommitNodeEdits(false, "节点业务内容已更新，并进入自动保存队列。"));

            Button applyNode = new Button(() => CommitNodeEdits(true, "节点内容已更新"))
            {
                text = "立即应用当前更改",
                tooltip = "标题和通用业务内容在离开输入框时会自动更新；此按钮用于立即提交当前全部字段。"
            };
            ESGraphInspectorVisuals.StyleButton(applyNode, true);
            business.Add(applyNode);
            details.Add(business);

            VisualElement relations = CreateSection("链接关系",
                "◀ 输入接收上游信息；输出 ▶ 把结果交给下游。输出端拖到空白处可快速续建。", 1,
                (node.ports?.Count ?? 0) + " 个端口");
            if (node.ports == null || node.ports.Count == 0)
                relations.Add(ESGraphInspectorVisuals.CreateNotice("该节点还没有连接端口。",
                    HelpBoxMessageType.Warning));
            else
            {
                for (int i = 0; i < node.ports.Count; i++)
                {
                    ESGraphPortRecord port = node.ports[i];
                    relationSummaries.TryGetValue(port?.portId ?? string.Empty,
                        out List<PortRelationTarget> connectedNodes);
                    AddPortRelationSummary(relations, port, connectedNodes);
                }
            }
            details.Add(relations);

            Foldout advanced = CreateAdvancedFoldout("高级诊断与结构");
            advanced.Add(ESGraphInspectorVisuals.CreateNotice(
                "这里保存稳定身份、迁移版本和端口结构。普通业务编辑不需要修改。",
                HelpBoxMessageType.None));
            AddReadOnlyText(advanced, "节点编号", node.nodeId);
            advanced.Add(typeId);
            advanced.Add(version);
            if (definition != null)
            {
                AddReadOnlyText(advanced, "定义版本", definition.CurrentVersion.ToString());
                if (node.version < definition.CurrentVersion)
                {
                    advanced.Add(ESGraphInspectorVisuals.CreateNotice(
                        "该节点使用旧版数据，需要先升级后再继续编辑。升级会记录 Undo。",
                        HelpBoxMessageType.Warning));
                    advanced.Add(ESGraphInspectorVisuals.CreateButton("升级节点数据",
                        "迁移到当前节点定义版本。", () => MigrateNode(node.nodeId), true));
                }
                else if (node.version > definition.CurrentVersion)
                {
                    advanced.Add(ESGraphInspectorVisuals.CreateNotice(
                        "该节点来自更高版本，当前编辑器不会自动降级或覆盖数据。",
                        HelpBoxMessageType.Error));
                }
            }
            if (hasOdinPayload || hasSpecializedPayload)
            {
                TextField rawPayload = new TextField("原始业务数据")
                {
                    value = node.payloadJson ?? string.Empty,
                    multiline = true,
                    isReadOnly = true
                };
                rawPayload.style.minHeight = 70f;
                ESGraphInspectorVisuals.StyleTextField(rawPayload);
                advanced.Add(rawPayload);
            }
            if (schemaLocked)
            {
                advanced.Add(ESGraphInspectorVisuals.CreateNotice(
                    "当前节点的端口结构由领域方案管理，只允许查看，不直接修改。",
                    HelpBoxMessageType.Info));
            }
            else if (node.ports != null)
            {
                for (int i = 0; i < node.ports.Count; i++)
                {
                    ESGraphPortRecord port = node.ports[i];
                    if (port != null)
                        AddPortEditor(advanced, node, port);
                }
            }
            if (!schemaLocked)
                AddPortCreator(advanced, node);
            details.Add(advanced);
        }

        private void AddPortEditor(VisualElement parent, ESGraphNodeRecord node, ESGraphPortRecord port)
        {
            Foldout foldout = new Foldout
            {
                text = ESGraphChinesePresentation.GetDirectionName(port.direction) + " · "
                    + ESGraphChinesePresentation.GetPortName(port.name),
                value = false
            };
            ESGraphInspectorVisuals.StyleFoldout(foldout, 2);
            TextField stableKey = new TextField("稳定名称（高级）") { value = port.stableKey ?? string.Empty };
            stableKey.tooltip = "端口的稳定身份。已有连线后不要随意修改。";
            TextField name = new TextField("名称") { value = port.name ?? string.Empty };
            ESGraphPortValueKind initialValueKind = port.ValueKind;
            TextField customValueType = new TextField("自定义数据标识（高级）")
            {
                value = initialValueKind == ESGraphPortValueKind.Custom ? port.valueTypeId ?? string.Empty : string.Empty
            };
            customValueType.tooltip = "只有选择“自定义数据”时才需要填写稳定标识。";
            customValueType.SetEnabled(initialValueKind == ESGraphPortValueKind.Custom);
            VisualElement valueKind = CreateValueKindPicker(initialValueKind,
                out PickerValue<ESGraphPortValueKind> valueKindValue,
                selected => customValueType.SetEnabled(selected == ESGraphPortValueKind.Custom));
            VisualElement direction = CreateDirectionPicker(port.direction, out PickerValue<ESGraphPortDirection> directionValue);
            VisualElement capacity = CreateCapacityPicker(port.capacity, out PickerValue<ESGraphPortCapacity> capacityValue);
            ESGraphInspectorVisuals.StyleTextField(stableKey);
            ESGraphInspectorVisuals.StyleTextField(name);
            ESGraphInspectorVisuals.StyleTextField(customValueType);
            foldout.Add(stableKey);
            foldout.Add(name);
            foldout.Add(valueKind);
            foldout.Add(customValueType);
            foldout.Add(direction);
            foldout.Add(capacity);
            VisualElement actions = new VisualElement();
            actions.style.flexDirection = FlexDirection.Row;
            Button apply = new Button(() =>
            {
                ESGraphPortDirection selectedDirection = directionValue.Value;
                ESGraphPortCapacity selectedCapacity = capacityValue.Value;
                string selectedValueType = ESGraphPortValueCatalog.GetStableId(valueKindValue.Value,
                    customValueType.value);
                if (!asset.CanUpdatePort(port.portId, stableKey.value, selectedValueType,
                    selectedDirection, selectedCapacity, out string error))
                {
                    report?.Invoke(error);
                    return;
                }
                Undo.RecordObject(asset, "修改图端口");
                asset.UpdatePort(port.portId, stableKey.value, name.value, selectedValueType,
                    selectedDirection, selectedCapacity, out _);
                MarkChanged("端口规则已更新");
                rebuildGraph?.Invoke();
                locate?.Invoke(node.nodeId);
            }) { text = "应用" };
            Button delete = new Button(() =>
            {
                if (!EditorUtility.DisplayDialog("删除端口", "删除端口会同时删除关联连线，是否继续？", "删除", "取消"))
                    return;
                Undo.RecordObject(asset, "删除图端口");
                asset.RemovePort(port.portId);
                MarkChanged("端口已删除");
                rebuildGraph?.Invoke();
                locate?.Invoke(node.nodeId);
            }) { text = "删除" };
            ESGraphInspectorVisuals.StyleButton(apply, true);
            ESGraphInspectorVisuals.StyleButton(delete, false, true);
            actions.Add(apply);
            actions.Add(delete);
            foldout.Add(actions);
            parent.Add(foldout);
        }

        private void AddPortCreator(VisualElement parent, ESGraphNodeRecord node)
        {
            Foldout creator = new Foldout { text = "添加端口", value = false };
            ESGraphInspectorVisuals.StyleFoldout(creator, 2);
            TextField stableKey = new TextField("稳定名称（高级）") { value = "flow.port." + (node.ports?.Count ?? 0) };
            stableKey.tooltip = "端口的稳定身份；普通使用可保持自动生成的值。";
            TextField name = new TextField("名称") { value = "新端口" };
            TextField customValueType = new TextField("自定义数据标识（高级）") { value = string.Empty };
            customValueType.tooltip = "只有选择“自定义数据”时才需要填写稳定标识。";
            customValueType.SetEnabled(false);
            VisualElement valueKind = CreateValueKindPicker(ESGraphPortValueKind.Flow,
                out PickerValue<ESGraphPortValueKind> valueKindValue,
                selected => customValueType.SetEnabled(selected == ESGraphPortValueKind.Custom));
            VisualElement direction = CreateDirectionPicker(ESGraphPortDirection.Output,
                out PickerValue<ESGraphPortDirection> directionValue);
            VisualElement capacity = CreateCapacityPicker(ESGraphPortCapacity.Single,
                out PickerValue<ESGraphPortCapacity> capacityValue);
            ESGraphInspectorVisuals.StyleTextField(stableKey);
            ESGraphInspectorVisuals.StyleTextField(name);
            ESGraphInspectorVisuals.StyleTextField(customValueType);
            creator.Add(stableKey);
            creator.Add(name);
            creator.Add(valueKind);
            creator.Add(customValueType);
            creator.Add(direction);
            creator.Add(capacity);
            creator.Add(ESGraphInspectorVisuals.CreateButton("添加端口",
                "使用当前名称、数据类型、方向和容量创建端口。", () =>
            {
                ESGraphPortDefinition definition = new ESGraphPortDefinition(name.value, stableKey.value,
                    directionValue.Value, capacityValue.Value, valueKindValue.Value, customValueType.value);
                if (!asset.CanAddPort(node.nodeId, definition, out string error))
                {
                    report?.Invoke(error);
                    return;
                }
                Undo.RecordObject(asset, "添加图端口");
                ESGraphPortRecord created = asset.AddPort(node.nodeId, definition, out _);
                MarkChanged("端口已添加");
                rebuildGraph?.Invoke();
                locate?.Invoke(node.nodeId);
            }, true));
            parent.Add(creator);
        }

        private void ClearDetails()
        {
            ClearOdinPayloadSession();
            details.Clear();
        }

        private void ClearOdinPayloadSession()
        {
            if (odinPayloadSession == null)
                return;
            odinPayloadSession.Dispose();
            odinPayloadSession = null;
        }

        private bool TryCreateOdinPayloadInspector(ESGraphNodeRecord node, out VisualElement inspector)
        {
            inspector = null;
            Type payloadType = ResolveOdinPayloadType(node);
            if (payloadType == null)
                return false;

            if (!ESGraphOdinPayloadSession.TryCreate(payloadType, node.payloadJson,
                    value => CommitPayload(node.nodeId, value), out ESGraphOdinPayloadSession session))
            {
                return false;
            }

            odinPayloadSession = session;
            var root = new VisualElement { name = "es-graph-odin-payload" };
            root.style.marginTop = 3f;
            root.style.marginBottom = 4f;
            root.style.paddingTop = 5f;
            root.style.paddingBottom = 5f;
            root.style.paddingLeft = 7f;
            root.style.paddingRight = 7f;
            root.style.backgroundColor = ESEditorPresentation.GetDepthBackground(2);
            root.style.borderLeftWidth = 2f;
            root.style.borderLeftColor = ESEditorPresentation.GetDepthAccent(1);
            root.style.borderBottomWidth = 1f;
            root.style.borderBottomColor = ESEditorPresentation.DividerColor;

            var caption = new Label("业务字段");
            caption.style.fontSize = 10f;
            caption.style.unityFontStyleAndWeight = FontStyle.Bold;
            caption.style.color = ESEditorPresentation.SectionMutedTextColor;
            caption.style.marginBottom = 3f;
            root.Add(caption);

            var content = new IMGUIContainer(session.Draw);
            content.style.marginLeft = 1f;
            content.style.marginRight = 1f;
            content.style.paddingTop = 2f;
            content.style.paddingBottom = 3f;
            root.Add(content);
            inspector = root;
            return true;
        }

        private Type ResolveOdinPayloadType(ESGraphNodeRecord node)
        {
            if (node == null || asset == null
                || asset.DomainKind != ESGraphDomainKind.AgentAuthoring)
            {
                return null;
            }

            switch (node.BuiltInKind)
            {
                case ESGraphBuiltInNodeKind.AgentGoal: return typeof(ESAgentGoalPayload);
                case ESGraphBuiltInNodeKind.AgentReference: return typeof(ESAgentReferencePayload);
                case ESGraphBuiltInNodeKind.AgentConstraint: return typeof(ESAgentConstraintPayload);
                case ESGraphBuiltInNodeKind.AgentAICommandOutput: return typeof(ESAgentAICommandOutputPayload);
                case ESGraphBuiltInNodeKind.AgentSkillOutput: return typeof(ESAgentSkillOutputPayload);
                case ESGraphBuiltInNodeKind.AgentValidation: return typeof(ESAgentValidationPayload);
                default: return null;
            }
        }

        private void ShowEdgeInspector(string edgeId)
        {
            ClearDetails();
            ESGraphEdgeRecord edge = asset?.FindEdge(edgeId);
            if (edge == null)
            {
                SetHeader("单向关系", "关系不可用", "连线不存在或已删除",
                    ESEditorPresentation.GetStatusAccent(0, ESStatusKind.Warning));
                details.Add(ESGraphInspectorVisuals.CreateNotice("连线不存在或已删除。",
                    HelpBoxMessageType.Warning));
                return;
            }
            VisualElement relationship = CreateSection("单向关系", "关系始终从输出端流向输入端。画布中的箭头沿同一方向移动。", 1);
            if (asset.TryFindPort(edge.outputPortId, out ESGraphNodeRecord fromNode, out ESGraphPortRecord output)
                && asset.TryFindPort(edge.inputPortId, out ESGraphNodeRecord toNode, out ESGraphPortRecord input))
            {
                string fromName = fromNode.title ?? fromNode.typeId;
                string toName = toNode.title ?? toNode.typeId;
                SetHeader("单向关系", fromName + " → " + toName,
                    ESGraphChinesePresentation.GetPortValueTypeName(output.valueTypeId),
                    new Color(0.28f, 0.82f, 0.72f, 1f));
                AddKeyValue(relationship, "节点流向", (fromNode.title ?? fromNode.typeId) + "  →  " + (toNode.title ?? toNode.typeId));
                AddKeyValue(relationship, "数据语义", ESGraphChinesePresentation.GetPortValueTypeName(output.valueTypeId));
                AddKeyValue(relationship, "端口流向",
                    ESGraphChinesePresentation.GetPortName(output.name) + " → "
                    + ESGraphChinesePresentation.GetPortName(input.name));
            }
            else
            {
                SetHeader("单向关系", "端点信息不完整", "请通过质量检查定位问题",
                    ESEditorPresentation.GetStatusAccent(0, ESStatusKind.Warning));
                relationship.Add(ESGraphInspectorVisuals.CreateNotice(
                    "这条关系的输入或输出端口已无法解析，请通过底部质量检查定位。",
                    HelpBoxMessageType.Warning));
            }
            details.Add(relationship);

            Foldout advanced = CreateAdvancedFoldout("高级关系身份");
            AddReadOnlyText(advanced, "连线编号", edge.edgeId);
            AddReadOnlyText(advanced, "输出端口", edge.outputPortId);
            AddReadOnlyText(advanced, "输入端口", edge.inputPortId);
            details.Add(advanced);

            Button delete = ESGraphInspectorVisuals.CreateButton("删除这条关系",
                "删除当前关系，可通过 Undo 恢复。", () =>
            {
                Undo.RecordObject(asset, "删除图连线");
                asset.RemoveEdge(edge.edgeId);
                MarkChanged("连线已删除");
                rebuildGraph?.Invoke();
            }, false, true);
            details.Add(delete);
        }

        private void RefreshValidation()
        {
            CancelScheduledValidation();
            ShowIssues(asset != null ? ESGraphAuthoringRegistry.Validate(asset) : new List<ESGraphValidationIssue>());
        }

        private void ForceValidation()
        {
            validationRevision++;
            RefreshValidation();
        }

        private void RefreshValidationIfNeeded()
        {
            if (validatedRevision == validationRevision)
                return;
            RefreshValidation();
        }

        private void RequestValidation()
        {
            CancelScheduledValidation();
            if (validatedRevision == validationRevision)
                return;
            issueSummary.text = "等待质量检查";
            issueSummary.style.color = ESEditorPresentation.SectionTextColor;
            issueMeta.text = "图已修改 · 将自动检查";
            validationSchedule = schedule.Execute(RefreshValidationIfNeeded)
                .StartingIn(ValidationDelayMilliseconds);
        }

        private void CancelScheduledValidation()
        {
            validationSchedule?.Pause();
            validationSchedule = null;
        }

        private void BakeSnapshot()
        {
            if (asset == null)
                return;
            if (!ESGraphAuthoringRegistry.TryBake(asset, out ESBakedGraphSnapshot snapshot,
                    out IESBakedGraphPlan domainPlan, out List<ESGraphValidationIssue> issues))
            {
                ShowIssues(issues);
                report?.Invoke("烘焙失败：请处理校验错误");
                return;
            }
            ShowIssues(issues);
            string result = domainPlan == null ? "通用检查结果" : "领域检查结果";
            report?.Invoke(result + " 烘焙成功：" + snapshot.ContentSignature.Substring(0, 12));
        }

        private void SendAgentGenerationRequest()
        {
            if (!TryBakeAgentSpec("生成请求", out ESAgentArtifactGenerationSpec spec))
                return;
            SendAgentGenerationRequest(spec, "全部产物");
        }

        private void SendAgentGenerationRequest(ESAgentArtifactKind artifactKind)
        {
            if (!TryBakeAgentSpec("生成请求", out ESAgentArtifactGenerationSpec spec))
                return;
            if (!ESAgentArtifactGenerationWorkspace.TryCreateArtifactView(spec, artifactKind,
                    out ESAgentArtifactGenerationSpec artifactView, out string filterError))
            {
                report?.Invoke(filterError);
                return;
            }
            SendAgentGenerationRequest(artifactView,
                artifactKind == ESAgentArtifactKind.AICommand ? "AICommand" : "Agent Skill");
        }

        private void SendAgentGenerationRequest(ESAgentArtifactGenerationSpec spec, string displayName)
        {
            if (!ESAgentArtifactGenerationWorkspace.CreateAndSend(spec, out string requestDirectory,
                    out string dispatchMessage, out string error))
            {
                report?.Invoke(error);
                return;
            }
            report?.Invoke(displayName + "候选请求已创建；Cmd Agent：" + dispatchMessage
                + "；候选目录：" + requestDirectory);
        }

        internal bool CanExecuteNodeCardAction(string nodeId, ESGraphNodeCardActionKey action)
        {
            return TryCreateNodeCardActionContext(nodeId, out ESGraphNodeCardActionContext context)
                && ESGraphAuthoringRegistry.CanExecuteNodeCardAction(context, action, out _);
        }

        internal void ExecuteNodeCardAction(string nodeId, ESGraphNodeCardActionKey action)
        {
            if (!TryCreateNodeCardActionContext(nodeId, out ESGraphNodeCardActionContext context))
            {
                report?.Invoke("节点局部动作目标不存在或上下文已失效。");
                return;
            }
            if (!ESGraphAuthoringRegistry.TryExecuteNodeCardAction(context, action, out string error))
                report?.Invoke(error);
        }

        private bool TryCreateNodeCardActionContext(string nodeId, out ESGraphNodeCardActionContext context)
        {
            context = null;
            ESGraphNodeRecord node = asset?.FindNode(nodeId);
            if (node == null)
                return false;

            ESGraphAuthoringRegistry.TryGetNodeDefinition(asset.DomainKey, node.TypeKey,
                out IESGraphNodeDefinition definition);
            bool futureGraphSchema = asset.schemaVersion > ESGraphAsset.CurrentSchemaVersion;
            bool unsupportedGraphSchema = asset.schemaVersion != ESGraphAsset.CurrentSchemaVersion;
            bool futureNodeSchema = definition != null && node.version > definition.CurrentVersion;
            context = new ESGraphNodeCardActionContext(asset, node,
                unsupportedGraphSchema || futureNodeSchema,
                futureGraphSchema || futureNodeSchema,
                ShowIssues,
                report);
            return true;
        }

        private void SendSingleUseAgentArtifact(ESAgentArtifactKind artifactKind)
        {
            if (!TryBakeAgentSpec("即时执行", out ESAgentArtifactGenerationSpec spec))
                return;
            if (!ESAgentArtifactGenerationWorkspace.SendSingleUse(spec, artifactKind, out string requestId,
                    out string dispatchMessage, out string error))
            {
                report?.Invoke(error);
                return;
            }
            string displayName = artifactKind == ESAgentArtifactKind.AICommand
                ? "单次 Command" : "临时 Skill";
            report?.Invoke(displayName + "请求 " + requestId + " 已提交至 Cmd Agent；状态："
                + dispatchMessage + "。当前只确认发送或排队，不代表 AI 已确认接收或完成。");
        }

        private void OpenAgentCopyMenu(Button anchor)
        {
            if (anchor == null || !TryBakeAgentSpec("复制图信息", out ESAgentArtifactGenerationSpec spec))
                return;
            ESSearchDropdown.Open(
                anchor,
                hostWindow,
                "复制智能助手编排图",
                new[]
                {
                    ESSearchDropdown.Entry.Item(
                        "复制 AI 即时执行提示",
                        () => CopyAgentGraph(spec, ESAgentGraphCopyFormat.ImmediateExecutionPrompt, "AI 即时执行提示"),
                        subtitle: "可直接粘贴给 AI 执行当前最终目的；不生成永久产物。",
                        keywords: "复制 AI 执行 提示"),
                    ESSearchDropdown.Entry.Item(
                        "复制生成 / 更新请求 JSON",
                        () => CopyAgentGraph(spec, ESAgentGraphCopyFormat.ArtifactRequestJson, "生成 / 更新请求 JSON"),
                        subtitle: "包含 GraphId、内容签名、稳定 ArtifactId、输出和校验门禁。",
                        keywords: "复制 JSON 生成 更新"),
                    ESSearchDropdown.Entry.Item(
                        "复制完整图说明（Markdown + Mermaid）",
                        () => CopyAgentGraph(spec, ESAgentGraphCopyFormat.GraphMarkdown, "完整图说明"),
                        subtitle: "适合发送给外部 AI 或写入需求文档。",
                        keywords: "复制 Markdown Mermaid 图说明")
                },
                minimumWindowSize: new Vector2(560f, 320f));
        }

        private void CopyAgentGraph(ESAgentArtifactGenerationSpec spec, ESAgentGraphCopyFormat format,
            string displayName)
        {
            if (!ESAgentArtifactGenerationWorkspace.TryBuildCopyText(spec, format,
                    out string content, out string error)
                || string.IsNullOrWhiteSpace(content))
            {
                report?.Invoke("复制失败：" + (string.IsNullOrWhiteSpace(error) ? "没有可复制的图信息。" : error));
                return;
            }
            EditorGUIUtility.systemCopyBuffer = content;
            report?.Invoke("已复制" + displayName + "，共 " + content.Length + " 个字符。");
        }

        private bool TryBakeAgentSpec(string actionName, out ESAgentArtifactGenerationSpec spec)
        {
            spec = null;
            if (asset == null)
                return false;
            if (ESGraphAuthoringRegistry.TryBake(asset, out _, out IESBakedGraphPlan plan,
                    out List<ESGraphValidationIssue> issues)
                && plan is ESAgentArtifactGenerationSpec baked)
            {
                ShowIssues(issues);
                spec = baked;
                return true;
            }
            ShowIssues(issues);
            report?.Invoke(actionName + "失败：请先修复智能助手编排图的校验错误，并明确最终目的与成功标准。");
            return false;
        }

        private void LaunchApprovedAgentImplementation(Button launchButton)
        {
            if (!TryBakeAgentSpec("打开实现窗口", out ESAgentArtifactGenerationSpec spec))
                return;

            launchButton?.SetEnabled(false);
            bool started = ESAgentImplementationSessionLauncher.TryLaunchApprovedImplementation(spec, message =>
            {
                launchButton?.SetEnabled(true);
                report?.Invoke(message);
            }, out string error);
            if (started)
                return;
            launchButton?.SetEnabled(true);
            report?.Invoke(error);
        }

        private void CommitPayload(string nodeId, string payloadJson)
        {
            ESGraphNodeRecord node = asset?.FindNode(nodeId);
            if (node == null || string.Equals(node.payloadJson, payloadJson, StringComparison.Ordinal))
                return;
            if (editService != null)
            {
                ESGraphEditResult result = editService.SetNodeContent(
                    asset, node.nodeId, node.typeId, node.version, node.title, payloadJson);
                if (!result.changed)
                    return;
            }
            else
            {
                Undo.RecordObject(asset, "修改节点业务内容");
                asset.UpdateNode(node.nodeId, node.typeId, node.version, node.title, payloadJson, out _);
            }
            MarkChanged("业务内容已更新，正在自动检查整张图。");
            rebuildGraph?.Invoke();
        }

        private void MigrateNode(string nodeId)
        {
            if (asset == null || string.IsNullOrEmpty(nodeId))
                return;
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("升级图节点数据");
            Undo.RegisterCompleteObjectUndo(asset, "升级图节点数据");
            if (!ESGraphAuthoringRegistry.TryMigrateNode(asset, nodeId, out string error))
            {
                Undo.RevertAllDownToGroup(undoGroup);
                report?.Invoke("节点升级失败：" + error);
                return;
            }
            Undo.CollapseUndoOperations(undoGroup);
            EditorUtility.SetDirty(asset);
            requestAutoSave?.Invoke();
            rebuildGraph?.Invoke();
            ShowNodeInspector(nodeId);
            RefreshValidation();
            report?.Invoke("节点数据已升级到当前定义版本。");
        }

        private VisualElement CreateDirectionPicker(ESGraphPortDirection initial,
            out PickerValue<ESGraphPortDirection> selection)
        {
            selection = new PickerValue<ESGraphPortDirection>(initial);
            PickerValue<ESGraphPortDirection> captured = selection;
            VisualElement field = CreateSearchPickerField(
                "方向",
                ESGraphChinesePresentation.GetDirectionName(initial),
                "输入端口接收上游信息，输出端口把结果传给下游。",
                out Button button);
            button.clicked += () =>
            {
                ESSearchDropdown.Open(
                    button,
                    hostWindow,
                    "选择端口方向",
                    () => new[]
                    {
                        ESSearchDropdown.Entry.Item(
                            "输入",
                            () => SetDirection(ESGraphPortDirection.Input),
                            subtitle: "接收上游信息",
                            keywords: "输入 接收 上游 input",
                            selected: captured.Value == ESGraphPortDirection.Input),
                        ESSearchDropdown.Entry.Item(
                            "输出",
                            () => SetDirection(ESGraphPortDirection.Output),
                            subtitle: "发送给下游",
                            keywords: "输出 发送 下游 output",
                            selected: captured.Value == ESGraphPortDirection.Output)
                    },
                    minimumWindowSize: new Vector2(420f, 260f));
            };
            return field;

            void SetDirection(ESGraphPortDirection value)
            {
                captured.Value = value;
                button.text = ESGraphChinesePresentation.GetDirectionName(value) + "  ▼";
            }
        }

        private VisualElement CreateCapacityPicker(ESGraphPortCapacity initial,
            out PickerValue<ESGraphPortCapacity> selection)
        {
            selection = new PickerValue<ESGraphPortCapacity>(initial);
            PickerValue<ESGraphPortCapacity> captured = selection;
            VisualElement field = CreateSearchPickerField(
                "连接数量",
                ESGraphChinesePresentation.GetCapacityName(initial),
                "单连接只允许一条连线，多连接允许连接多个节点。",
                out Button button);
            button.clicked += () =>
            {
                ESSearchDropdown.Open(
                    button,
                    hostWindow,
                    "选择连接数量",
                    () => new[]
                    {
                        ESSearchDropdown.Entry.Item(
                            "单连接",
                            () => SetCapacity(ESGraphPortCapacity.Single),
                            subtitle: "最多连接一个节点",
                            keywords: "单连接 一个 single",
                            selected: captured.Value == ESGraphPortCapacity.Single),
                        ESSearchDropdown.Entry.Item(
                            "多连接",
                            () => SetCapacity(ESGraphPortCapacity.Multi),
                            subtitle: "可以连接多个节点",
                            keywords: "多连接 多个 multi",
                            selected: captured.Value == ESGraphPortCapacity.Multi)
                    },
                    minimumWindowSize: new Vector2(420f, 260f));
            };
            return field;

            void SetCapacity(ESGraphPortCapacity value)
            {
                captured.Value = value;
                button.text = ESGraphChinesePresentation.GetCapacityName(value) + "  ▼";
            }
        }

        private VisualElement CreateValueKindPicker(ESGraphPortValueKind initial,
            out PickerValue<ESGraphPortValueKind> selection, Action<ESGraphPortValueKind> changed)
        {
            selection = new PickerValue<ESGraphPortValueKind>(initial);
            PickerValue<ESGraphPortValueKind> captured = selection;
            VisualElement field = CreateSearchPickerField(
                "数据类型",
                ESGraphChinesePresentation.GetPortValueKindName(initial),
                "用于限制哪些端口可以连接。普通流程选择“流程”，业务数据请选择对应分类。",
                out Button button);
            button.clicked += () =>
            {
                ESSearchDropdown.Open(
                    button,
                    hostWindow,
                    "选择端口数据类型",
                    () => EditablePortValueKinds.Select(kind =>
                    {
                        ESGraphPortValueKind selected = kind;
                        string stableId = selected == ESGraphPortValueKind.Custom
                            ? "由扩展定义稳定标识"
                            : ESGraphPortValueCatalog.GetStableId(selected);
                        return ESSearchDropdown.Entry.Item(
                            ESGraphChinesePresentation.GetPortValueKindName(selected),
                            () => SetValueKind(selected),
                            subtitle: stableId,
                            keywords: ESGraphChinesePresentation.GetPortValueKindName(selected) + " " + stableId,
                            selected: captured.Value == selected);
                    }),
                    minimumWindowSize: new Vector2(460f, 320f));
            };
            return field;

            void SetValueKind(ESGraphPortValueKind value)
            {
                captured.Value = value;
                button.text = ESGraphChinesePresentation.GetPortValueKindName(value) + "  ▼";
                changed?.Invoke(value);
            }
        }

        private static VisualElement CreateSection(string title, string subtitle = null, int depth = 1,
            string badge = null)
        {
            return ESGraphInspectorVisuals.CreateCard(title, subtitle, depth, badge);
        }

        private static Foldout CreateAdvancedFoldout(string text)
        {
            var foldout = new Foldout { text = text, value = false };
            ESGraphInspectorVisuals.StyleFoldout(foldout, 2);
            return foldout;
        }

        private static VisualElement CreateWorkflowStep(string number, string title, string description,
            Button action)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginTop = 4f;
            row.style.marginBottom = 4f;
            row.style.paddingLeft = 6f;
            row.style.paddingRight = 4f;
            row.style.paddingTop = 6f;
            row.style.paddingBottom = 6f;
            row.style.backgroundColor = ESEditorPresentation.GetDepthBackground(3);
            row.style.borderBottomWidth = 1f;
            row.style.borderBottomColor = ESEditorPresentation.DividerColor;
            Label index = ESGraphInspectorVisuals.CreateBadge(number,
                ESEditorPresentation.GetDepthAccent(1));
            index.style.marginLeft = 0f;
            index.style.marginRight = 8f;
            row.Add(index);
            var text = new VisualElement();
            text.style.flexGrow = 1f;
            text.style.minWidth = 118f;
            var titleLabel = new Label(title ?? string.Empty);
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.color = ESEditorPresentation.SectionTextColor;
            text.Add(titleLabel);
            var descriptionLabel = new Label(description ?? string.Empty);
            descriptionLabel.style.whiteSpace = WhiteSpace.Normal;
            descriptionLabel.style.fontSize = 9f;
            descriptionLabel.style.color = ESEditorPresentation.SectionMutedTextColor;
            descriptionLabel.style.marginTop = 2f;
            text.Add(descriptionLabel);
            row.Add(text);
            if (action != null)
            {
                action.style.minWidth = 112f;
                action.style.maxWidth = 138f;
                action.style.flexGrow = 0f;
                row.Add(action);
            }
            return row;
        }

        private static void AddKeyValue(VisualElement parent, string key, string value)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginTop = 2f;
            row.style.marginBottom = 2f;
            var keyLabel = new Label(key ?? string.Empty);
            keyLabel.style.width = 82f;
            keyLabel.style.minWidth = 82f;
            keyLabel.style.color = ESEditorPresentation.SectionMutedTextColor;
            row.Add(keyLabel);
            var valueLabel = new Label(value ?? string.Empty);
            valueLabel.style.flexGrow = 1f;
            valueLabel.style.whiteSpace = WhiteSpace.Normal;
            valueLabel.style.color = ESEditorPresentation.SectionTextColor;
            row.Add(valueLabel);
            parent.Add(row);
        }

        private Dictionary<string, List<PortRelationTarget>> BuildPortRelationSummaries(ESGraphNodeRecord selectedNode)
        {
            var result = new Dictionary<string, List<PortRelationTarget>>(StringComparer.Ordinal);
            if (asset == null || selectedNode?.ports == null || selectedNode.ports.Count == 0)
                return result;

            var selectedPortIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < selectedNode.ports.Count; i++)
            {
                string portId = selectedNode.ports[i]?.portId;
                if (string.IsNullOrEmpty(portId))
                    continue;
                selectedPortIds.Add(portId);
                result[portId] = new List<PortRelationTarget>();
            }

            var nodesByPort = new Dictionary<string, ESGraphNodeRecord>(StringComparer.Ordinal);
            var portsById = new Dictionary<string, ESGraphPortRecord>(StringComparer.Ordinal);
            for (int n = 0; n < asset.Nodes.Count; n++)
            {
                ESGraphNodeRecord node = asset.Nodes[n];
                if (node?.ports == null)
                    continue;
                for (int p = 0; p < node.ports.Count; p++)
                {
                    ESGraphPortRecord port = node.ports[p];
                    if (port == null || string.IsNullOrEmpty(port.portId))
                        continue;
                    nodesByPort[port.portId] = node;
                    portsById[port.portId] = port;
                }
            }

            for (int i = 0; i < asset.Edges.Count; i++)
            {
                ESGraphEdgeRecord edge = asset.Edges[i];
                if (edge == null)
                    continue;
                string selectedPortId = null;
                string otherPortId = null;
                if (selectedPortIds.Contains(edge.inputPortId ?? string.Empty))
                {
                    selectedPortId = edge.inputPortId;
                    otherPortId = edge.outputPortId;
                }
                else if (selectedPortIds.Contains(edge.outputPortId ?? string.Empty))
                {
                    selectedPortId = edge.outputPortId;
                    otherPortId = edge.inputPortId;
                }
                if (string.IsNullOrEmpty(selectedPortId) || string.IsNullOrEmpty(otherPortId)
                    || !nodesByPort.TryGetValue(otherPortId, out ESGraphNodeRecord otherNode)
                    || !portsById.TryGetValue(otherPortId, out ESGraphPortRecord otherPort))
                    continue;
                result[selectedPortId].Add(new PortRelationTarget(
                    otherNode.nodeId,
                    string.IsNullOrWhiteSpace(otherNode.title) ? otherNode.typeId : otherNode.title,
                    ESGraphChinesePresentation.GetPortName(otherPort.name)));
            }
            return result;
        }

        private void AddPortRelationSummary(VisualElement parent, ESGraphPortRecord port,
            IReadOnlyList<PortRelationTarget> connectedNodes)
        {
            if (asset == null || port == null)
                return;
            bool input = port.direction == ESGraphPortDirection.Input;
            connectedNodes = connectedNodes ?? Array.Empty<PortRelationTarget>();
            var row = new VisualElement();
            row.style.marginTop = 3f;
            row.style.marginBottom = 3f;
            row.style.paddingLeft = 8f;
            row.style.paddingRight = 8f;
            row.style.paddingTop = 6f;
            row.style.paddingBottom = 6f;
            row.style.backgroundColor = ESEditorPresentation.GetDepthBackground(2);
            row.style.borderLeftWidth = 2f;
            row.style.borderLeftColor = input
                ? new Color(0.35f, 0.65f, 0.95f, 1f) : new Color(0.28f, 0.82f, 0.72f, 1f);
            var header = new Label(input
                ? "◀ 输入 · " + ESGraphChinesePresentation.GetPortName(port.name)
                : "输出 · " + ESGraphChinesePresentation.GetPortName(port.name) + " ▶");
            header.style.flexGrow = 1f;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.color = ESEditorPresentation.SectionTextColor;
            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.alignItems = Align.Center;
            headerRow.Add(header);
            headerRow.Add(ESGraphInspectorVisuals.CreateBadge(
                connectedNodes.Count == 0 ? "未连接" : connectedNodes.Count + " 条",
                input ? new Color(0.35f, 0.65f, 0.95f, 1f)
                    : new Color(0.28f, 0.82f, 0.72f, 1f)));
            row.Add(headerRow);
            string relationship = connectedNodes.Count == 0
                ? "尚未连接"
                : (input ? "来自：" : "前往：") + BuildRelationPreview(connectedNodes);
            var detail = new Label(ESGraphChinesePresentation.GetPortValueTypeName(port.valueTypeId)
                + " · " + ESGraphChinesePresentation.GetCapacityName(port.capacity) + " · " + relationship);
            detail.style.whiteSpace = WhiteSpace.Normal;
            detail.style.fontSize = 10f;
            detail.style.color = connectedNodes.Count == 0
                ? ESEditorPresentation.SectionMutedTextColor : ESEditorPresentation.SectionTextColor;
            row.Add(detail);
            if (connectedNodes.Count > 0)
            {
                Button locateButton = null;
                locateButton = ESGraphInspectorVisuals.CreateButton(
                    connectedNodes.Count == 1
                        ? (input ? "定位上游节点" : "定位下游节点")
                        : "查看并定位 " + connectedNodes.Count + " 个节点",
                    connectedNodes.Count == 1
                        ? "在画布中定位这条关系连接的节点。"
                        : "选择一个已连接节点并在画布中定位。",
                    () =>
                    {
                        if (connectedNodes.Count == 1)
                        {
                            locate?.Invoke(connectedNodes[0].NodeId);
                            return;
                        }
                        ESSearchDropdown.Open(
                            locateButton,
                            hostWindow,
                            input ? "选择上游节点" : "选择下游节点",
                            () => connectedNodes.Select(target =>
                            {
                                PortRelationTarget captured = target;
                                return ESSearchDropdown.Entry.Item(
                                    captured.NodeName,
                                    () => locate?.Invoke(captured.NodeId),
                                    subtitle: "端口：" + captured.PortName,
                                    keywords: captured.NodeName + " " + captured.PortName);
                            }),
                            minimumWindowSize: new Vector2(460f, 300f));
                    });
                locateButton.style.minHeight = 24f;
                locateButton.style.marginTop = 5f;
                row.Add(locateButton);
            }
            parent.Add(row);
        }

        private static string BuildRelationPreview(IReadOnlyList<PortRelationTarget> connectedNodes)
        {
            int visibleCount = Mathf.Min(3, connectedNodes.Count);
            string result = string.Empty;
            for (int i = 0; i < visibleCount; i++)
            {
                if (i > 0)
                    result += "、";
                result += connectedNodes[i].NodeName + " · " + connectedNodes[i].PortName;
            }
            if (connectedNodes.Count > visibleCount)
                result += " 等 " + connectedNodes.Count + " 项";
            return result;
        }

        private void MarkChanged(string message)
        {
            EditorUtility.SetDirty(asset);
            requestAutoSave?.Invoke();
            validationRevision++;
            RequestValidation();
            report?.Invoke(message);
        }

        private static void AddReadOnlyText(VisualElement parent, string label, string value)
        {
            TextField field = new TextField(label) { value = value ?? string.Empty, isReadOnly = true };
            ESGraphInspectorVisuals.StyleTextField(field);
            parent.Add(field);
        }

        private static bool ValidateNodeInput(string typeId, int version, out string error)
        {
            if (!string.IsNullOrWhiteSpace(typeId) && version > 0)
            {
                error = null;
                return true;
            }
            error = "TypeId 不能为空，节点版本必须大于 0。";
            return false;
        }

        private sealed class ESGraphOdinPayloadSession : IDisposable
        {
            private readonly ESGraphOdinPayloadHost host;
            private readonly OdinEditor editor;
            private readonly Action<string> commit;
            private string committedJson;
            private bool disposed;

            private ESGraphOdinPayloadSession(ESGraphOdinPayloadHost host, OdinEditor editor,
                string committedJson, Action<string> commit)
            {
                this.host = host;
                this.editor = editor;
                this.committedJson = committedJson ?? string.Empty;
                this.commit = commit;
            }

            public static bool TryCreate(Type payloadType, string payloadJson,
                Action<string> commit, out ESGraphOdinPayloadSession session)
            {
                session = null;
                if (payloadType == null || payloadType.IsAbstract || payloadType.IsInterface)
                    return false;
                try
                {
                    object payload = JsonUtility.FromJson(payloadJson ?? string.Empty, payloadType)
                        ?? Activator.CreateInstance(payloadType);
                    if (payload == null)
                        return false;

                    ESGraphOdinPayloadHost host = ScriptableObject.CreateInstance<ESGraphOdinPayloadHost>();
                    host.hideFlags = HideFlags.HideAndDontSave;
                    host.payload = payload;
                    OdinEditor editor = OdinEditor.CreateEditor(host, typeof(OdinEditor)) as OdinEditor;
                    if (editor == null)
                    {
                        UnityEngine.Object.DestroyImmediate(host);
                        return false;
                    }

                    session = new ESGraphOdinPayloadSession(host, editor, payloadJson, commit);
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            public void Draw()
            {
                if (disposed || host == null || editor == null)
                    return;

                EditorGUI.BeginChangeCheck();
                editor.DrawDefaultInspector();
                if (!EditorGUI.EndChangeCheck())
                    return;

                string nextJson = JsonUtility.ToJson(host.payload);
                if (string.Equals(committedJson, nextJson, StringComparison.Ordinal))
                    return;
                committedJson = nextJson;
                commit?.Invoke(nextJson);
            }

            public void Dispose()
            {
                if (disposed)
                    return;
                disposed = true;
                if (editor != null)
                    UnityEngine.Object.DestroyImmediate(editor);
                if (host != null)
                    UnityEngine.Object.DestroyImmediate(host);
            }
        }

        private sealed class ESGraphOdinPayloadHost : ScriptableObject
        {
            [TitleGroup("业务内容")]
            [HideLabel, HideReferenceObjectPicker, InlineProperty, SerializeReference]
            public object payload;
        }
    }
}
