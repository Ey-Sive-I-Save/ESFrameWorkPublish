using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ESFramework.ESAITest
{
    [DisallowMultipleComponent]
    public sealed class ESAITestRuntimeDashboard : MonoBehaviour
    {
        private const float DashboardRefreshSeconds = 0.5f;
        private readonly StringBuilder builder = new StringBuilder(8192);
        private readonly List<ESAITestEventDto> recentEvents = new List<ESAITestEventDto>(24);
        private ESAITestRunner runner;
        private Canvas canvas;
        private Text headerText;
        private Text detailText;
        private Text eventText;
        private Button cancelButton;
        private float nextRefreshTime;
        private long lastEvidenceScanNewestEventUtcTicks = long.MinValue;
        private long lastUseReceiptEventUtcTicks = long.MinValue;
        private long lastVerifyEvidenceEventUtcTicks = long.MinValue;
        private long lastPromptPublishEventUtcTicks = long.MinValue;
        private string lastUseReceiptSummary = "尚无";
        private string lastVerifyEvidenceSummary = "尚无";
        private string lastPromptPublishSummary = "尚无";
        private string renderedHeader = string.Empty;
        private string renderedDetail = string.Empty;
        private string renderedEvents = string.Empty;

        public void SetPresentationVisible(bool visible)
        {
            if (canvas != null)
                canvas.enabled = visible;
        }

        public void Bind(ESAITestRunner targetRunner)
        {
            if (runner != null)
                runner.StateChanged -= RefreshNow;

            runner = targetRunner;
            if (runner != null)
                runner.StateChanged += RefreshNow;

            EnsureView();
            RefreshNow();
        }

        private void OnDestroy()
        {
            if (runner != null)
                runner.StateChanged -= RefreshNow;
        }

        private void Update()
        {
            if (runner == null || !runner.IsRunning)
                return;
            if (Time.unscaledTime < nextRefreshTime)
                return;
            nextRefreshTime = Time.unscaledTime + DashboardRefreshSeconds;
            RefreshNow();
        }

        private void EnsureView()
        {
            if (canvas != null)
                return;

            GameObject canvasObject = new GameObject("ESAITest Runtime Dashboard", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32760;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;

            GameObject panel = CreateUIObject("Panel", canvasObject.transform, typeof(Image));
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0f, 0f);
            panelRect.anchorMax = new Vector2(0f, 1f);
            panelRect.pivot = new Vector2(0f, 0.5f);
            panelRect.sizeDelta = new Vector2(720f, 0f);
            panel.GetComponent<Image>().color = new Color(0.025f, 0.035f, 0.055f, 0.94f);

            Font font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            headerText = CreateText(panel.transform, "Header", font, 24, FontStyle.Bold, new Vector2(18f, -18f), new Vector2(-18f, -150f));
            detailText = CreateText(panel.transform, "Detail", font, 20, FontStyle.Normal, new Vector2(18f, -160f), new Vector2(-18f, -465f));
            eventText = CreateText(panel.transform, "Events", font, 17, FontStyle.Normal, new Vector2(18f, -475f), new Vector2(-18f, -86f));

            GameObject buttonObject = CreateUIObject("Cancel", panel.transform, typeof(Image), typeof(Button));
            RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0f, 0f);
            buttonRect.anchorMax = new Vector2(1f, 0f);
            buttonRect.offsetMin = new Vector2(18f, 18f);
            buttonRect.offsetMax = new Vector2(-18f, 72f);
            buttonObject.GetComponent<Image>().color = new Color(0.68f, 0.16f, 0.18f, 0.96f);
            cancelButton = buttonObject.GetComponent<Button>();
            cancelButton.onClick.AddListener(RequestCancel);

            Text buttonText = CreateText(buttonObject.transform, "Label", font, 22, FontStyle.Bold, Vector2.zero, Vector2.zero);
            RectTransform labelRect = buttonText.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            buttonText.alignment = TextAnchor.MiddleCenter;
            buttonText.text = "中断 ESAITest（安全取消）";
        }

        private void RequestCancel()
        {
            runner?.Cancel();
        }

        private void RefreshNow()
        {
            if (runner == null || headerText == null)
                return;

            ESAITestResultDto result = runner.Result;
            string status = runner.IsRunning ? (runner.CancellationRequested ? "取消中" : "运行中") : result?.statusCode ?? "准备中";
            runner.CopyRecentEvents(recentEvents, 18);
            RefreshEvidenceSummaries();
            SetTextIfChanged(headerText,
                "ESAITest 商业验收驾驶台\n"
                + "状态：" + status + "    Run：" + runner.RunId + "\n"
                + "计划：" + runner.PlanId + "    进度：" + runner.CompletedStepCount + "/" + runner.TotalStepCount,
                ref renderedHeader);

            builder.Clear();
            builder.Append("当前 Step：").Append(string.IsNullOrEmpty(runner.CurrentStepId) ? "-" : runner.CurrentStepId).Append('\n');
            builder.Append("当前操作：").Append(string.IsNullOrEmpty(runner.CurrentOperation) ? "-" : runner.CurrentOperation).Append('\n');
            builder.Append("信息：").Append(string.IsNullOrEmpty(runner.CurrentMessage) ? "-" : runner.CurrentMessage).Append('\n');
            builder.Append("耗时：").Append(runner.ElapsedSeconds.ToString("F2")).Append(" 秒\n");
            if (runner.IsAutonomyEnabled)
            {
                builder.Append("自主目标：").Append(runner.AutonomyGoal).Append('\n');
                builder.Append("自主回合：").Append(runner.AutonomyTurn)
                    .Append(" | ").Append(runner.AutonomyWaitingForDecision ? "等待 AI 决策" : "执行中").Append('\n');
                ESAITestAutonomyBridgeDiagnosticsDto bridge = runner.AutonomyBridgeDiagnostics;
                if (bridge != null)
                {
                    builder.Append("外部 AI 桥：").Append(bridge.state ?? "-")
                        .Append(" | 启动=").Append(bridge.autoLaunchRequested)
                        .Append(" | PID=").Append(bridge.externalProcessId)
                        .Append(" | 请求=").Append(bridge.requestsPublished)
                        .Append(" | 接收=").Append(bridge.decisionsAccepted)
                        .Append(" | 拒绝=").Append(bridge.decisionsRejected)
                        .Append(" | 心跳=").Append(bridge.lastHeartbeatUtcTicks == 0 ? "无" : "正常")
                        .Append('\n');
                    if (!string.IsNullOrEmpty(bridge.lastMessage))
                        builder.Append("外部 AI 桥信息：").Append(bridge.lastMessage).Append('\n');
                }
            }
            builder.Append("场景代际：").Append(ESAITestRuntime.SceneGeneration).Append('\n');
            builder.Append("Capability：");
            ESAITestCapabilityManifestDto manifest = ESAITestRuntime.Registry?.CreateManifest(ESAITestRuntime.RunId, ESAITestRuntime.SceneGeneration);
            if (manifest == null || manifest.capabilities == null || manifest.capabilities.Length == 0)
            {
                builder.Append("无");
            }
            else
            {
                for (int i = 0; i < manifest.capabilities.Length; i++)
                {
                    if (i > 0) builder.Append(", ");
                    builder.Append(manifest.capabilities[i].capabilityId);
                }
            }
            builder.Append('\n');
            builder.Append("最近 See：").Append(string.IsNullOrEmpty(ESAITestObservationRuntimeState.LastCommand)
                ? "尚无"
                : ESAITestObservationRuntimeState.LastCommand
                  + (string.IsNullOrEmpty(ESAITestObservationRuntimeState.LastAttentionProfile)
                      ? string.Empty
                      : " | Attention=" + ESAITestObservationRuntimeState.LastAttentionProfile)
                  + " | UI=" + ESAITestObservationRuntimeState.LastUiCount
                  + " | Scene=" + ESAITestObservationRuntimeState.LastSceneObjectCount
                  + " | Sampling=" + ESAITestObservationRuntimeState.LastSamplingCostMilliseconds.ToString("F3") + "ms").Append('\n');
            int pendingPromptCount = ESAITestAIPrompt.PendingCount;
            string highestPromptPriority = ESAITestAIPrompt.HighestPendingPriority;
            builder.Append("AI 提示：待消费=").Append(pendingPromptCount)
                .Append(" | 最高等级=").Append(string.IsNullOrEmpty(highestPromptPriority)
                    ? "-"
                    : highestPromptPriority).Append('\n');
            builder.Append("最近 Prompt Publish：").Append(lastPromptPublishSummary).Append('\n');
            builder.Append("最近截图：").Append(ESAITestObservationRuntimeState.LatestScreenshot == null
                ? "尚无"
                : ESAITestObservationRuntimeState.LatestScreenshot.relativePath).Append('\n');
            builder.Append("最近 ToUse：").Append(lastUseReceiptSummary).Append('\n');
            builder.Append("最近 ToVerify：").Append(lastVerifyEvidenceSummary).Append('\n');
            ESAITestConversationReceiptDto conversation = ESAITestConversationRuntimeState.LastReceipt;
            if (conversation == null)
            {
                builder.Append("对话 IPC：尚无请求\n");
            }
            else
            {
                builder.Append("对话 IPC：").Append(conversation.stage).Append(" | ")
                    .Append(conversation.statusCode).Append(" | requestId=")
                    .Append(conversation.requestId).Append(" | intent=")
                    .Append(conversation.intent).Append(" | verify=")
                    .Append(conversation.verificationState).Append('\n');
            }
            ESAITestRunDiagnosticsDto diagnostics = result?.diagnostics;
            if (diagnostics != null)
            {
                builder.Append("测试执行：").Append(diagnostics.executionStatusCode).Append(" | ")
                    .Append(diagnostics.executionMessage).Append('\n');
                builder.Append("报告落盘：").Append(diagnostics.reportStatusCode).Append(" | ")
                    .Append(diagnostics.reportMessage).Append('\n');
                builder.Append("快速定位：").Append(diagnostics.suggestedInvestigation).Append('\n');
                if (diagnostics.firstFailedStep != null)
                    builder.Append("首个失败：").Append(diagnostics.firstFailedStep.stepId).Append(" | ")
                        .Append(diagnostics.firstFailedStep.statusCode).Append(" | ")
                        .Append(diagnostics.firstFailedStep.message).Append('\n');
                if (diagnostics.conversation != null)
                    builder.Append("报告中的对话回执：").Append(diagnostics.conversation.stage).Append(" | ")
                        .Append(diagnostics.conversation.statusCode).Append(" | ")
                        .Append(diagnostics.conversation.verificationState).Append('\n');
            }
            builder.Append("中断入口：运行面板按钮、Editor Control Center 或 ESAITestPlayerBootstrap.RequestCancel()。\n");
            builder.Append("GUI 输入：").Append(EventSystem.current == null ? "缺少 EventSystem，面板按钮不可点击" : "EventSystem 可用");
            SetTextIfChanged(detailText, builder.ToString(), ref renderedDetail);

            builder.Clear();
            builder.Append("最近事件\n");
            for (int i = recentEvents.Count - 1; i >= 0; i--)
            {
                ESAITestEventDto item = recentEvents[i];
                builder.Append('[').Append(item.elapsedSeconds.ToString("F2")).Append("s] ")
                    .Append(item.eventType).Append(" | ")
                    .Append(string.IsNullOrEmpty(item.stepId) ? "run" : item.stepId).Append(" | ")
                    .Append(item.statusCode).Append(" | ")
                    .Append(item.message).Append('\n');
            }
            SetTextIfChanged(eventText, builder.ToString(), ref renderedEvents);

            if (cancelButton != null)
                cancelButton.interactable = runner.IsRunning && !runner.CancellationRequested && EventSystem.current != null;
        }

        private static void SetTextIfChanged(Text text, string value, ref string rendered)
        {
            if (string.Equals(rendered, value, StringComparison.Ordinal))
                return;

            rendered = value;
            text.text = value;
        }

        private void RefreshEvidenceSummaries()
        {
            long newestEventUtcTicks = recentEvents.Count == 0
                ? 0L
                : recentEvents[recentEvents.Count - 1]?.utcTicks ?? 0L;
            if (newestEventUtcTicks == lastEvidenceScanNewestEventUtcTicks)
                return;

            lastEvidenceScanNewestEventUtcTicks = newestEventUtcTicks;
            for (int i = recentEvents.Count - 1; i >= 0; i--)
            {
                ESAITestEventDto item = recentEvents[i];
                if (!TryGetUseReceipt(item?.value, out ESAITestUseResultDto receipt))
                    continue;

                if (item.utcTicks != lastUseReceiptEventUtcTicks)
                {
                    lastUseReceiptEventUtcTicks = item.utcTicks;
                    builder.Clear();
                    builder.Append(receipt.stepId).Append(" | ")
                        .Append(receipt.capabilityId).Append('/').Append(receipt.command)
                        .Append(" | ").Append(receipt.statusCode)
                        .Append(" | route=").Append(receipt.executionRoute);
                    if (!string.IsNullOrEmpty(receipt.leaseOwner))
                        builder.Append(" | owner=").Append(receipt.leaseOwner)
                            .Append(" | gen=").Append(receipt.leaseGeneration)
                            .Append(" | held=").Append(receipt.leaseHeld);
                    else
                        builder.Append(" | handler=").Append(receipt.handlerMatched);
                    lastUseReceiptSummary = builder.ToString();
                }
                break;
            }

            for (int i = recentEvents.Count - 1; i >= 0; i--)
            {
                ESAITestEventDto item = recentEvents[i];
                if (!TryGetVerifyEvidence(item?.value, out ESAITestVerifyResultDto evidence))
                    continue;

                if (item.utcTicks != lastVerifyEvidenceEventUtcTicks)
                {
                    lastVerifyEvidenceEventUtcTicks = item.utcTicks;
                    builder.Clear();
                    builder.Append(evidence.stepId).Append(" | ")
                        .Append(evidence.capabilityId).Append('/').Append(evidence.command)
                        .Append(" | ").Append(evidence.statusCode)
                        .Append(" | expected=").Append(evidence.expectedValue)
                        .Append(" | actual=").Append(evidence.actualValue);
                    lastVerifyEvidenceSummary = builder.ToString();
                }
                break;
            }

            for (int i = recentEvents.Count - 1; i >= 0; i--)
            {
                ESAITestEventDto item = recentEvents[i];
                if (!TryGetPromptPublishResult(item?.value, out ESAITestAIPromptPublishResultDto publish))
                    continue;

                if (item.utcTicks != lastPromptPublishEventUtcTicks)
                {
                    lastPromptPublishEventUtcTicks = item.utcTicks;
                    builder.Clear();
                    builder.Append(publish.stepId).Append(" | ")
                        .Append(publish.priority).Append(" | promptId=").Append(publish.promptId)
                        .Append(" | seq=").Append(publish.sequence)
                        .Append(" | 待消费=").Append(publish.pendingCount);
                    if (!string.IsNullOrEmpty(publish.evictedPromptId))
                        builder.Append(" | 淘汰=").Append(publish.evictedPromptId);
                    lastPromptPublishSummary = builder.ToString();
                }
                break;
            }
        }

        private static bool TryGetUseReceipt(ESAITestValueDto value, out ESAITestUseResultDto receipt)
        {
            receipt = null;
            if (value == null || !string.Equals(value.kind, "string", StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(value.stringValue))
                return false;

            try
            {
                receipt = JsonUtility.FromJson<ESAITestUseResultDto>(value.stringValue);
                return receipt != null && string.Equals(receipt.schema, ESAITestUseResultDto.Schema, StringComparison.Ordinal);
            }
            catch (Exception)
            {
                receipt = null;
                return false;
            }
        }

        private static bool TryGetVerifyEvidence(ESAITestValueDto value, out ESAITestVerifyResultDto evidence)
        {
            evidence = null;
            if (value == null || !string.Equals(value.kind, "string", StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(value.stringValue))
                return false;

            try
            {
                evidence = JsonUtility.FromJson<ESAITestVerifyResultDto>(value.stringValue);
                return evidence != null && string.Equals(evidence.schema, ESAITestVerifyResultDto.Schema, StringComparison.Ordinal);
            }
            catch (Exception)
            {
                evidence = null;
                return false;
            }
        }

        private static bool TryGetPromptPublishResult(ESAITestValueDto value, out ESAITestAIPromptPublishResultDto result)
        {
            result = null;
            if (value == null || !string.Equals(value.kind, "string", StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(value.stringValue))
                return false;

            try
            {
                result = JsonUtility.FromJson<ESAITestAIPromptPublishResultDto>(value.stringValue);
                return result != null && string.Equals(result.schema, ESAITestAIPromptPublishResultDto.Schema, StringComparison.Ordinal);
            }
            catch (Exception)
            {
                result = null;
                return false;
            }
        }

        private static GameObject CreateUIObject(string name, Transform parent, params Type[] components)
        {
            GameObject value = new GameObject(name, typeof(RectTransform));
            for (int i = 0; i < components.Length; i++)
                if (components[i] != typeof(RectTransform))
                    value.AddComponent(components[i]);
            value.transform.SetParent(parent, false);
            return value;
        }

        private static Text CreateText(Transform parent, string name, Font font, int fontSize, FontStyle style, Vector2 topLeft, Vector2 bottomRight)
        {
            GameObject value = CreateUIObject(name, parent, typeof(Text));
            Text text = value.GetComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = Color.white;
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            RectTransform rect = text.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(topLeft.x, bottomRight.y);
            rect.offsetMax = new Vector2(bottomRight.x, topLeft.y);
            return text;
        }
    }
}
