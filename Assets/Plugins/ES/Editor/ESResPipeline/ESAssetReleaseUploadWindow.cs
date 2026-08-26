using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// 资源四步构建后的第五步。此窗口从已生成上传计划读取输入，绝不重新构建、重写 Root 或保存凭据。
    /// </summary>
    public sealed class ESAssetReleaseUploadWindow : ESSinglePageIMGUIWindow<ESAssetReleaseUploadWindow>
    {
        private ESAssetReleaseUploadSettings settings;
        private ESAssetReleaseUploadPlan selectedPlan;
        private string selectedPlanPath = string.Empty;
        private string selectedPlanFingerprint = string.Empty;
        private string selectedPlanHash = string.Empty;
        private bool preflightPassed;
        private bool lastUploadFailed;
        private ESEditorLongTask activeUploadTask;
        private int lifecycleGeneration;
        private string status = "请选择或读取第四步生成的上传计划。";
        private Vector2 scrollPosition;

        internal static void Open()
        {
            ESAssetReleaseUploadWindow window = GetWindow<ESAssetReleaseUploadWindow>();
            window.titleContent = new GUIContent("ES远端发布");
            window.minSize = new Vector2(640f, 440f);
            window.Show();
        }

        public override GUIContent ESWindow_GetWindowGUIContent()
        {
            return new GUIContent("ES 远端发布", "上传第四步生成并验证的 Release 产物");
        }
        public override string ESWindow_PresentationShortTitle => "发布";

        protected override string ESWindow_Subtitle => "第五步：远端发布工具";
        protected override Vector2 ESWindow_MinSize => new Vector2(640f, 440f);
        protected override Vector2 ESWindow_DefaultSize => new Vector2(900f, 720f);
        protected override string ESWindow_PageStableId => "resource.release-upload";
        protected override string ESWindow_PageTitle => "远端发布";
        protected override string ESWindow_PageKeywords => "资源 Release 上传 OSS S3 HTTP 预检 发布";

        protected override void ESWindow_BuildPageActions(
            ICollection<ESMenuTreePageAction> actions)
        {
            actions.Add(new ESMenuTreePageAction(
                    "release.reload-plan",
                    "读取计划",
                    "重新读取第四步生成的最新上传计划。",
                    context =>
                    {
                        TryLoadLatestPlan();
                        context.RefreshPageActions();
                        context.SetStatus(status, selectedPlan != null
                            ? ESMenuTreePageStatus.Info
                            : ESMenuTreePageStatus.Warning);
                        Repaint();
                    })
                .WithUnityIcon("Refresh")
                .WithPriority(100));
            actions.Add(new ESMenuTreePageAction(
                    "release.preflight",
                    "发布预检",
                    "校验上传计划、目标配置和发布顺序。",
                    context =>
                    {
                        RunPreflight();
                        context.RefreshPageActions();
                        context.SetStatus(status, preflightPassed
                            ? ESMenuTreePageStatus.Info
                            : ESMenuTreePageStatus.Error);
                    })
                .When(HasUploadInput)
                .WithUnityIcon("TestPassed")
                .WithPriority(90));
            actions.Add(new ESMenuTreePageAction(
                    "release.publish",
                    "发布",
                    "确认后执行远端发布；Root Manifest 最后上传。",
                    context =>
                    {
                        BeginPublish();
                        context.RefreshPageActions();
                        context.SetStatus(status);
                    })
                .When(() => HasUploadInput() && preflightPassed && !IsUploadTaskActive())
                .WithUnityIcon("CloudConnect")
                .WithPriority(80));
            actions.Add(new ESMenuTreePageAction(
                    "release.cancel",
                    "取消上传",
                    "请求取消当前上传任务。",
                    context =>
                    {
                        CancelUpload();
                        context.RefreshPageActions();
                        context.SetStatus(status, ESMenuTreePageStatus.Warning);
                    })
                .WhenVisible(IsUploadTaskActive)
                .WithUnityIcon("PauseButton")
                .WithPriority(110));
        }

        protected override void ESWindow_OnHostEnable()
        {
            lifecycleGeneration++;
            maxSize = new Vector2(1400f, 1000f);
            settings = ESAssetReleaseUploadSettings.Load();
            TryLoadLatestPlan();
        }

        protected override void ESWindow_OnHostDisable()
        {
            lifecycleGeneration++;
        }

        protected override void ESWindow_DrawIMGUI(ESMenuTreePageContext context)
        {
            RefreshPlanStateIfChanged();
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            DrawHeader();
            DrawTarget();
            DrawPlan();
            DrawActions();
            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("第五步：远端发布工具", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "此步骤只上传第四步已经生成并校验的 Release。所有版本化文件先上传，ESAssetReleaseManifest.json 必须最后上传；它是客户端发现新版本的唯一开关。",
                MessageType.Info);
        }

        private void DrawTarget()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("远端目标（不保存凭据）", EditorStyles.boldLabel);
                if (settings == null)
                {
                    EditorGUILayout.HelpBox("尚未创建远端发布配置。配置只保存 Endpoint、Bucket、对象前缀等非敏感信息；AK/SK、STS Token 不会写入此资产。", MessageType.Warning);
                    if (GUILayout.Button("创建远端发布配置"))
                    {
                        settings = ESAssetReleaseUploadSettings.Create();
                        Selection.activeObject = settings;
                        EditorGUIUtility.PingObject(settings);
                    }
                    return;
                }

                if (settings.target == null)
                {
                    Undo.RecordObject(settings, "初始化远端发布配置");
                    settings.target = new ESAssetReleaseUploadTarget();
                    EditorUtility.SetDirty(settings);
                }

                SerializedObject serializedSettings;
                try
                {
                    serializedSettings = new SerializedObject(settings);
                    serializedSettings.UpdateIfRequiredOrScript();
                }
                catch (Exception exception)
                {
                    EditorGUILayout.HelpBox("远端发布配置在重载或外部修改后已失效，已取消本次编辑：" + exception.Message, MessageType.Warning);
                    return;
                }
                using (serializedSettings)
                {
                    SerializedProperty targetProperty = serializedSettings.FindProperty("target");
                    bool schemaValid = true;
                    schemaValid &= DrawTargetProperty(targetProperty, "displayName", new GUIContent("显示名称"));
                    schemaValid &= DrawTargetProperty(targetProperty, "mode", new GUIContent("发布方式"));
                    schemaValid &= DrawTargetProperty(targetProperty, "region", new GUIContent("OSS 地域"));
                    schemaValid &= DrawTargetProperty(targetProperty, "endpoint", new GUIContent("Endpoint"));
                    schemaValid &= DrawTargetProperty(targetProperty, "bucket", new GUIContent("Bucket"));
                    schemaValid &= DrawTargetProperty(targetProperty, "objectPrefix", new GUIContent("对象前缀"));
                    schemaValid &= DrawTargetProperty(targetProperty, "validationPrefix", new GUIContent("验证隔离前缀"));
                    schemaValid &= DrawTargetProperty(targetProperty, "publicBaseUrl", new GUIContent("客户端访问根地址"));
                    schemaValid &= DrawTargetProperty(targetProperty, "credentialProfile", new GUIContent("凭据配置名"));
                    schemaValid &= DrawTargetProperty(targetProperty, "verifyRemoteAfterUpload", new GUIContent("上传后 HEAD 校验"));
                    schemaValid &= DrawTargetProperty(targetProperty, "refreshCdnAfterUpload", new GUIContent("发布后刷新 Root CDN"));
                    if (!schemaValid)
                    {
                        EditorGUILayout.HelpBox("远端发布配置字段不完整，已取消本次写回。请重新创建或迁移该配置。", MessageType.Error);
                        return;
                    }
                    try
                    {
                        if (serializedSettings.ApplyModifiedProperties())
                        {
                            EditorUtility.SetDirty(settings);
                            AssetDatabase.SaveAssetIfDirty(settings);
                        }
                    }
                    catch (Exception exception)
                    {
                        EditorGUILayout.HelpBox("远端发布配置写回失败，已取消本次保存：" + exception.Message, MessageType.Warning);
                        return;
                    }
                }

                ESAssetReleaseUploadTarget target = settings.target;
                if (target.mode == ESAssetReleaseUploadMode.ManualPlan)
                    EditorGUILayout.HelpBox("“手动上传计划”只能导出交接清单，不能伪装为一键远端发布。选择真实 OSS、S3 或 HTTP PUT Provider 后才允许执行。", MessageType.Warning);
                else
                    EditorGUILayout.HelpBox("客户端访问根地址必须包含对象前缀、但不包含平台目录。例如对象前缀为 es-release 时填写 https://<bucket-domain>/es-release/；预检会与第四步 Manifest 严格比对。", MessageType.None);
            }
        }

        private bool DrawTargetProperty(SerializedProperty targetProperty, string relativeName, GUIContent label)
        {
            if (targetProperty == null) return false;
            SerializedProperty property = targetProperty.FindPropertyRelative(relativeName);
            if (property == null) return false;
            EditorGUILayout.PropertyField(property, label);
            return true;
        }

        private void DrawPlan()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("第四步发布产物", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("上传计划", string.IsNullOrEmpty(selectedPlanPath) ? "未找到" : selectedPlanPath);
                if (selectedPlan != null)
                {
                    long bytes = selectedPlan.files?.Where(item => item != null).Sum(item => item.size) ?? 0L;
                    EditorGUILayout.LabelField("版本", selectedPlan.platform + " / " + selectedPlan.releaseVersion);
                    EditorGUILayout.LabelField("文件", (selectedPlan.files?.Count ?? 0) + " 个，" + EditorUtility.FormatBytes(bytes));
                    EditorGUILayout.LabelField("Generation", string.IsNullOrWhiteSpace(selectedPlan.generatedUtc) ? "未知" : selectedPlan.generatedUtc);
                    EditorGUILayout.LabelField("计划指纹", string.IsNullOrWhiteSpace(selectedPlanHash) ? "未知" : selectedPlanHash);
                    EditorGUILayout.LabelField("目标", settings?.target == null ? "未配置" : settings.target.displayName);
                    EditorGUILayout.LabelField("预检状态", preflightPassed ? "已通过" : "未预检 / 已过期");
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (!string.IsNullOrEmpty(selectedPlanPath) && GUILayout.Button("定位计划"))
                    {
                        UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(ToProjectRelativePath(selectedPlanPath));
                        if (asset != null) { Selection.activeObject = asset; EditorGUIUtility.PingObject(asset); }
                        else EditorUtility.RevealInFinder(selectedPlanPath);
                    }
                }
            }
        }

        private void DrawActions()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("执行", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(status, EditorStyles.wordWrappedMiniLabel);
                bool hasInput = settings != null && selectedPlan != null;
                bool taskActive = activeUploadTask != null && !activeUploadTask.IsFinished;
                using (new EditorGUI.DisabledScope(!hasInput || taskActive))
                    if (GUILayout.Button("初步验证远端隔离区", GUILayout.Height(30f))) BeginValidation();
                if (lastUploadFailed && !taskActive)
                    EditorGUILayout.HelpBox("上次上传失败；确认计划未变化后，可使用右上“发布”动作重试。", MessageType.Warning);
                EditorGUILayout.HelpBox("初步验证只写入验证隔离前缀（默认 .es-validation），执行一次探针上传、HEAD 校验和清理，不接触正式版本目录与 Root。正式发布前仍必须运行预检。", MessageType.None);
            }
        }

        private void RunPreflight()
        {
            try
            {
                ESAssetReleaseUploadPreflightResult result = ESAssetReleaseUploadCoordinator.Preflight(CreateRequest());
                preflightPassed = result.IsSuccess;
                status = result.Message;
                ESResWindow.SetRemotePlanPreflightStatus(
                    result.IsSuccess ? "Ready" : "不可用",
                    result.Message);
            }
            catch (Exception exception)
            {
                preflightPassed = false;
                status = "预检未执行：" + exception.Message;
            }
            ESWindow_CurrentPageContext?.RefreshPageActions();
            Repaint();
        }

        private void BeginValidation()
        {
            if (settings == null || settings.target == null) return;
            if (!EditorUtility.DisplayDialog("验证远端隔离区", "只会在“" + settings.target.validationPrefix + "”前缀写入一次探针对象，验证后清理。不会改动正式版本。", "开始验证", "取消"))
                return;
            try
            {
                int generation = lifecycleGeneration;
                activeUploadTask = ESAssetReleaseUploadCoordinator.EnqueueValidation(settings.target, result =>
                {
                    if (generation != lifecycleGeneration)
                        return;
                    activeUploadTask = null;
                    status = result.Message;
                    ESWindow_CurrentPageContext?.RefreshPageActions();
                    ShowNotification(new GUIContent(result.IsSuccess ? "远端隔离区验证通过" : "远端隔离区验证失败"));
                    Repaint();
                });
                status = "远端隔离区验证任务已加入队列。";
            }
            catch (Exception exception)
            {
                status = "远端隔离区验证未开始：" + exception.Message;
            }
        }

        private void BeginPublish()
        {
            if (!preflightPassed)
            {
                status = "发布未开始：请先运行预检并确认上传计划未变化。";
                return;
            }
            if (activeUploadTask != null && !activeUploadTask.IsFinished)
            {
                status = "发布未开始：已有上传任务正在执行。";
                return;
            }

            ESAssetReleaseUploadPreflightResult preflight;
            try
            {
                preflight = ESAssetReleaseUploadCoordinator.Preflight(CreateRequest());
            }
            catch (Exception exception)
            {
                preflightPassed = false;
                status = "发布未开始：预检失败：" + exception.Message;
                return;
            }
            if (!preflight.IsSuccess)
            {
                status = "发布未开始：" + preflight.Message;
                preflightPassed = false;
                return;
            }
            if (!EditorUtility.DisplayDialog("确认发布到远端", preflight.Message + "\n\n确认后将开始上传；根发布清单会在所有叶子文件成功后最后上传。", "发布", "取消"))
                return;
            lastUploadFailed = false;
            int generation = lifecycleGeneration;
            activeUploadTask = ESAssetReleaseUploadCoordinator.Enqueue(CreateRequest(), result =>
            {
                if (generation != lifecycleGeneration)
                    return;
                activeUploadTask = null;
                lastUploadFailed = !result.IsSuccess;
                status = result.Message;
                ESResWindow.SetRemotePlanPreflightStatus(
                    result.IsSuccess ? "Ready" : "不可用",
                    result.Message);
                ESWindow_CurrentPageContext?.RefreshPageActions();
                ShowNotification(new GUIContent(result.IsSuccess ? "远端发布完成" : "远端发布失败"));
                Repaint();
            });
            status = "远端发布任务已加入队列。";
        }

        private void CancelUpload()
        {
            if (activeUploadTask == null || activeUploadTask.IsFinished)
                return;
            activeUploadTask.Cancel();
            status = "已请求取消上传；请等待任务停止。";
            ESWindow_CurrentPageContext?.RefreshPageActions();
            Repaint();
        }

        private bool HasUploadInput()
        {
            return settings != null && selectedPlan != null;
        }

        private bool IsUploadTaskActive()
        {
            return activeUploadTask != null && !activeUploadTask.IsFinished;
        }

        private ESAssetReleaseUploadRequest CreateRequest()
        {
            if (settings == null || settings.target == null || selectedPlan == null)
                throw new InvalidOperationException("缺少有效的远端发布配置或上传计划。");
            return new ESAssetReleaseUploadRequest(settings.target, selectedPlan);
        }

        private void TryLoadLatestPlan()
        {
            selectedPlan = null;
            selectedPlanPath = string.Empty;
            string folder = ESGlobalResSetting.Instance.Path_ManualUploadPlans;
            if (!Directory.Exists(folder)) { status = "尚未找到第四步上传计划目录。请先执行“4. 发布资源包”。"; return; }
            string path = Directory.EnumerateFiles(folder, "*.json", SearchOption.TopDirectoryOnly)
                .OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
            if (string.IsNullOrEmpty(path)) { status = "尚未找到上传计划。请先执行“4. 发布资源包”。"; return; }
            ESAssetReleaseUploadPlan candidate = ESAssetPipelineIO.ReadJson<ESAssetReleaseUploadPlan>(path);
            if (candidate == null)
            {
                status = "上传计划无法读取：" + path;
                return;
            }
            if (!ESAssetReleaseUploadCoordinator.TryValidateUploadPlanSource(candidate, out string planError))
            {
                selectedPlan = null;
                selectedPlanPath = string.Empty;
                status = "不可用上传计划：" + planError;
                Debug.LogWarning("[ESAssetReleaseUploadWindow] " + status + "\n" + path);
                return;
            }
            selectedPlan = candidate;
            selectedPlanPath = path;
            selectedPlanFingerprint = ESResourcePipelineStageValidators.GetUploadPlanFingerprint(path);
            selectedPlanHash = File.Exists(path)
                ? ESResManifestIntegrity.ComputeFileSha256(path)
                : string.Empty;
            preflightPassed = false;
            status = selectedPlan == null ? "上传计划无法读取：" + path : "已读取第四步上传计划；请先运行预检。";
        }

        private void RefreshPlanStateIfChanged()
        {
            if (string.IsNullOrEmpty(selectedPlanPath))
                return;
            string currentFingerprint = ESResourcePipelineStageValidators.GetUploadPlanFingerprint(selectedPlanPath);
            if (string.Equals(currentFingerprint, selectedPlanFingerprint, StringComparison.Ordinal))
                return;

            selectedPlanFingerprint = currentFingerprint;
            selectedPlanHash = File.Exists(selectedPlanPath)
                ? ESResManifestIntegrity.ComputeFileSha256(selectedPlanPath)
                : string.Empty;
            preflightPassed = false;
            status = "上传计划已变化，请重新预检。";
            Repaint();
        }

        private static string ToProjectRelativePath(string fullPath)
        {
            string projectRoot = ESAssetPipelineIO.ProjectRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string normalized = Path.GetFullPath(fullPath);
            return normalized.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase)
                ? normalized.Substring(projectRoot.Length).Replace('\\', '/')
                : normalized;
        }
    }
}
