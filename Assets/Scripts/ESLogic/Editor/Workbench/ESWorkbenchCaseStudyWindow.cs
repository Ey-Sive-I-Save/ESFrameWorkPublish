#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using ES.EditorInternal;

namespace ES
{
    /// <summary>
    /// ES 工作台表现案例测试窗口。它不写入项目资产，只用于验证统一底座在状态、空状态、失败恢复和窄窗口下的交互表现。
    /// </summary>
    public sealed class ESWorkbenchCaseStudyWindow : ESSinglePageIMGUIWindow<ESWorkbenchCaseStudyWindow>
    {
        private enum CasePage { Overview, Workflow, Recovery, Density }
        private CasePage page;
        private Vector2 scroll;
        private ESStatusKind simulatedStatus = ESStatusKind.Ready;
        private int selectedCaseIndex;
        private double feedbackStartedAt = -1d;
        private readonly string[] cases = { "地图收集流程", "地形预览", "Prefab 批量散布", "NavMesh 烘焙" };

        [MenuItem("【ES】/验证与诊断/工作台/表现案例测试", false, 260)]
        public static void Open()
        {
            ESWorkbenchIntegrationTestWindow.Open();
        }

        public override GUIContent ESWindow_GetWindowGUIContent()
            => new GUIContent("ES 编辑器表现案例工作台", "验证 ES 工作台的商业级信息层级、状态表达、失败恢复和窄窗口布局。");

        protected override string ESWindow_Subtitle => "ES 编辑器体验验收 · 不写入项目资产";
        protected override Vector2 ESWindow_MinSize => new Vector2(820f, 560f);
        protected override Vector2 ESWindow_DefaultSize => new Vector2(1120f, 720f);
        protected override string ESWindow_PageStableId => "editor.workbench-case-study";
        protected override string ESWindow_PageTitle => "编辑器表现案例工作台";
        protected override string ESWindow_PageKeywords => "工作台 表现 案例 状态 失败恢复 窄窗口 高DPI 交互验收";

        protected override void ESWindow_BuildPageActions(ICollection<ESMenuTreePageAction> actions)
        {
            actions.Add(new ESMenuTreePageAction("case-study.ready", "模拟成功", "展示成功状态与下一步动作。", _ => SetStatus(ESStatusKind.Ready)).WithPriority(100));
            actions.Add(new ESMenuTreePageAction("case-study.warning", "模拟警告", "展示可继续但需要关注的状态。", _ => SetStatus(ESStatusKind.Warning)).WithPriority(90));
            actions.Add(new ESMenuTreePageAction("case-study.error", "模拟失败", "展示失败原因、影响和恢复入口。", _ => SetStatus(ESStatusKind.Error)).WithPriority(80));
        }

        protected override void ESWindow_DrawIMGUI(ESMenuTreePageContext context)
        {
            DrawHeader();
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawNavigation();
                using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
                {
                    scroll = EditorGUILayout.BeginScrollView(scroll);
                    DrawPage();
                    EditorGUILayout.EndScrollView();
                }
            }
        }

        private void DrawHeader()
        {
            Rect rect = GUILayoutUtility.GetRect(0f, 68f, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(rect, ESEditorPresentation.GetDepthBackground(0));
                EditorGUI.DrawRect(new Rect(rect.x, rect.y, 4f, rect.height), ESEditorPresentation.GetDepthAccent(0));
                ESEditorPresentation.DrawFrame(rect, ESEditorPresentation.GetStatusFrameColor(0, simulatedStatus));
                ESEditorPresentation.DrawFeedbackSweep(rect, ESEditorPresentation.GetStatusAccent(0, simulatedStatus), feedbackStartedAt, 0.7f, 0.16f);
            }
            GUI.Label(new Rect(rect.x + 14f, rect.y + 9f, rect.width - 220f, 24f), "编辑器表现案例工作台", ESEditorPresentation.HeaderStyle);
            GUI.Label(new Rect(rect.x + 14f, rect.y + 36f, rect.width - 220f, 18f), "验证信息层级、状态语义、主路径和恢复动作", ESEditorPresentation.MetaStyle);
            GUI.Label(new Rect(rect.xMax - 190f, rect.y + 25f, 175f, 22f), GetStatusTitle(), ESEditorPresentation.CompactCollectionMetaStyle);
            if (ESEditorPresentation.MotionEnabled && ESEditorPresentation.EvaluatePulse(feedbackStartedAt, 0.7f) > 0f) Repaint();
        }

        private void DrawNavigation()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(position.width < 980f ? 145f : 180f)))
            {
                GUILayout.Label("案例导航", ESEditorPresentation.HeaderStyle);
                DrawNavButton(CasePage.Overview, "总览", "先看主结论与状态");
                DrawNavButton(CasePage.Workflow, "流程", "预检、提交、运行、完成");
                DrawNavButton(CasePage.Recovery, "失败恢复", "原因、影响、恢复动作");
                DrawNavButton(CasePage.Density, "密度测试", "窄窗口与长中文");
                GUILayout.Space(12f);
                GUILayout.Label("模拟案例", ESEditorPresentation.MetaStyle);
                selectedCaseIndex = EditorGUILayout.Popup(selectedCaseIndex, cases);
                if (GUILayout.Button("重置案例状态", GUILayout.Height(24f))) SetStatus(ESStatusKind.Ready);
            }
        }

        private void DrawNavButton(CasePage target, string title, string tooltip)
        {
            Color previous = GUI.backgroundColor;
            GUI.backgroundColor = page == target ? ESEditorPresentation.SelectionColor : ESEditorPresentation.ToolbarSurfaceColor;
            if (GUILayout.Button(new GUIContent(title, tooltip), GUILayout.Height(30f))) page = target;
            GUI.backgroundColor = previous;
        }

        private void DrawPage()
        {
            switch (page)
            {
                case CasePage.Overview: DrawOverview(); break;
                case CasePage.Workflow: DrawWorkflow(); break;
                case CasePage.Recovery: DrawRecovery(); break;
                case CasePage.Density: DrawDensity(); break;
            }
        }

        private void DrawOverview()
        {
            GUILayout.Label("当前案例总览", ESEditorPresentation.HeaderStyle);
            GUILayout.Label("案例：" + cases[Mathf.Clamp(selectedCaseIndex, 0, cases.Length - 1)], ESEditorPresentation.SubtitleStyle);
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawMetric("当前状态", GetStatusTitle(), simulatedStatus);
                DrawMetric("下一步", GetNextAction(), ESStatusKind.Modified);
                DrawMetric("窗口宽度", Mathf.RoundToInt(position.width) + " px", position.width < 900f ? ESStatusKind.Warning : ESStatusKind.Ready);
            }
            DrawStatusPanel();
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawAction("执行主动作", () => SetStatus(ESStatusKind.Ready), simulatedStatus != ESStatusKind.Error);
                DrawAction("打开详情", () => page = CasePage.Workflow, true);
                DrawAction("查看恢复", () => page = CasePage.Recovery, simulatedStatus == ESStatusKind.Error);
            }
        }

        private void DrawWorkflow()
        {
            GUILayout.Label("精细流程状态", ESEditorPresentation.HeaderStyle);
            GUILayout.Label("每个阶段都明确区分预检、提交、运行和完成，不把按钮点击当成最终成功。", ESEditorPresentation.SubtitleStyle);
            DrawStep("01", "输入检查", "地图身份、资源 Key、UGC 配额", ESStatusKind.Ready);
            DrawStep("02", "预检", "生成 requestId 与 revision 快照", simulatedStatus == ESStatusKind.Error ? ESStatusKind.Warning : ESStatusKind.Ready);
            DrawStep("03", "提交", "使用同一 requestId 执行事务提交", simulatedStatus == ESStatusKind.Error ? ESStatusKind.Empty : ESStatusKind.Ready);
            DrawStep("04", "运行", "等待长任务并支持刷新/恢复", simulatedStatus == ESStatusKind.Warning ? ESStatusKind.Warning : ESStatusKind.Ready);
            DrawStep("05", "交付", "显示产物、定位入口和后续动作", simulatedStatus == ESStatusKind.Error ? ESStatusKind.Error : ESStatusKind.Ready);
        }

        private void DrawRecovery()
        {
            GUILayout.Label("失败恢复案例", ESEditorPresentation.HeaderStyle);
            if (simulatedStatus != ESStatusKind.Error)
            {
                DrawEmptyState("当前没有失败", "点击上方“模拟失败”，检查原因、影响和恢复入口。", "模拟失败", () => SetStatus(ESStatusKind.Error));
                return;
            }
            DrawStatusPanel();
            using (new EditorGUILayout.VerticalScope(ESEditorPresentation.SurfaceStyle))
            {
                GUILayout.Label("恢复动作", ESEditorPresentation.HeaderStyle);
                GUILayout.Label("重新预检会生成新的 requestId；不会复用已失效的提交身份。", ESEditorPresentation.SubtitleStyle);
                DrawAction("重新预检", () => SetStatus(ESStatusKind.Warning), true);
                DrawAction("定位输入", () => page = CasePage.Workflow, true);
                DrawAction("复制错误摘要", () => EditorGUIUtility.systemCopyBuffer = "ES_CASE_STUDY: 输入快照已失效，请重新执行预检。", true);
            }
        }

        private void DrawDensity()
        {
            GUILayout.Label("窄窗口与高 DPI 密度测试", ESEditorPresentation.HeaderStyle);
            GUILayout.Label("窗口缩窄时优先保留状态、主动作和恢复入口；技术详情进入滚动区。", ESEditorPresentation.SubtitleStyle);
            using (new EditorGUILayout.VerticalScope(ESEditorPresentation.SurfaceStyle))
            {
                EditorGUILayout.LabelField("长中文路径", "ES/ResourcePipeline/Baked/世界地图/运行时构建数据/地图分块清单.json");
                EditorGUILayout.LabelField("请求身份", "preflight-8e2c9c7b4c3f4f93a01f");
                EditorGUILayout.LabelField("说明", "所有长文本都应支持复制、定位或展开查看，不要求用户手动搜索 Console。");
                DrawAction("复制路径", () => EditorGUIUtility.systemCopyBuffer = "ES/ResourcePipeline/Baked/世界地图/运行时构建数据/地图分块清单.json", true);
            }
        }

        private void DrawStatusPanel()
        {
            using (new EditorGUILayout.VerticalScope(ESEditorPresentation.SurfaceStyle))
            {
                GUILayout.Label("状态与下一步", ESEditorPresentation.HeaderStyle);
                ESStatusKind status = simulatedStatus;
                Rect rect = GUILayoutUtility.GetRect(0f, 32f, GUILayout.ExpandWidth(true));
                if (Event.current.type == EventType.Repaint)
                {
                    EditorGUI.DrawRect(rect, ESEditorPresentation.GetDepthBackground(1));
                    EditorGUI.DrawRect(new Rect(rect.x, rect.y, 3f, rect.height), ESEditorPresentation.GetStatusAccent(0, status));
                    ESEditorPresentation.DrawFrame(rect, ESEditorPresentation.GetStatusFrameColor(0, status));
                }
                GUI.Label(new Rect(rect.x + 10f, rect.y + 7f, rect.width - 20f, 18f), GetStatusDescription(), ESEditorPresentation.MetaStyle);
            }
        }

        private void DrawStep(string number, string title, string detail, ESStatusKind status)
        {
            using (new EditorGUILayout.HorizontalScope(ESEditorPresentation.SurfaceStyle))
            {
                GUILayout.Label(number, ESEditorPresentation.HeaderStyle, GUILayout.Width(34f));
                using (new EditorGUILayout.VerticalScope())
                {
                    GUILayout.Label(title, ESEditorPresentation.HeaderStyle);
                    GUILayout.Label(detail, ESEditorPresentation.MetaStyle);
                }
                GUILayout.FlexibleSpace();
                GUILayout.Label(status == ESStatusKind.Ready ? "已通过" : status == ESStatusKind.Warning ? "待关注" : status == ESStatusKind.Error ? "失败" : "未开始", ESEditorPresentation.CompactCollectionMetaStyle);
            }
            GUILayout.Space(4f);
        }

        private void DrawMetric(string label, string value, ESStatusKind status)
        {
            using (new EditorGUILayout.VerticalScope(ESEditorPresentation.SurfaceStyle, GUILayout.MinWidth(120f), GUILayout.Height(58f)))
            {
                GUILayout.Label(label, ESEditorPresentation.MetaStyle);
                Color previous = GUI.color;
                GUI.color = ESEditorPresentation.GetStatusAccent(0, status);
                GUILayout.Label(value, ESEditorPresentation.HeaderStyle);
                GUI.color = previous;
            }
        }

        private void DrawAction(string label, Action action, bool enabled)
        {
            using (new EditorGUI.DisabledScope(!enabled))
            {
                if (GUILayout.Button(label, GUILayout.MinWidth(110f), GUILayout.Height(28f))) action?.Invoke();
            }
        }

        private void DrawEmptyState(string title, string description, string actionLabel, Action action)
        {
            using (new EditorGUILayout.VerticalScope(ESEditorPresentation.SurfaceStyle))
            {
                GUILayout.Label(title, ESEditorPresentation.HeaderStyle);
                GUILayout.Label(description, ESEditorPresentation.SubtitleStyle);
                DrawAction(actionLabel, action, true);
            }
        }

        private void SetStatus(ESStatusKind status)
        {
            simulatedStatus = status;
            feedbackStartedAt = EditorApplication.timeSinceStartup;
            Repaint();
        }

        private string GetStatusTitle()
            => simulatedStatus == ESStatusKind.Ready ? "已就绪" : simulatedStatus == ESStatusKind.Warning ? "需要关注" : simulatedStatus == ESStatusKind.Error ? "无法继续" : "未开始";

        private string GetStatusDescription()
            => simulatedStatus == ESStatusKind.Ready ? "当前输入已通过检查，可以执行下一步。" : simulatedStatus == ESStatusKind.Warning ? "存在可解释的风险，用户仍可继续或返回修正。" : simulatedStatus == ESStatusKind.Error ? "输入快照失效，当前提交已阻断。" : "等待用户选择一个明确动作。";

        private string GetNextAction()
            => simulatedStatus == ESStatusKind.Ready ? "执行主动作" : simulatedStatus == ESStatusKind.Warning ? "查看风险" : simulatedStatus == ESStatusKind.Error ? "重新预检" : "选择案例";
    }
}
#endif
