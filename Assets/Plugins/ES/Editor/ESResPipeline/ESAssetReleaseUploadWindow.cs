using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ES
{
    /// <summary>第五步远端发布的非敏感项目配置。访问密钥只允许由 Provider 的凭据源在执行时读取。</summary>
    public sealed class ESAssetReleaseUploadSettings : ScriptableObject
    {
        internal const string AssetPath = "Assets/ESNormalAssets/Data/GlobalData/AssetSettings/ESAssetReleaseUploadSettings.asset";

        public ESAssetReleaseUploadTarget target = new ESAssetReleaseUploadTarget();

        internal static ESAssetReleaseUploadSettings Load()
        {
            return AssetDatabase.LoadAssetAtPath<ESAssetReleaseUploadSettings>(AssetPath);
        }

        internal static ESAssetReleaseUploadSettings Create()
        {
            ESAssetReleaseUploadSettings existing = Load();
            if (existing != null) return existing;
            string folder = Path.GetDirectoryName(AssetPath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(folder)) throw new InvalidOperationException("远端发布设置路径无效。");
            Directory.CreateDirectory(folder);
            var settings = CreateInstance<ESAssetReleaseUploadSettings>();
            AssetDatabase.CreateAsset(settings, AssetPath);
            AssetDatabase.SaveAssets();
            return settings;
        }
    }

    /// <summary>
    /// 资源四步构建后的第五步。此窗口从已生成上传计划读取输入，绝不重新构建、重写 Root 或保存凭据。
    /// </summary>
    public sealed class ESAssetReleaseUploadWindow : EditorWindow
    {
        private ESAssetReleaseUploadSettings settings;
        private ESAssetReleaseUploadPlan selectedPlan;
        private string selectedPlanPath = string.Empty;
        private string status = "请选择或读取第四步生成的上传计划。";
        private Vector2 scrollPosition;

        internal static void Open()
        {
            ESAssetReleaseUploadWindow window = GetWindow<ESAssetReleaseUploadWindow>();
            window.titleContent = new GUIContent("ES远端发布");
            window.minSize = new Vector2(640f, 440f);
            window.Show();
        }

        private void OnEnable()
        {
            settings = ESAssetReleaseUploadSettings.Load();
            TryLoadLatestPlan();
        }

        private void OnGUI()
        {
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
            EditorGUILayout.LabelField("第五步：发布到远端", EditorStyles.boldLabel);
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

                ESAssetReleaseUploadTarget target = settings.target ??= new ESAssetReleaseUploadTarget();
                EditorGUI.BeginChangeCheck();
                target.displayName = EditorGUILayout.TextField("显示名称", target.displayName);
                target.mode = (ESAssetReleaseUploadMode)EditorGUILayout.EnumPopup("发布方式", target.mode);
                target.region = EditorGUILayout.TextField("OSS 地域", target.region);
                target.endpoint = EditorGUILayout.TextField("Endpoint", target.endpoint);
                target.bucket = EditorGUILayout.TextField("Bucket", target.bucket);
                target.objectPrefix = EditorGUILayout.TextField("对象前缀", target.objectPrefix);
                target.validationPrefix = EditorGUILayout.TextField("验证隔离前缀", target.validationPrefix);
                target.publicBaseUrl = EditorGUILayout.TextField("客户端访问根地址", target.publicBaseUrl);
                target.credentialProfile = EditorGUILayout.TextField("凭据配置名", target.credentialProfile);
                target.verifyRemoteAfterUpload = EditorGUILayout.Toggle("上传后 HEAD 校验", target.verifyRemoteAfterUpload);
                target.refreshCdnAfterUpload = EditorGUILayout.Toggle("发布后刷新 Root CDN", target.refreshCdnAfterUpload);
                if (EditorGUI.EndChangeCheck())
                {
                    EditorUtility.SetDirty(settings);
                    AssetDatabase.SaveAssets();
                }

                if (target.mode == ESAssetReleaseUploadMode.ManualPlan)
                    EditorGUILayout.HelpBox("“手动上传计划”只能导出交接清单，不能伪装为一键远端发布。选择真实 OSS、S3 或 HTTP PUT Provider 后才允许执行。", MessageType.Warning);
                else
                    EditorGUILayout.HelpBox("客户端访问根地址必须包含对象前缀、但不包含平台目录。例如对象前缀为 es-release 时填写 https://<bucket-domain>/es-release/；预检会与第四步 Manifest 严格比对。", MessageType.None);
            }
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
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("读取最新上传计划")) TryLoadLatestPlan();
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
                using (new EditorGUI.DisabledScope(!hasInput))
                {
                    if (GUILayout.Button("初步验证远端隔离区", GUILayout.Height(30f))) BeginValidation();
                    if (GUILayout.Button("预检远端发布", GUILayout.Height(30f))) RunPreflight();
                    if (GUILayout.Button("正式发布到远端", GUILayout.Height(38f))) BeginPublish();
                }
                EditorGUILayout.HelpBox("初步验证只写入验证隔离前缀（默认 .es-validation），执行一次探针上传、HEAD 校验和清理，不接触正式版本目录与 Root。正式发布前仍必须运行预检。", MessageType.None);
            }
        }

        private void RunPreflight()
        {
            ESAssetReleaseUploadPreflightResult result = ESAssetReleaseUploadCoordinator.Preflight(CreateRequest());
            status = result.Message;
            Repaint();
        }

        private void BeginValidation()
        {
            if (settings == null || settings.target == null) return;
            if (!EditorUtility.DisplayDialog("验证远端隔离区", "只会在“" + settings.target.validationPrefix + "”前缀写入一次探针对象，验证后清理。不会改动正式版本。", "开始验证", "取消"))
                return;
            try
            {
                ESAssetReleaseUploadCoordinator.EnqueueValidation(settings.target, result =>
                {
                    status = result.Message;
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
            ESAssetReleaseUploadPreflightResult preflight = ESAssetReleaseUploadCoordinator.Preflight(CreateRequest());
            if (!preflight.IsSuccess)
            {
                status = "发布未开始：" + preflight.Message;
                return;
            }
            if (!EditorUtility.DisplayDialog("确认发布到远端", preflight.Message + "\n\n确认后将开始上传；根发布清单会在所有叶子文件成功后最后上传。", "发布", "取消"))
                return;
            ESAssetReleaseUploadCoordinator.Enqueue(CreateRequest(), result =>
            {
                status = result.Message;
                ShowNotification(new GUIContent(result.IsSuccess ? "远端发布完成" : "远端发布失败"));
                Repaint();
            });
            status = "远端发布任务已加入队列。";
        }

        private ESAssetReleaseUploadRequest CreateRequest()
        {
            if (settings == null || selectedPlan == null) throw new InvalidOperationException("缺少远端发布配置或上传计划。");
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
            selectedPlan = ESAssetPipelineIO.ReadJson<ESAssetReleaseUploadPlan>(path);
            selectedPlanPath = path;
            status = selectedPlan == null ? "上传计划无法读取：" + path : "已读取第四步上传计划；请先运行预检。";
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
