using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ES.EditorInternal
{
    /// <summary>
    /// ES 唯一的 URP 渲染质量入口。
    /// UI 只暴露 ES 语义质量档；Unity Quality/Graphics/Renderer 菜单不作为用户入口。
    /// </summary>
    public sealed class ESUrpRenderControlWindow : EditorWindow
    {
        private static readonly ESRenderQualityProfileId[] Profiles =
        {
            ESRenderQualityProfileId.Performant,
            ESRenderQualityProfileId.Balanced,
            ESRenderQualityProfileId.HighFidelity,
            ESRenderQualityProfileId.CombatReadability,
            ESRenderQualityProfileId.CinematicShowcase,
            ESRenderQualityProfileId.MobileStable
        };

        private int selectedProfile;
        private int selectedTemplateStyle;
        private int selectedSceneIntent;
        private int selectedPlatform;
        private int selectedContentType;
        private ESRenderResolvedConfiguration resolvedTemplate;
        private bool hasResolvedTemplate;
        private ESRenderBackendSnapshot snapshot;
        private ESRenderBackendChangePlan plan;
        private ESRenderBackendReceipt lastReceipt;
        private ESUnityProfilerMetricSource metricSource;
        private ESRenderMetricSnapshot lastMetrics;
        private bool hasMetrics;
        private ESRenderBackendEvidenceReceipt latestEvidence;
        private ESRenderEvidenceBatch latestBatch;
        private ESRenderEvidenceBatch baselineBatch;
        private ESRenderEvidenceBatchDecision batchDecision;
        private ESRenderEvidenceScenarioSummary[] scenarioSummaries = new ESRenderEvidenceScenarioSummary[0];
        private ESRenderQualitySamplingQueue samplingQueue;
        private string latestEvidenceJson = string.Empty;
        private ESRenderEvidenceReport latestReport;
        private string latestReportJson = string.Empty;
        private ESRenderEvidenceAggregateReport aggregateReport;
        private string aggregateReportJson = string.Empty;
        private string reportPath = "ES/Output/RenderingEvidence/urp-render-report.json";
        private string metricPlatform = "Editor-Desktop";
        private string metricScenario = "urp-quality-baseline";
        private int metricSampleCount = 60;
        private string drawCallsMarker = "Draw Calls";
        private string setPassCallsMarker = "SetPass Calls";
        private string cpuTimeMarker = "Main Thread";
        private string gpuTimeMarker = "GPU Frame Time";
        private string gcAllocMarker = "GC.Alloc";
        private string residentMemoryMarker = "System Used Memory";
        private string status = "尚未捕获 ES URP 后端快照。";
        private string sceneManifestStatus = "未检查场景模板 Manifest。";
        private bool hasSnapshot;
        private bool hasPlan;

        [MenuItem(MenuItemPathDefine.VALIDATION_DIAGNOSTICS_PATH + "渲染质量/打开 ES URP 渲染控制台", false, 120)]
        public static void Open()
        {
            GetWindow<ESUrpRenderControlWindow>("ES URP 渲染控制台");
        }

        private void OnEnable()
        {
            minSize = new Vector2(520f, 360f);
            Capture();
        }

        private void OnDisable()
        {
            if (metricSource != null)
            {
                metricSource.Dispose();
                metricSource = null;
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("ES URP 渲染控制台", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "ES 统一管理 URP 质量意图、后端快照和可回滚切换。此窗口不提供 Built-in/HDRP 入口。",
                MessageType.Info);

            string[] labels = BuildProfileLabels();
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("ES 内容模板", EditorStyles.boldLabel);
            selectedContentType = EditorGUILayout.Popup("内容类型", selectedContentType, BuildContentTypeLabels());
            selectedTemplateStyle = EditorGUILayout.Popup("视觉风格", selectedTemplateStyle, BuildTemplateLabels());
            selectedSceneIntent = EditorGUILayout.Popup("场景意图", selectedSceneIntent, BuildSceneIntentLabels());
            selectedPlatform = EditorGUILayout.Popup("目标平台", selectedPlatform, BuildPlatformLabels());
            if (GUILayout.Button("套用内容类型默认风格与场景"))
            {
                ESRenderContentTypeId contentType = (ESRenderContentTypeId)selectedContentType;
                ESRenderSceneTemplateDescriptor sceneDescriptor;
                ESRenderTemplatePlan scenePlan;
                if (ESRenderSceneTemplatePlanFactory.TryCreate(
                    contentType,
                    (ESRenderPlatformId)selectedPlatform,
                    out sceneDescriptor,
                    out scenePlan,
                    out status))
                {
                    selectedTemplateStyle = (int)sceneDescriptor.Style;
                    selectedSceneIntent = (int)sceneDescriptor.Intent;
                    resolvedTemplate = scenePlan.Configuration;
                    hasResolvedTemplate = true;
                }
                else
                {
                    hasResolvedTemplate = false;
                }
            }
            ESRenderVisualStyleId selectedStyle = ESRenderStyleCatalog.GetStyleIdAt(selectedTemplateStyle);
            EditorGUILayout.LabelField("模板资源清单", "ESRenderTemplateManifest.json");
            EditorGUILayout.LabelField("场景模板清单", "ESRenderSceneTemplateManifest.json");
            if (GUILayout.Button("检查 ES 场景模板清单"))
                sceneManifestStatus = ValidateSceneTemplateManifest();
            EditorGUILayout.LabelField("清单状态", sceneManifestStatus, EditorStyles.wordWrappedLabel);
            if (GUILayout.Button("解析当前风格 × 场景 × 平台模板"))
            {
                hasResolvedTemplate = ESRenderTemplateCatalog.TryResolve(
                    selectedStyle,
                    (ESRenderSceneIntentId)selectedSceneIntent,
                    (ESRenderPlatformId)selectedPlatform,
                    out resolvedTemplate,
                    out status);
            }
            if (hasResolvedTemplate)
            {
                EditorGUILayout.LabelField("已解析模板", resolvedTemplate.Style.Style + " / " + resolvedTemplate.QualityProfile);
                EditorGUILayout.LabelField("内容类型", resolvedTemplate.ContentType.ToString());
                EditorGUILayout.LabelField("预算倍率", "透明 ×" + resolvedTemplate.TransparencyBudgetScale.ToString("0.00") + " / 粒子 ×" + resolvedTemplate.ParticleBudgetScale.ToString("0.00"));
                ESRenderTemplateResourceBinding resolvedBinding;
                string bindingReason;
                if (ESRenderTemplateResourceMap.TryGet(selectedStyle, resolvedTemplate.QualityProfile, out resolvedBinding, out bindingReason))
                {
                    EditorGUILayout.LabelField("URP Renderer", resolvedBinding.RendererAssetPath);
                    EditorGUILayout.LabelField("ES Material", resolvedBinding.MaterialAssetPath);
                    EditorGUILayout.LabelField("ES Volume", resolvedBinding.VolumeAssetPath);
                    EditorGUILayout.LabelField("ES Shader", resolvedBinding.ShaderAssetPath);
                }
                else
                {
                    EditorGUILayout.LabelField("资源绑定", "未解析：" + bindingReason);
                }
                EditorGUILayout.LabelField("平台降级", resolvedTemplate.QualityDowngraded ? "已降级" : "未降级");
                EditorGUILayout.LabelField("Feature 预算", resolvedTemplate.FeatureBudget.ToString());
                EditorGUILayout.LabelField("体积效果", resolvedTemplate.VolumetricsEnabled ? "允许" : "关闭");
                EditorGUILayout.LabelField("材质模型", resolvedTemplate.MaterialRecipe.Surface.ToString());
                EditorGUILayout.LabelField("阴影", resolvedTemplate.LightingRecipe.ShadowMode + " / 级联 " + resolvedTemplate.LightingRecipe.CascadeCount);
                EditorGUILayout.LabelField("最终预算决策", "透明 " + resolvedTemplate.EffectsRecipe.TransparentBudget + "，粒子 " + resolvedTemplate.EffectsRecipe.ParticleBudget + "，Decal " + resolvedTemplate.EffectsRecipe.DecalBudget + "，Variant " + resolvedTemplate.EffectsRecipe.ShaderVariantBudget);
            }
            EditorGUILayout.Space(4f);
            selectedProfile = EditorGUILayout.Popup("ES 质量档", selectedProfile, labels);
            ESRenderQualityPolicy target = ESRenderQualityPolicy.Resolve(Profiles[selectedProfile]);
            EditorGUILayout.LabelField("目标策略", target.Profile.ToString());
            EditorGUILayout.LabelField("目标帧预算", target.TargetFrameMilliseconds.ToString("0.0") + " ms");
            EditorGUILayout.LabelField("动态分辨率", target.DynamicResolutionAllowed ? "允许" : "关闭");
            if (GUILayout.Button("创建六档 ES 采样队列"))
            {
                ESRenderQualitySamplingQueue.TryCreate(Profiles, out samplingQueue, out status);
            }
            if (samplingQueue != null)
            {
                EditorGUILayout.LabelField("采样队列", samplingQueue.Status + "（" + samplingQueue.CompletedCount + "/" + samplingQueue.Count + "）");
                using (new EditorGUI.DisabledScope(!samplingQueue.HasNext || samplingQueue.Status == ESRenderQualitySamplingQueueStatus.InProgress))
                {
                    if (GUILayout.Button("开始下一个质量档"))
                    {
                        if (samplingQueue.TryBeginNext(out ESRenderQualityProfileId queuedProfile, out status))
                        {
                            for (int i = 0; i < Profiles.Length; i++)
                                if (Profiles[i] == queuedProfile) selectedProfile = i;
                            status = "已选择队列质量档：" + queuedProfile + "。请生成计划并采样。";
                        }
                    }
                }
            }

            EditorGUILayout.Space(8f);
            if (GUILayout.Button("重新捕获后端快照"))
                Capture();

            if (hasSnapshot)
            {
                EditorGUILayout.LabelField("当前质量", snapshot.QualityName + " (" + snapshot.QualityIndex + ")");
                EditorGUILayout.LabelField("当前管线", snapshot.PipelineName);
                EditorGUILayout.LabelField("SRP Batcher", snapshot.SrpBatcherEnabled ? "启用" : "关闭");
            }

            if (GUILayout.Button(hasResolvedTemplate ? "按当前 ES 模板生成 Dry-Run 计划" : "生成 ES Dry-Run 计划"))
            {
                BuildPlan(hasResolvedTemplate ? resolvedTemplate.QualityPolicy : target);
                if (hasResolvedTemplate)
                    status = "已生成模板计划：" + resolvedTemplate.Style.Style + " / " + resolvedTemplate.Configuration.SceneIntent + " / " + resolvedTemplate.Configuration.Platform;
            }

            if (hasPlan)
            {
                EditorGUILayout.LabelField("计划状态", plan.Status.ToString());
                EditorGUILayout.LabelField("计划说明", plan.Reason);
                using (new EditorGUI.DisabledScope(!plan.IsDryRun))
                {
                    if (GUILayout.Button("按当前 ES 计划应用 URP 质量档"))
                        Apply();
                }
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("ES 运行时证据采样", EditorStyles.boldLabel);
            metricPlatform = EditorGUILayout.TextField("平台身份", metricPlatform);
            metricScenario = EditorGUILayout.TextField("场景身份", metricScenario);
            metricSampleCount = EditorGUILayout.IntField("采样帧数", metricSampleCount);
            using (new EditorGUI.DisabledScope(metricSource != null))
            {
                drawCallsMarker = EditorGUILayout.TextField("Draw 标记", drawCallsMarker);
                setPassCallsMarker = EditorGUILayout.TextField("SetPass 标记", setPassCallsMarker);
                cpuTimeMarker = EditorGUILayout.TextField("CPU 标记", cpuTimeMarker);
                gpuTimeMarker = EditorGUILayout.TextField("GPU 标记", gpuTimeMarker);
                gcAllocMarker = EditorGUILayout.TextField("GC 标记", gcAllocMarker);
                residentMemoryMarker = EditorGUILayout.TextField("显存/内存标记", residentMemoryMarker);
            }
            using (new EditorGUI.DisabledScope(metricSource != null || metricSampleCount <= 0))
            {
                if (GUILayout.Button("开始 ES 采样"))
                    StartMetricCapture();
            }
            using (new EditorGUI.DisabledScope(metricSource == null))
            {
                if (GUILayout.Button("采集当前帧"))
                    CaptureMetricFrame();
                if (GUILayout.Button("完成 ES 采样"))
                    CompleteMetricCapture();
            }
            if (metricSource != null)
                EditorGUILayout.LabelField("采样进度", metricSource.IsCompleted ? "已完成" : "进行中（" + metricSource.CapturedSampleCount + "/" + metricSampleCount + "）");
            if (hasMetrics)
            {
                EditorGUILayout.LabelField("最近证据", lastMetrics.Platform + " / " + lastMetrics.Scenario + " / " + lastMetrics.SampleCount + " 帧");
                using (new EditorGUI.DisabledScope(latestEvidence != null))
                {
                    if (GUILayout.Button("绑定 URP 资源并生成 EvidenceBatch"))
                        BuildEvidenceBatch();
                }
                if (latestEvidence != null)
                {
                    EditorGUILayout.LabelField("EvidenceBatch", latestBatch.batchId + "（JSON " + latestEvidenceJson.Length + " 字符）");
                    if (GUILayout.Button("将当前批次设为 ES 回归基线"))
                    {
                        baselineBatch = latestBatch;
                        status = "ES 回归基线已更新（仅内存）。";
                    }
                    if (baselineBatch != null)
                    {
                        ESRenderQualityPolicy auditPolicy = ESRenderQualityPolicy.Resolve(Profiles[selectedProfile]);
                        batchDecision = ESRenderEvidenceBatchDecision.Evaluate(baselineBatch, latestBatch, auditPolicy);
                        scenarioSummaries = ESRenderEvidenceScenarioSummary.Build(latestBatch, auditPolicy);
                        EditorGUILayout.LabelField("回归决策", batchDecision.Status.ToString());
                        EditorGUILayout.LabelField("预算审计", "已测 " + batchDecision.BudgetAudit.EvaluatedCount + " / 未测 " + batchDecision.BudgetAudit.UnmeasuredCount + " / 超预算 " + batchDecision.BudgetAudit.OverrunCount);
                        for (int i = 0; i < scenarioSummaries.Length; i++)
                        {
                            ESRenderEvidenceScenarioSummary summary = scenarioSummaries[i];
                            EditorGUILayout.LabelField(
                                summary.QualityProfile + " / " + summary.Platform + " / " + summary.Scenario,
                                "样本 " + summary.ReceiptCount + "，已测 " + summary.MeasuredCount + "，未测 " + summary.UnmeasuredCount + "，超预算 " + summary.OverrunCount);
                        }
                    }
                    if (GUILayout.Button("生成 ES 渲染回归报告"))
                        BuildEvidenceReport();
                    if (latestReport != null)
                    {
                        EditorGUILayout.LabelField("回归报告", latestReport.decisionStatus + "（JSON " + latestReportJson.Length + " 字符）");
                        if (GUILayout.Button("生成 ES 多报告组合总览"))
                            BuildAggregateReport();
                        reportPath = EditorGUILayout.TextField("报告路径", reportPath);
                        if (GUILayout.Button("验证 ES 报告导出路径"))
                            ValidateReportPath();
                    }
                    if (aggregateReport != null)
                        EditorGUILayout.LabelField("组合总览", aggregateReport.overallStatus + "（报告 " + aggregateReport.reportCount + "，JSON " + aggregateReportJson.Length + " 字符）");
                }
            }

            if (!string.IsNullOrEmpty(lastReceipt.Reason))
                EditorGUILayout.HelpBox("最近操作：" + lastReceipt.Status + " / " + lastReceipt.Reason, MessageType.None);
            EditorGUILayout.LabelField("状态", status, EditorStyles.wordWrappedLabel);
        }

        private string ValidateSceneTemplateManifest()
        {
            try
            {
                string projectRoot = Directory.GetParent(Application.dataPath).FullName;
                string path = Path.Combine(projectRoot, "Assets/Plugins/ES/0_Stand/BaseDefine_RunTime/ShaderSystem/ESRenderSceneTemplateManifest.json");
                if (!File.Exists(path))
                    return "失败：场景模板 Manifest 不存在。";

                SceneTemplateManifest manifest = JsonUtility.FromJson<SceneTemplateManifest>(File.ReadAllText(path));
                if (manifest == null || manifest.templates == null || manifest.templates.Length != 8)
                    return "失败：场景模板数量不是 8。";
                for (int i = 0; i < manifest.templates.Length; i++)
                {
                    SceneTemplateEntry entry = manifest.templates[i];
                    if (string.IsNullOrEmpty(entry.templateId) || string.IsNullOrEmpty(entry.contentType)
                        || string.IsNullOrEmpty(entry.style) || string.IsNullOrEmpty(entry.intent)
                        || string.IsNullOrEmpty(entry.renderer) || string.IsNullOrEmpty(entry.material)
                        || string.IsNullOrEmpty(entry.volume) || string.IsNullOrEmpty(entry.shader))
                        return "失败：第 " + (i + 1) + " 个场景模板字段不完整。";
                }
                return "通过：已读取 8 个 ES URP 场景模板（仅校验，未写入）。";
            }
            catch (Exception exception)
            {
                return "失败：" + exception.GetType().Name + "。";
            }
        }

        [Serializable]
        private sealed class SceneTemplateManifest
        {
            public SceneTemplateEntry[] templates;
        }

        [Serializable]
        private sealed class SceneTemplateEntry
        {
            public string templateId;
            public string contentType;
            public string style;
            public string intent;
            public string renderer;
            public string material;
            public string volume;
            public string shader;
        }

        private void Capture()
        {
            hasSnapshot = ESRenderBackendSnapshot.TryCapture(out snapshot, out status);
            hasPlan = false;
            Repaint();
        }

        private static string[] BuildTemplateLabels()
        {
            string[] labels = new string[ESRenderStyleCatalog.Count];
            for (int i = 0; i < labels.Length; i++)
                labels[i] = ESRenderStyleCatalog.GetStyleIdAt(i).ToString();
            return labels;
        }

        private static string[] BuildContentTypeLabels()
        {
            Array values = Enum.GetValues(typeof(ESRenderContentTypeId));
            string[] labels = new string[values.Length];
            for (int i = 0; i < labels.Length; i++)
                labels[i] = ((ESRenderContentTypeId)values.GetValue(i)).ToString();
            return labels;
        }

        private static string[] BuildSceneIntentLabels()
        {
            Array values = Enum.GetValues(typeof(ESRenderSceneIntentId));
            string[] labels = new string[values.Length];
            for (int i = 0; i < labels.Length; i++)
                labels[i] = ((ESRenderSceneIntentId)values.GetValue(i)).ToString();
            return labels;
        }

        private static string[] BuildPlatformLabels()
        {
            Array values = Enum.GetValues(typeof(ESRenderPlatformId));
            string[] labels = new string[values.Length];
            for (int i = 0; i < labels.Length; i++)
                labels[i] = ((ESRenderPlatformId)values.GetValue(i)).ToString();
            return labels;
        }

        private void BuildPlan(ESRenderQualityPolicy target)
        {
            if (!hasSnapshot)
            {
                status = "无法生成计划：后端快照不可用。";
                return;
            }

            hasPlan = ESRenderBackendChangePlan.TryCreateDryRun(
                snapshot, target, out plan, out status);
            latestEvidence = null;
            latestBatch = null;
            latestEvidenceJson = string.Empty;
            latestReport = null;
            latestReportJson = string.Empty;
            aggregateReport = null;
            aggregateReportJson = string.Empty;
            batchDecision = null;
            scenarioSummaries = new ESRenderEvidenceScenarioSummary[0];
            Repaint();
        }

        private void Apply()
        {
            if (!hasPlan)
                return;

            string key = "es-urp-editor-" + Guid.NewGuid().ToString("N");
            if (!ESRenderBackendApplyGate.TryAuthorize(
                plan, snapshot, true, key, out ESRenderBackendApplyGate gate, out status))
                return;

            bool applied = ESRenderBackendUnityWriter.TryApply(
                plan, gate, key, out lastReceipt, out status);
            status = applied
                ? "ES URP 质量档已应用并完成后端快照复核。"
                : "ES URP 质量档应用失败：" + status;
            Capture();
        }

        private void StartMetricCapture()
        {
            ESRenderMetricSamplingRequest request = new ESRenderMetricSamplingRequest(
                metricPlatform, metricScenario, metricSampleCount);
            if (!ESUnityProfilerMetricSource.TryCreate(
                request, drawCallsMarker, setPassCallsMarker, cpuTimeMarker, gpuTimeMarker,
                gcAllocMarker, residentMemoryMarker, out metricSource, out status))
                return;
            hasMetrics = false;
            latestEvidence = null;
            latestBatch = null;
            latestEvidenceJson = string.Empty;
            latestReport = null;
            latestReportJson = string.Empty;
            aggregateReport = null;
            aggregateReportJson = string.Empty;
            batchDecision = null;
            scenarioSummaries = new ESRenderEvidenceScenarioSummary[0];
            status = "ES 采样已开始；请逐帧采集后完成。";
            Repaint();
        }

        private void CaptureMetricFrame()
        {
            if (metricSource == null)
                return;
            bool captured = metricSource.TryCaptureFrame(out status);
            if (captured)
                status = "已采集第 " + metricSource.CapturedSampleCount + " 帧。";
            Repaint();
        }

        private void CompleteMetricCapture()
        {
            if (metricSource == null)
                return;
            if (metricSource.TryComplete(out lastMetrics, out status))
            {
                hasMetrics = true;
                status = "ES 采样完成，可绑定到 EvidenceReceipt。";
            }
            metricSource.Dispose();
            metricSource = null;
            Repaint();
        }

        private void BuildEvidenceBatch()
        {
            if (!hasMetrics || !hasPlan || (lastReceipt.Status != ESRenderBackendReceiptStatus.Verified
                && lastReceipt.Status != ESRenderBackendReceiptStatus.RolledBack))
            {
                status = "生成证据前必须存在已验证的 ES 后端回执。";
                return;
            }

            if (!ESRenderBackendResourceSnapshot.TryCapture(out ESRenderBackendResourceSnapshot resources, out status)
                || !ESRenderVolumeResourceSnapshot.TryCapture(out ESRenderVolumeResourceSnapshot volumes, out status)
                || !ESRenderShaderResourceSnapshot.TryCapture(out ESRenderShaderResourceSnapshot shaders, out status))
                return;

            string key = "es-urp-evidence-" + Guid.NewGuid().ToString("N");
            if (!ESRenderBackendEvidenceReceiptStore.TryCreateWithAllResourceAndMetricsSnapshots(
                plan, lastReceipt, key, resources, volumes, shaders, lastMetrics,
                out latestEvidence, out status))
                return;

            if (!ESRenderEvidenceBatch.TryCreate(
                "es-urp-batch-" + Guid.NewGuid().ToString("N"),
                new[] { latestEvidence }, out latestBatch, out status))
            {
                latestEvidence = null;
                return;
            }

            if (!ESRenderBackendEvidenceReceiptStore.TrySerializeBatch(
                latestBatch, out latestEvidenceJson, out status))
            {
                latestEvidence = null;
                latestBatch = null;
                return;
            }
            status = "EvidenceBatch 已生成（仅内存 JSON，未写入文件）。";
            if (samplingQueue != null && samplingQueue.Status == ESRenderQualitySamplingQueueStatus.InProgress)
                samplingQueue.TryCompleteCurrent(out _);
            Repaint();
        }

        private void BuildEvidenceReport()
        {
            if (latestBatch == null)
            {
                status = "生成报告前必须先生成 EvidenceBatch。";
                return;
            }
            ESRenderQualityPolicy policy = ESRenderQualityPolicy.Resolve(Profiles[selectedProfile]);
            if (!ESRenderEvidenceReport.TryCreate(
                "es-urp-report-" + Guid.NewGuid().ToString("N"),
                baselineBatch, latestBatch, policy, out latestReport, out status))
                return;
            if (!ESRenderBackendEvidenceReceiptStore.TrySerializeReport(
                latestReport, out latestReportJson, out status))
            {
                latestReport = null;
                return;
            }
            status = "ES 渲染回归报告已生成（仅内存 JSON，未写入文件）。";
            Repaint();
        }

        private void ValidateReportPath()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string candidate = Path.IsPathRooted(reportPath)
                ? reportPath
                : Path.Combine(projectRoot, reportPath);
            if (ESRenderBackendEvidenceReceiptStore.TryValidateReportOutputPath(
                projectRoot, candidate, out string normalizedPath, out status))
                status = "报告路径通过 ES 白名单：" + normalizedPath;
            Repaint();
        }

        private void BuildAggregateReport()
        {
            if (latestReport == null)
            {
                status = "生成组合总览前必须先生成回归报告。";
                return;
            }
            if (!ESRenderEvidenceAggregateReport.TryCreate(
                "es-urp-aggregate-" + Guid.NewGuid().ToString("N"),
                new[] { latestReport }, out aggregateReport, out status))
                return;
            if (!ESRenderBackendEvidenceReceiptStore.TrySerializeAggregateReport(
                aggregateReport, out aggregateReportJson, out status))
            {
                aggregateReport = null;
                return;
            }
            status = "ES 多报告组合总览已生成（仅内存 JSON，未写入文件）。";
            Repaint();
        }

        private static string[] BuildProfileLabels()
        {
            string[] labels = new string[Profiles.Length];
            for (int i = 0; i < Profiles.Length; i++)
                labels[i] = Profiles[i].ToString();
            return labels;
        }
    }
}
