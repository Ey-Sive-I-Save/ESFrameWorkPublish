using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace ES
{
    /// <summary>资源系统运行时观察与安全点验收入口。</summary>
    public sealed class ESResourceRuntimeMonitorWindow : ESSinglePageIMGUIWindow<ESResourceRuntimeMonitorWindow>
    {
        private Vector2 scrollPosition;
        private bool autoRefresh = true;
        private bool operationRunning;
        private int operationGeneration;
        private string lastOperationResult = "尚未执行验收操作。";
        private double nextRepaintTime;
        private ESResourcePlanInfo lifecycleValidationPlan;
        private ESResourcePlanInfo manualValidationPlan;

        [MenuItem("【ES】/验证与诊断/运行时监视/资源系统/打开资源运行时监视器", false, 2200)]
        public static void Open()
        {
            ESResourceRuntimeMonitorWindow window = GetWindow<ESResourceRuntimeMonitorWindow>();
            window.titleContent = new GUIContent("ES资源诊断");
            window.minSize = new Vector2(560f, 480f);
            window.Show();
        }

        public override GUIContent ESWindow_GetWindowGUIContent()
        {
            return new GUIContent("ES 资源诊断", "观察资源 Bootstrap、Provider、Scope 与 ResourcePlan 状态");
        }
        public override string ESWindow_PresentationShortTitle => "资源";

        protected override string ESWindow_Subtitle => "资源运行时观察与安全点验收";
        protected override Vector2 ESWindow_MinSize => new Vector2(560f, 480f);
        protected override Vector2 ESWindow_DefaultSize => new Vector2(920f, 720f);
        protected override string ESWindow_PageStableId => "resource.runtime-monitor";
        protected override string ESWindow_PageTitle => "资源运行时监视器";
        protected override string ESWindow_PageKeywords => "资源 Bootstrap Provider Scope ResourcePlan 下载 诊断";

        protected override void ESWindow_BuildPageActions(
            ICollection<ESMenuTreePageAction> actions)
        {
            actions.Add(new ESMenuTreePageAction(
                    "resource-monitor.refresh",
                    "立即刷新",
                    "立即重绘当前资源诊断快照。",
                    context =>
                    {
                        Repaint();
                        context.SetStatus("资源诊断视图已刷新");
                    })
                .WithUnityIcon("Refresh")
                .WithPriority(100));
            actions.Add(new ESMenuTreePageAction(
                    "resource-monitor.auto-refresh",
                    "自动刷新",
                    "每 0.25 秒刷新一次运行状态。",
                    context =>
                    {
                        autoRefresh = !autoRefresh;
                        nextRepaintTime = 0d;
                        context.RefreshPageActions();
                        context.SetStatus(autoRefresh ? "已启用自动刷新" : "已暂停自动刷新");
                    })
                .WithCheckedState(() => autoRefresh)
                .WithPriority(90));
            actions.Add(new ESMenuTreePageAction(
                    "resource-monitor.copy-report",
                    "复制报告",
                    "复制完整资源诊断报告到剪贴板。",
                    context =>
                    {
                        EditorGUIUtility.systemCopyBuffer = BuildDiagnosticReport();
                        context.Notify("资源诊断报告已复制");
                    })
                .WithUnityIcon("Clipboard")
                .WithPriority(80));
        }

        protected override void ESWindow_OnHostEnable()
        {
            operationGeneration++;
            maxSize = new Vector2(1400f, 1000f);
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
            nextRepaintTime = 0d;
        }

        protected override void ESWindow_OnHostDisable()
        {
            operationGeneration++;
            operationRunning = false;
            EditorApplication.update -= OnEditorUpdate;
            nextRepaintTime = 0d;
        }

        private void OnEditorUpdate()
        {
            if (!autoRefresh || EditorApplication.timeSinceStartup < nextRepaintTime)
                return;
            nextRepaintTime = EditorApplication.timeSinceStartup + 0.25d;
            Repaint();
        }

        protected override void ESWindow_DrawIMGUI(ESMenuTreePageContext context)
        {
            GUILayout.Label(
                EditorApplication.isPlaying ? "PLAY MODE" : "EDIT MODE",
                EditorStyles.miniBoldLabel);
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            DrawBootstrapSection();
            DrawAssetCoreSection();
            DrawResourcePlanSection();
            DrawValidationSection();
            EditorGUILayout.EndScrollView();
        }

        private static void DrawSectionTitle(string title)
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        }

        private void DrawBootstrapSection()
        {
            DrawSectionTitle("Bootstrap / 下载");
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                ESGlobalResSetting settings = ESGlobalResSetting.Instance;
                EditorGUILayout.LabelField("配置运行模式", settings != null ? settings.AssetRunMode.ToString() : "未配置");
                if (ESAssetRunModeSession.TryGetLockedModes(out ESAssetRunMode configuredMode, out ESAssetRunMode effectiveMode))
                {
                    EditorGUILayout.LabelField("会话有效模式", effectiveMode.ToString());
                    if (configuredMode != effectiveMode)
                        EditorGUILayout.HelpBox("配置模式与会话有效模式不一致；资源系统禁止静默切换，需立即检查初始化路径。", MessageType.Error);
                }
                else
                    EditorGUILayout.LabelField("会话有效模式", "尚未锁定");

                ESResManager manager = ESResManager.Instance;
                EditorGUILayout.LabelField("Manager", manager != null ? "存在" : "未创建");
                EditorGUILayout.LabelField("Bootstrap 状态", manager != null ? manager.State.ToString() : "不可用");

                ESRuntimeReleaseDownloader downloader = manager != null ? manager.DiagnosticReleaseDownloader : null;
                if (downloader != null)
                {
                    EditorGUILayout.LabelField("运行模式", downloader.DiagnosticRunMode.ToString());
                    EditorGUILayout.LabelField("平台", downloader.DiagnosticPlatform);
                    EditorGUILayout.LabelField("来源", downloader.DiagnosticUsesLocalReleaseSource ? "StreamingAssets / 本地发布" : "远端发布");
                    EditorGUILayout.LabelField("已验证版本", EmptyAsDash(downloader.DiagnosticVerifiedReleaseVersion));
                    EditorGUILayout.LabelField("已验证文件", downloader.DiagnosticVerifiedFileCount.ToString());
                    DrawPath("缓存目录", downloader.DiagnosticCacheRoot);

                    if (downloader.HasDiagnosticProgress)
                    {
                        ESRuntimeReleaseDownloadProgress progress = downloader.LastDiagnosticProgress;
                        float ratio = progress.TotalCount > 0
                            ? Mathf.Clamp01((float)progress.CompletedCount / progress.TotalCount)
                            : progress.Stage == ESRuntimeReleaseDownloadStage.Completed ? 1f : 0f;
                        Rect rect = EditorGUILayout.GetControlRect(false, 20f);
                        EditorGUI.ProgressBar(rect, ratio, progress.Stage + "  " + progress.CompletedCount + "/" + progress.TotalCount);
                        EditorGUILayout.LabelField("当前目标", EmptyAsDash(progress.Subject));
                    }
                    if (downloader.HasDiagnosticSnapshot)
                    {
                        ESRuntimeReleaseDownloadSnapshot snapshot = downloader.LastDiagnosticSnapshot;
                        Rect rect = EditorGUILayout.GetControlRect(false, 20f);
                        EditorGUI.ProgressBar(rect, snapshot.Progress01,
                            snapshot.State + "  " + FormatBytes(snapshot.CompletedBytes) + " / " + FormatBytes(snapshot.TotalBytes));
                        EditorGUILayout.LabelField("传输文件", snapshot.CompletedFileCount + " / " + snapshot.TotalFileCount);
                        EditorGUILayout.LabelField("当前传输", EmptyAsDash(snapshot.Subject));
                        EditorGUILayout.LabelField("速度 / ETA", FormatBytes((long)snapshot.SpeedBytesPerSecond) + "/s / " + snapshot.EstimatedRemainingSeconds + "s");
                        EditorGUILayout.LabelField("重试", snapshot.RetryAttempt.ToString());
                        if (snapshot.State == ESRuntimeReleaseTransferState.Failed || snapshot.State == ESRuntimeReleaseTransferState.Cancelled)
                            EditorGUILayout.HelpBox(
                                string.IsNullOrWhiteSpace(snapshot.TerminalMessage) ? "资源传输已结束，但没有提供终态原因。" : snapshot.TerminalMessage,
                                snapshot.State == ESRuntimeReleaseTransferState.Failed ? MessageType.Error : MessageType.Warning);
                    }
                }
                else
                {
                    DrawPath("PersistentData", Application.persistentDataPath);
                }

                if (manager != null && !string.IsNullOrWhiteSpace(manager.LastBootstrapError))
                {
                    EditorGUILayout.Space(4f);
                    EditorGUILayout.HelpBox("最近一次 Bootstrap 失败", MessageType.Error);
                    EditorGUILayout.SelectableLabel(manager.LastBootstrapError, EditorStyles.textArea, GUILayout.MinHeight(70f));
                }
            }
        }

        private static void DrawAssetCoreSection()
        {
            DrawSectionTitle("Provider / Scope");
            ESAssetRuntimeDiagnostics diagnostics = ESAssets.GetRuntimeDiagnostics();
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("资源系统 Ready", BoolText(diagnostics.IsReady));
                EditorGUILayout.LabelField("Provider 切换中", BoolText(diagnostics.IsProviderTransitioning));
                EditorGUILayout.LabelField("Provider", EmptyAsDash(diagnostics.ProviderType));
                EditorGUILayout.LabelField("Provider 在途请求", BoolText(diagnostics.ProviderHasPendingOperations));
                if (ESEditorResourceSessionBootstrap.IsEditorDirectCatalogDegraded)
                {
                    EditorGUILayout.HelpBox(
                        "EditorDirect 已进入降级模式：直接 ESAssetRefer 可用，但 ConfigKey / ConfigData 未完成注入。",
                        MessageType.Error);
                    EditorGUILayout.SelectableLabel(
                        ESEditorResourceSessionBootstrap.EditorDirectCatalogDiagnostic,
                        EditorStyles.textArea,
                        GUILayout.MinHeight(50f));
                }
                EditorGUILayout.LabelField("活跃 Scope", diagnostics.LiveScopeCount.ToString());
                EditorGUILayout.LabelField("有请求的 Scope", diagnostics.PendingScopeCount.ToString());
                EditorGUILayout.LabelField("Registry Scope", diagnostics.RegisteredScopeCount.ToString());
                EditorGUILayout.LabelField("隐式创建 Registry", diagnostics.ImplicitRegisteredScopeCount.ToString());
                EditorGUILayout.LabelField("正在关闭 Registry", diagnostics.ClosingRegisteredScopeCount.ToString());
                EditorGUILayout.LabelField("Scope 已持有资产", diagnostics.LoadedAssetCount.ToString());
                EditorGUILayout.LabelField("Scope 在途资产", diagnostics.PendingAssetCount.ToString());
                EditorGUILayout.LabelField("Resident Scope", diagnostics.HasResidentScope ? "已创建" : "未创建");
                if (diagnostics.HasUnloadFailure)
                {
                    EditorGUILayout.HelpBox("AssetBundle 卸载出现异常，Provider 已进入阻断状态；失败次数：" + diagnostics.UnloadFailureCount, MessageType.Error);
                    EditorGUILayout.SelectableLabel(diagnostics.LastUnloadError, EditorStyles.textArea, GUILayout.MinHeight(50f));
                }

                ESRuntimeDataAssetLoadingService loadingService = ESGameManager.RuntimeData != null
                    ? ESGameManager.RuntimeData.ExistingAssetLoadingService
                    : null;
                EditorGUILayout.LabelField("AssetLoadingService", loadingService == null
                    ? "不可用"
                    : loadingService.IsInitialized ? "已初始化" : "未初始化");
            }
        }

        private static void DrawResourcePlanSection()
        {
            DrawSectionTitle("ResourcePlan");
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                ESResourcePlanRuntimeService service = ESGameManager.ResourcePlans;
                if (service == null)
                {
                    EditorGUILayout.LabelField("ResourcePlan Service 尚未创建。");
                    return;
                }

                ESResourcePlanRuntimeDiagnostics diagnostics = service.GetRuntimeDiagnostics();
                EditorGUILayout.LabelField("活动计划", diagnostics.ActiveCount.ToString());
                EditorGUILayout.LabelField("释放中计划", diagnostics.ReleasingCount.ToString());
                if (diagnostics.Entries == null || diagnostics.Entries.Count == 0)
                {
                    EditorGUILayout.LabelField("当前没有活动 ResourcePlan。", EditorStyles.miniLabel);
                    return;
                }

                EditorGUILayout.Space(3f);
                for (int i = 0; i < diagnostics.Entries.Count; i++)
                {
                    ESResourcePlanDiagnosticEntry entry = diagnostics.Entries[i];
                    string state = entry.IsReleasing ? entry.State + "（释放中）" : entry.State.ToString();
                    EditorGUILayout.LabelField(entry.PlanName, state, EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(
                        "  Total=" + entry.TotalCount
                        + "  Success=" + entry.SuccessCount
                        + "  Failure=" + entry.FailureCount
                        + "  RequiredFailure=" + entry.RequiredFailureCount
                        + "  OptionalPending=" + entry.OptionalPendingCount
                        + "  Retain=" + entry.RetainCount
                        + "  ScopeOwners=" + entry.LifetimeScopeOwnerCount
                        + "  Unowned=" + entry.UnownedRetainCount
                        + "  InternalScopeDisposed=" + entry.InternalScopeDisposed,
                        EditorStyles.miniLabel);
                }
            }
        }

        private void DrawValidationSection()
        {
            DrawSectionTitle("运行时验收操作");
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (!EditorApplication.isPlaying)
                    EditorGUILayout.HelpBox("安全点与 Bootstrap 操作只允许在 Play Mode 执行。", MessageType.Info);

                using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying || operationRunning))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("增量安全点", GUILayout.Height(30f)))
                            ConfirmAndRunIncrementalSafePoint();
                        if (GUILayout.Button("全量安全点", GUILayout.Height(30f)))
                            ConfirmAndRunFullSafePoint();
                        if (GUILayout.Button("重跑 Bootstrap", GUILayout.Height(30f)))
                            ConfirmAndRestartBootstrap();
                    }

                    EditorGUILayout.Space(5f);
                    lifecycleValidationPlan = (ESResourcePlanInfo)EditorGUILayout.ObjectField(
                        new GUIContent("自动释放 Plan", "必须配置 releaseOnExit=true；用于 P1-P5、P8。"),
                        lifecycleValidationPlan,
                        typeof(ESResourcePlanInfo),
                        false);
                    manualValidationPlan = (ESResourcePlanInfo)EditorGUILayout.ObjectField(
                        new GUIContent("手动常驻 Plan", "必须配置 releaseOnExit=false；用于 P10。"),
                        manualValidationPlan,
                        typeof(ESResourcePlanInfo),
                        false);
                    if (GUILayout.Button("运行 Scope 生命周期验收 P1-P5 / P8 / P10", GUILayout.Height(32f)))
                        ConfirmAndRunScopeAcceptance();
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("打开缓存目录"))
                        RevealCacheDirectory();
                    if (GUILayout.Button("打开资源管理窗口"))
                        ESResWindow.TryOpenWindow();
                }

                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField(operationRunning ? "执行中……" : "最近结果", EditorStyles.boldLabel);
                EditorGUILayout.SelectableLabel(lastOperationResult, EditorStyles.textArea, GUILayout.MinHeight(55f));
            }
        }

        private void ConfirmAndRunScopeAcceptance()
        {
            if (lifecycleValidationPlan == null || manualValidationPlan == null)
            {
                EditorUtility.DisplayDialog("缺少验收 Plan", "请分别指定自动释放 Plan 和手动常驻 Plan。", "知道了");
                return;
            }
            if (!lifecycleValidationPlan.releaseOnExit || manualValidationPlan.releaseOnExit)
            {
                EditorUtility.DisplayDialog(
                    "Plan 退出策略不匹配",
                    "自动释放 Plan 必须为 releaseOnExit=true；手动常驻 Plan 必须为 releaseOnExit=false。",
                    "知道了");
                return;
            }
            if (!EditorUtility.DisplayDialog(
                    "运行 ResourcePlan Scope 验收",
                    "将真实 Apply/Release 所选 Plan，并创建临时 Binder GameObject。请确保当前不是业务切换关键帧。",
                    "运行",
                    "取消"))
                return;
            RunScopeAcceptanceAsync(operationGeneration).Forget();
        }

        private async UniTaskVoid RunScopeAcceptanceAsync(int generation)
        {
            operationRunning = true;
            var evidence = new StringBuilder(2048);
            try
            {
                if (!ESAssets.IsReady || ESGameManager.ResourcePlans == null)
                    throw new InvalidOperationException("资源 Provider 或 ResourcePlan Service 尚未就绪。");

                ESResourcePlanRuntimeService service = ESGameManager.ResourcePlans;
                await EnsurePlanFullyReleasedAsync(service, lifecycleValidationPlan);
                EnsureOperationActive(generation);
                await RunP1Async(service, lifecycleValidationPlan, evidence);
                EnsureOperationActive(generation);
                await RunP2Async(service, lifecycleValidationPlan, evidence);
                EnsureOperationActive(generation);
                await RunP3Async(service, lifecycleValidationPlan, evidence);
                EnsureOperationActive(generation);
                await RunP4Async(service, lifecycleValidationPlan, evidence);
                EnsureOperationActive(generation);
                await RunBinderCaseAsync(lifecycleValidationPlan, true, "P5", evidence);
                EnsureOperationActive(generation);
                await RunP8Async(service, lifecycleValidationPlan, evidence);
                EnsureOperationActive(generation);
                await EnsurePlanFullyReleasedAsync(service, manualValidationPlan);
                EnsureOperationActive(generation);
                await RunBinderCaseAsync(manualValidationPlan, false, "P10", evidence);
                EnsureOperationActive(generation);
                evidence.AppendLine("[NOT RUN] P6/P7：必须使用可观察慢加载计划，在加载中途取消。");
                evidence.AppendLine("[NOT RUN] P9：必须执行真实 Provider 重建，不在通用按钮中伪造。");
                lastOperationResult = evidence.ToString();
                Debug.Log("[ESRes][ScopeAcceptance]\n" + lastOperationResult);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                if (this != null && generation == operationGeneration)
                {
                    evidence.AppendLine("[FAIL] " + exception);
                    lastOperationResult = evidence.ToString();
                }
                Debug.LogException(exception);
            }
            finally
            {
                if (this != null && generation == operationGeneration)
                {
                    operationRunning = false;
                    Repaint();
                }
            }
        }

        private void EnsureOperationActive(int generation)
        {
            if (this == null || generation != operationGeneration)
                throw new OperationCanceledException("资源验收宿主已关闭或重建，停止写入旧窗口状态。");
        }

        private static async UniTask RunP1Async(ESResourcePlanRuntimeService service, ESResourcePlanInfo plan, StringBuilder evidence)
        {
            ESAssetScope scope = ESAssets.CreateScope();
            ESResourcePlanReport report = await service.ApplyAsync(plan, scope);
            AssertReport("P1 Apply", report, 1);
            scope.Dispose();
            await WaitForStateAsync(report, ESResourcePlanState.Released, ReleaseTimeout(plan));
            AssertReport("P1 Released", report, 0);
            AppendEvidence(evidence, "P1", report, "单 Scope Dispose 后释放");
        }

        private static async UniTask RunP2Async(ESResourcePlanRuntimeService service, ESResourcePlanInfo plan, StringBuilder evidence)
        {
            ESAssetScope scope = ESAssets.CreateScope();
            ESResourcePlanReport report = await service.ApplyAsync(plan, scope);
            report = await service.ReleaseAsync(plan, scope);
            scope.Dispose();
            AssertReport("P2", report, 0, ESResourcePlanState.Released);
            AppendEvidence(evidence, "P2", report, "显式 Release 后 Scope Dispose 无重复扣减");
        }

        private static async UniTask RunP3Async(ESResourcePlanRuntimeService service, ESResourcePlanInfo plan, StringBuilder evidence)
        {
            ESAssetScope scope = ESAssets.CreateScope();
            ESResourcePlanReport report = await service.ApplyAsync(plan, scope);
            report = await service.ApplyAsync(plan, scope);
            AssertReport("P3 Apply x2", report, 2);
            report = await service.ReleaseAsync(plan, scope);
            AssertReport("P3 Release #1", report, 1);
            report = await service.ReleaseAsync(plan, scope);
            scope.Dispose();
            AssertReport("P3 Release #2", report, 0, ESResourcePlanState.Released);
            AppendEvidence(evidence, "P3", report, "同 Scope 两次 retain 分别归还");
        }

        private static async UniTask RunP4Async(ESResourcePlanRuntimeService service, ESResourcePlanInfo plan, StringBuilder evidence)
        {
            ESAssetScope scopeA = ESAssets.CreateScope();
            ESAssetScope scopeB = ESAssets.CreateScope();
            ESResourcePlanReport report = await service.ApplyAsync(plan, scopeA);
            report = await service.ApplyAsync(plan, scopeB);
            AssertReport("P4 Apply A+B", report, 2);
            scopeA.Dispose();
            await UniTask.Yield();
            AssertReport("P4 Dispose A", report, 1);
            scopeB.Dispose();
            await WaitForStateAsync(report, ESResourcePlanState.Released, ReleaseTimeout(plan));
            AssertReport("P4 Dispose B", report, 0, ESResourcePlanState.Released);
            AppendEvidence(evidence, "P4", report, "多 Scope 互不误释放");
        }

        private static async UniTask RunP8Async(ESResourcePlanRuntimeService service, ESResourcePlanInfo plan, StringBuilder evidence)
        {
            ESAssetScope scope = ESAssets.CreateScope();
            ESResourcePlanReport report = await service.ApplyAsync(plan, scope);
            double started = EditorApplication.timeSinceStartup;
            scope.Dispose();
            if (report.RetainCount != 0 || report.State != ESResourcePlanState.ReleasePending)
                throw new InvalidOperationException("P8 最后 retain 结束后应立即进入 ReleasePending。当前=" + Describe(report));
            await WaitForStateAsync(report, ESResourcePlanState.Released, ReleaseTimeout(plan));
            ESRuntimeUnusedAssetBundleUnloadResult safePoint = await ESAssets.UnloadReleasedAssetBundlesAtSafePointAsync();
            AppendEvidence(evidence, "P8", report,
                "延迟=" + (EditorApplication.timeSinceStartup - started).ToString("F2")
                + "s，安全点卸载 Bundle=" + safePoint.UnloadedAssetBundleCount);
        }

        private static async UniTask RunBinderCaseAsync(
            ESResourcePlanInfo plan,
            bool expectReleaseOnDisable,
            string caseName,
            StringBuilder evidence)
        {
            // ES-EDITOR-VALIDATOR: intentional-no-undo
            // This Binder exists only for the acceptance probe and is destroyed
            // before the operation completes; deliberately keep it out of the
            // user's Undo history.
            GameObject target = new GameObject("ESResourcePlanBinder_Acceptance_" + caseName);
            target.SetActive(false);
            ESResourcePlanBinder binder = target.AddComponent<ESResourcePlanBinder>();
            using (var serialized = new SerializedObject(binder))
            {
                serialized.FindProperty("plan").objectReferenceValue = plan;
                serialized.FindProperty("applyOnEnable").boolValue = true;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            target.SetActive(true);
            await WaitUntilAsync(() => binder.LastReport != null
                && (binder.LastReport.State == ESResourcePlanState.Ready || binder.LastReport.State == ESResourcePlanState.Failed), 30f, caseName + " Binder Apply");
            ESResourcePlanReport report = binder.LastReport;
            target.SetActive(false);

            if (expectReleaseOnDisable)
            {
                await WaitForStateAsync(report, ESResourcePlanState.Released, ReleaseTimeout(plan));
                AssertReport(caseName, report, 0, ESResourcePlanState.Released);
            }
            else
            {
                await UniTask.Yield();
                AssertReport(caseName + " Disable", report, 1);
                report = await binder.ReleaseAsync();
                AssertReport(caseName + " Explicit Release", report, 0, ESResourcePlanState.Released);
            }

            UnityEngine.Object.Destroy(target);
            AppendEvidence(evidence, caseName, report,
                expectReleaseOnDisable ? "Binder 禁用自动归还" : "禁用保持，显式释放归还");
        }

        private static async UniTask EnsurePlanFullyReleasedAsync(ESResourcePlanRuntimeService service, ESResourcePlanInfo plan)
        {
            if (service.TryGetStatus(plan, out ESResourcePlanReport current) && current.RetainCount > 0)
                throw new InvalidOperationException(
                    "验收 Plan 当前正被业务持有，拒绝代替其他 Owner 释放：" + Describe(current));
            if (service.TryGetStatus(plan, out ESResourcePlanReport releasing))
                await WaitForStateAsync(releasing, ESResourcePlanState.Released, ReleaseTimeout(plan));
        }

        private static async UniTask WaitForStateAsync(ESResourcePlanReport report, ESResourcePlanState state, float timeoutSeconds)
            => await WaitUntilAsync(() => report != null && report.State == state, timeoutSeconds, "等待 " + state);

        private static async UniTask WaitUntilAsync(Func<bool> predicate, float timeoutSeconds, string operation)
        {
            double deadline = EditorApplication.timeSinceStartup + Mathf.Max(1f, timeoutSeconds);
            while (!predicate())
            {
                if (EditorApplication.timeSinceStartup >= deadline)
                    throw new TimeoutException(operation + " 超时。");
                await UniTask.Yield();
            }
        }

        private static float ReleaseTimeout(ESResourcePlanInfo plan)
            => Mathf.Max(10f, (plan != null ? plan.releaseDelaySeconds : 0f) + 10f);

        private static void AssertReport(string step, ESResourcePlanReport report, int retain, ESResourcePlanState? state = null)
        {
            if (report == null || report.RetainCount != retain || (state.HasValue && report.State != state.Value))
                throw new InvalidOperationException(step + " 失败：" + Describe(report));
        }

        private static string Describe(ESResourcePlanReport report)
            => report == null ? "<null>" : "State=" + report.State
                + ", Retain=" + report.RetainCount
                + ", RequiredFailure=" + report.RequiredFailureCount;

        private static void AppendEvidence(StringBuilder builder, string caseName, ESResourcePlanReport report, string note)
        {
            builder.Append("[PASS] ").Append(caseName)
                .Append(" | Plan=").Append(report?.Plan != null ? report.Plan.name : "<null>")
                .Append(" | State=").Append(report?.State)
                .Append(" | Retain=").Append(report?.RetainCount ?? 0)
                .Append(" | RequiredFailure=").Append(report?.RequiredFailureCount ?? 0)
                .Append(" | ").AppendLine(note);
        }

        private void ConfirmAndRunIncrementalSafePoint()
        {
            if (!EditorUtility.DisplayDialog("执行增量安全点", "会等待在途请求，并卸载零引用 AssetBundle。继续吗？", "执行", "取消"))
                return;
            RunIncrementalSafePointAsync(operationGeneration).Forget();
        }

        private async UniTaskVoid RunIncrementalSafePointAsync(int generation)
        {
            operationRunning = true;
            double started = EditorApplication.timeSinceStartup;
            try
            {
                ESRuntimeUnusedAssetBundleUnloadResult result = await ESAssets.UnloadReleasedAssetBundlesAtSafePointAsync();
                EnsureOperationActive(generation);
                lastOperationResult = "[PASS] 增量安全点完成，卸载 Bundle=" + result.UnloadedAssetBundleCount
                    + "，清理缓存资产=" + result.EvictedCachedAssetCount
                    + "，耗时=" + FormatSeconds(started);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                if (this != null && generation == operationGeneration)
                    lastOperationResult = "[FAIL] 增量安全点失败\n" + exception;
                Debug.LogException(exception);
            }
            finally
            {
                if (this != null && generation == operationGeneration)
                {
                    operationRunning = false;
                    Repaint();
                }
            }
        }

        private void ConfirmAndRunFullSafePoint()
        {
            if (!EditorUtility.DisplayDialog(
                    "执行全量安全点",
                    "会释放 ResourcePlan、全部 Scope、AssetTable Loader 和已加载 Bundle，然后重新绑定当前 Provider。继续吗？",
                    "执行",
                    "取消"))
                return;
            RunFullSafePointAsync(operationGeneration).Forget();
        }

        private async UniTaskVoid RunFullSafePointAsync(int generation)
        {
            operationRunning = true;
            double started = EditorApplication.timeSinceStartup;
            try
            {
                ESRuntimeDataAssetLoadingService service = ESGameManager.RuntimeData != null
                    ? ESGameManager.RuntimeData.ExistingAssetLoadingService
                    : null;
                if (service == null || !service.IsInitialized)
                    throw new InvalidOperationException("AssetLoadingService 尚未初始化。");
                await service.UnloadAllAssetsAtSafePointAsync();
                EnsureOperationActive(generation);
                lastOperationResult = "[PASS] 全量安全点完成并重新绑定 AssetTable Loader，耗时=" + FormatSeconds(started);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                if (this != null && generation == operationGeneration)
                    lastOperationResult = "[FAIL] 全量安全点失败\n" + exception;
                Debug.LogException(exception);
            }
            finally
            {
                if (this != null && generation == operationGeneration)
                {
                    operationRunning = false;
                    Repaint();
                }
            }
        }

        private void ConfirmAndRestartBootstrap()
        {
            ESResManager manager = ESResManager.Instance;
            if (manager == null)
            {
                lastOperationResult = "[FAIL] 场景中没有 ESResManager。";
                return;
            }
            if (!EditorUtility.DisplayDialog("重跑 Bootstrap", "会取消当前启动流程并重新读取发布清单。继续吗？", "重跑", "取消"))
                return;
            manager.RestartBootstrapFlow();
            lastOperationResult = "已请求重新执行 Bootstrap；请观察上方状态与下载进度。";
        }

        private static string FormatSeconds(double started)
        {
            return (EditorApplication.timeSinceStartup - started).ToString("0.000") + "s";
        }

        private static string BoolText(bool value) => value ? "是" : "否";
        private static string EmptyAsDash(string value) => string.IsNullOrWhiteSpace(value) ? "—" : value;

        private static void DrawPath(string label, string path)
        {
            EditorGUILayout.LabelField(label);
            EditorGUILayout.SelectableLabel(EmptyAsDash(path), EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
        }

        private static string ResolveCacheDirectory()
        {
            ESRuntimeReleaseDownloader downloader = ESResManager.Instance != null
                ? ESResManager.Instance.DiagnosticReleaseDownloader
                : null;
            if (downloader != null && !string.IsNullOrWhiteSpace(downloader.DiagnosticCacheRoot))
                return downloader.DiagnosticCacheRoot;

            ESGlobalResSetting settings = ESGlobalResSetting.Instance;
            return settings != null && !string.IsNullOrWhiteSpace(settings.Path_RuntimeDownloadCache)
                ? Path.Combine(settings.Path_RuntimeDownloadCache, "ReleaseV2")
                : Application.persistentDataPath;
        }

        private void RevealCacheDirectory()
        {
            string path = ResolveCacheDirectory();
            string existingPath = path;
            while (!string.IsNullOrWhiteSpace(existingPath) && !Directory.Exists(existingPath))
                existingPath = Path.GetDirectoryName(existingPath);
            if (string.IsNullOrWhiteSpace(existingPath))
            {
                lastOperationResult = "缓存目录及其父目录均不存在：" + path;
                return;
            }
            EditorUtility.RevealInFinder(existingPath);
            lastOperationResult = Directory.Exists(path)
                ? "已打开缓存目录：" + path
                : "目标缓存尚未生成，已打开最近存在的父目录：" + existingPath;
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1024L * 1024L) return (bytes / 1024f).ToString("0.0") + " KB";
            if (bytes < 1024L * 1024L * 1024L) return (bytes / (1024f * 1024f)).ToString("0.0") + " MB";
            return (bytes / (1024f * 1024f * 1024f)).ToString("0.00") + " GB";
        }

        private static string BuildDiagnosticReport()
        {
            var builder = new StringBuilder(1024);
            builder.AppendLine("[ESRes][Runtime Diagnostics]");
            builder.AppendLine("Time=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
            builder.AppendLine("PlayMode=" + EditorApplication.isPlaying);
            builder.AppendLine("PersistentDataPath=" + Application.persistentDataPath);
            ESGlobalResSetting settings = ESGlobalResSetting.Instance;
            builder.AppendLine("RunMode.Configured=" + (settings != null ? settings.AssetRunMode.ToString() : "Unavailable"));
            if (ESAssetRunModeSession.TryGetLockedModes(out ESAssetRunMode configuredMode, out ESAssetRunMode effectiveMode))
            {
                builder.AppendLine("RunMode.SessionConfigured=" + configuredMode);
                builder.AppendLine("RunMode.Effective=" + effectiveMode);
                builder.AppendLine("RunMode.Mismatch=" + (configuredMode != effectiveMode));
            }
            else
            {
                builder.AppendLine("RunMode.SessionConfigured=Unlocked");
                builder.AppendLine("RunMode.Effective=Unavailable");
                builder.AppendLine("RunMode.Mismatch=false");
            }

            ESResManager manager = ESResManager.Instance;
            builder.AppendLine("Bootstrap.Manager=" + (manager != null));
            builder.AppendLine("Bootstrap.State=" + (manager != null ? manager.State.ToString() : "Unavailable"));
            ESRuntimeReleaseDownloader downloader = manager != null ? manager.DiagnosticReleaseDownloader : null;
            if (downloader != null)
            {
                builder.AppendLine("Release.RunMode=" + downloader.DiagnosticRunMode);
                builder.AppendLine("Release.Platform=" + downloader.DiagnosticPlatform);
                builder.AppendLine("Release.CacheRoot=" + downloader.DiagnosticCacheRoot);
                builder.AppendLine("Release.Version=" + downloader.DiagnosticVerifiedReleaseVersion);
                builder.AppendLine("Release.VerifiedFiles=" + downloader.DiagnosticVerifiedFileCount);
                if (downloader.HasDiagnosticProgress)
                {
                    ESRuntimeReleaseDownloadProgress progress = downloader.LastDiagnosticProgress;
                    builder.AppendLine("Release.Stage=" + progress.Stage);
                    builder.AppendLine("Release.Subject=" + progress.Subject);
                    builder.AppendLine("Release.Progress=" + progress.CompletedCount + "/" + progress.TotalCount);
                }
                if (downloader.HasDiagnosticSnapshot)
                {
                    ESRuntimeReleaseDownloadSnapshot snapshot = downloader.LastDiagnosticSnapshot;
                    builder.AppendLine("Release.TransferState=" + snapshot.State);
                    builder.AppendLine("Release.TransferSubject=" + snapshot.Subject);
                    builder.AppendLine("Release.TransferBytes=" + snapshot.CompletedBytes + "/" + snapshot.TotalBytes);
                    builder.AppendLine("Release.TransferFiles=" + snapshot.CompletedFileCount + "/" + snapshot.TotalFileCount);
                    builder.AppendLine("Release.TransferSpeed=" + snapshot.SpeedBytesPerSecond);
                    builder.AppendLine("Release.TransferETA=" + snapshot.EstimatedRemainingSeconds);
                    builder.AppendLine("Release.TransferRetry=" + snapshot.RetryAttempt);
                    builder.AppendLine("Release.TransferTerminalMessage=" + (snapshot.TerminalMessage ?? string.Empty));
                }
            }
            if (manager != null && !string.IsNullOrWhiteSpace(manager.LastBootstrapError))
                builder.AppendLine("Bootstrap.LastError=" + manager.LastBootstrapError.Replace('\r', ' ').Replace('\n', ' '));

            ESAssetRuntimeDiagnostics assets = ESAssets.GetRuntimeDiagnostics();
            builder.AppendLine("Assets.Ready=" + assets.IsReady);
            builder.AppendLine("Assets.ProviderTransitioning=" + assets.IsProviderTransitioning);
            builder.AppendLine("Assets.Provider=" + assets.ProviderType);
            builder.AppendLine("Assets.ProviderPending=" + assets.ProviderHasPendingOperations);
            builder.AppendLine("EditorDirect.CatalogDegraded=" + ESEditorResourceSessionBootstrap.IsEditorDirectCatalogDegraded);
            builder.AppendLine("EditorDirect.CatalogDiagnostic=" + (ESEditorResourceSessionBootstrap.EditorDirectCatalogDiagnostic ?? string.Empty).Replace('\r', ' ').Replace('\n', ' '));
            builder.AppendLine("Assets.LiveScopes=" + assets.LiveScopeCount);
            builder.AppendLine("Assets.PendingScopes=" + assets.PendingScopeCount);
            builder.AppendLine("Assets.RegisteredScopes=" + assets.RegisteredScopeCount);
            builder.AppendLine("Assets.ImplicitRegisteredScopes=" + assets.ImplicitRegisteredScopeCount);
            builder.AppendLine("Assets.ClosingRegisteredScopes=" + assets.ClosingRegisteredScopeCount);
            builder.AppendLine("Assets.LoadedInScopes=" + assets.LoadedAssetCount);
            builder.AppendLine("Assets.PendingInScopes=" + assets.PendingAssetCount);
            builder.AppendLine("Assets.ResidentScope=" + assets.HasResidentScope);
            builder.AppendLine("Assets.HasUnloadFailure=" + assets.HasUnloadFailure);
            builder.AppendLine("Assets.UnloadFailureCount=" + assets.UnloadFailureCount);
            builder.AppendLine("Assets.LastUnloadError=" + (assets.LastUnloadError ?? string.Empty).Replace('\r', ' ').Replace('\n', ' '));

            ESResourcePlanRuntimeService planService = ESGameManager.ResourcePlans;
            if (planService == null)
            {
                builder.AppendLine("Plans.Service=false");
            }
            else
            {
                ESResourcePlanRuntimeDiagnostics plans = planService.GetRuntimeDiagnostics();
                builder.AppendLine("Plans.Active=" + plans.ActiveCount);
                builder.AppendLine("Plans.Releasing=" + plans.ReleasingCount);
                if (plans.Entries != null)
                {
                    for (int i = 0; i < plans.Entries.Count; i++)
                    {
                        ESResourcePlanDiagnosticEntry entry = plans.Entries[i];
                        builder.AppendLine("Plan[" + i + "]=" + entry.PlanName
                            + " | State=" + entry.State
                            + " | Total=" + entry.TotalCount
                            + " | Success=" + entry.SuccessCount
                            + " | Failure=" + entry.FailureCount
                            + " | RequiredFailure=" + entry.RequiredFailureCount
                            + " | OptionalPending=" + entry.OptionalPendingCount
                            + " | Retain=" + entry.RetainCount
                            + " | ScopeOwners=" + entry.LifetimeScopeOwnerCount
                            + " | Unowned=" + entry.UnownedRetainCount
                            + " | InternalScopeDisposed=" + entry.InternalScopeDisposed
                            + " | Releasing=" + entry.IsReleasing);
                    }
                }
            }
            return builder.ToString();
        }
    }
}
