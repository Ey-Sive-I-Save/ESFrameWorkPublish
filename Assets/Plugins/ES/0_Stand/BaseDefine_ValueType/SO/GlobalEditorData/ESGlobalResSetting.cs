using ES;
using Sirenix.OdinInspector;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.Serialization;
using System.IO;

namespace ES
{
    [HideMonoScript]
    [CreateAssetMenu(fileName = "全局资源管理设置", menuName = MenuItemPathDefine.ASSET_GLOBAL_SO_PATH + "全局资源管理设置")]
    public class ESGlobalResSetting : ESEditorGlobalSo<ESGlobalResSetting>
    {
        public const string ResParentFolderName = "Res";
        public const string ResConsumersExpandParentFolderName = "_ExpandConsumers";
        public const string ESOutputRootFolderName = "ES";
        public const string ResourcePipelineFolderName = "ResourcePipeline";
        public const string ReleasesFolderName = "Releases";
        public const string InitialTargetFolderName = "InitialTarget";

        [DisplayAsString(fontSize: 30, Alignment = TextAlignment.Center), HideLabel, GUIColor("@ESDesignUtility.ColorSelector.Color_01")]
        public string createText = "--资源管理全局设置--";

        [HorizontalGroup("Main", Order = 5, MarginRight = 50)]
        [VerticalGroup("Main/BuildAndRun")]
        [Header("构建与运行")]
        [LabelText("应用平台")]
        public RuntimePlatform applyPlatform = RuntimePlatform.WindowsPlayer;

        [VerticalGroup("Main/BuildAndRun")]
        [LabelText("资源加载模式")]
        [EnumToggleButtons]
        [InfoBox("运行时资产入口的总模式。业务代码不直接判断路径，只通过 AssetTable / AssetModule 使用该模式。", InfoMessageType.Info)]
        public ESAssetRunMode AssetRunMode = ESAssetRunMode.EditorDirect;

        [VerticalGroup("Main/BuildAndRun")]
        [LabelText("辅助代码生成模式")]
        [HideInInspector]
        public ESABCodegenMode CodegenMode = ESABCodegenMode.CodeAsOriginal;

        [VerticalGroup("Main/BuildAndRun")]
        [LabelText("游戏版本号")]
        public string Version = "1.0.0";

        [VerticalGroup("Main/BuildAndRun")]
        [LabelText("输出资源详细流程日志")]
        public bool EnableResVerboseLog = false;

        [HorizontalGroup("Main")]
        [Header("文件夹")]
        [VerticalGroup("Main/FolderPath")]
        [DetailedInfoBox("需要自己配置好", "参考本体包资源路径。", InfoMessageType.Warning, VisibleIf = "@Path_Net.Length<10")]
        [LabelText("服务器网络路径")]
        public string Path_Net = "http....";

        [HideInInspector]
        public string Path_RemoteResOutBuildPath => Path.Combine(ProjectRootPath, ESOutputRootFolderName, ResourcePipelineFolderName, ReleasesFolderName);

        [HideInInspector]
        public string Path_RemotePlatform => Path.Combine(Path_RemoteResOutBuildPath, CurrentBuildPlatformName);

        [HideInInspector]
        public string Path_BuildStaging => Path.Combine(ProjectRootPath, ESOutputRootFolderName, ResourcePipelineFolderName, "BuildStaging", CurrentBuildPlatformName);

        [HideInInspector]
        public string Path_PipelineBaked => Path.Combine(ProjectRootPath, ESOutputRootFolderName, ResourcePipelineFolderName, "Baked");

        [HideInInspector]
        public string Path_PipelinePlanned => Path.Combine(ProjectRootPath, ESOutputRootFolderName, ResourcePipelineFolderName, "Planned", CurrentBuildPlatformName);

        [HideInInspector]
        public string Path_PipelineBuildCache => Path.Combine(ProjectRootPath, ESOutputRootFolderName, ResourcePipelineFolderName, "BuildCache", CurrentBuildPlatformName, "UnityAssetBundles");

        [HideInInspector]
        public string Path_LocalTest => Path.Combine(ProjectRootPath, ESOutputRootFolderName, ResourcePipelineFolderName, "Published", "LocalTest", CurrentBuildPlatformName);

        [HideInInspector]
        public string Path_ManualUploadPlans => Path.Combine(ProjectRootPath, ESOutputRootFolderName, ResourcePipelineFolderName, "Published", "ManualUploadPlans", CurrentBuildPlatformName);

        [HideInInspector]
        public string Path_BuildInitialTarget => Path.Combine(ProjectRootPath, ESOutputRootFolderName, ResourcePipelineFolderName, InitialTargetFolderName);

        private static string ProjectRootPath => Directory.GetParent(Application.dataPath).FullName;
        private string CurrentBuildPlatformName => ESAssetBundleUtility.GetBuildPlatformName(applyPlatform);

        [VerticalGroup("Main/FolderPath")]
        [FolderPath, LabelText("默认资源库放置文件夹")]
        [InlineButton("Ping_", "<*>")]
        [HideInInspector]
        [FormerlySerializedAs("Path_ResLibraryFolder")]
        public string Path_AssetLibraryFolder = "";

        [VerticalGroup("Main/FolderPath")]
        [FolderPath, LabelText("AB帮助代码生成文件夹")]
        [InlineButton("Ping_", "<*>")]
        [HideInInspector]
        public string Path_ABHelperCodeGen = "";

        [HideInInspector]
        public string Path_LocalBuildOnEditorPath_ => Path.Combine("Assets", "StreamingAssets", ResParentFolderName);

        [HideInInspector]
        public string Path_LocalBuildPlatform => Path.Combine(ProjectRootPath, Path_LocalBuildOnEditorPath_, CurrentBuildPlatformName);

        [VerticalGroup("Main/FolderPath")]
        [LabelText("下载持久相对路径")]
        [InlineButton("OpenPersist", "打开持久下载文件夹")]
        public string Path_Sub_DownloadRelative_ = ResParentFolderName;

        [HideInInspector]
        public string Path_RuntimeDownloadCache =>
            TryGetRuntimeDownloadCachePath(out string path, out _) ? path : string.Empty;

        /// <summary>
        /// Resolves the runtime download cache only when the configured value is a
        /// relative, normalized path below Application.persistentDataPath.
        /// Invalid serialized/configured values fail closed instead of escaping the cache root.
        /// </summary>
        public bool TryGetRuntimeDownloadCachePath(out string fullPath, out string error)
        {
            fullPath = string.Empty;
            error = string.Empty;

            if (!TryNormalizeDownloadRelativePath(Path_Sub_DownloadRelative_, out string normalized, out error))
                return false;

            string persistentRoot = Path.GetFullPath(Application.persistentDataPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string candidate = Path.GetFullPath(Path.Combine(
                persistentRoot,
                normalized.Replace('/', Path.DirectorySeparatorChar)));
            string requiredPrefix = persistentRoot + Path.DirectorySeparatorChar;
            if (!candidate.StartsWith(requiredPrefix, System.StringComparison.OrdinalIgnoreCase))
            {
                error = "规范化后的下载缓存路径不在 persistentDataPath 子目录内。";
                return false;
            }

            if (ContainsExistingReparsePoint(persistentRoot, candidate))
            {
                error = "下载缓存路径不能穿过 junction/symlink。";
                return false;
            }

            fullPath = candidate;
            return true;
        }

        public static bool TryNormalizeDownloadRelativePath(string value, out string normalized, out string error)
        {
            normalized = string.Empty;
            error = string.Empty;
            string candidate = (value ?? string.Empty).Trim().Replace('\\', '/');
            if (string.IsNullOrEmpty(candidate))
            {
                error = "下载缓存相对路径不能为空。";
                return false;
            }

            if (Path.IsPathRooted(candidate)
                || (candidate.Length >= 2 && char.IsLetter(candidate[0]) && candidate[1] == ':')
                || candidate.StartsWith("/", System.StringComparison.Ordinal))
            {
                error = "下载缓存路径必须是相对路径，不能是绝对路径。";
                return false;
            }

            string[] segments = candidate.Split('/');
            for (int i = 0; i < segments.Length; i++)
            {
                string segment = segments[i].Trim();
                if (string.IsNullOrEmpty(segment) || segment == "." || segment == ".."
                    || segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                {
                    error = "下载缓存路径包含非法、空或目录逃逸片段：" + value;
                    return false;
                }

                segments[i] = segment;
            }

            normalized = string.Join("/", segments);
            return true;
        }

        private static bool ContainsExistingReparsePoint(string root, string candidate)
        {
            string rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string current = rootFull;
            string relative = candidate.Substring(rootFull.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            foreach (string segment in relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            {
                if (string.IsNullOrEmpty(segment)) continue;
                current = Path.Combine(current, segment);
                if (!Directory.Exists(current) && !File.Exists(current)) break;
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) return true;
            }
            return false;
        }

        [VerticalGroup("Main/FolderPath")]
        [OnInspectorGUI]
        private void DrawGeneratedFolderShortcuts()
        {
#if UNITY_EDITOR
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("高频目录", EditorStyles.boldLabel);
            DrawFolderShortcut("远端当前平台", Path_RemotePlatform, true);
            DrawFolderShortcut("本机测试 LocalTest", Path_LocalTest, true);
            DrawFolderShortcut("内置 StreamingAssets", Path_LocalBuildPlatform, true);
            DrawFolderShortcut("运行时下载缓存", Path_RuntimeDownloadCache, true);

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("管线与维护目录", EditorStyles.boldLabel);
            DrawFolderShortcut("构建暂存 Staging", Path_BuildStaging);
            DrawFolderShortcut("Unity AB BuildCache", Path_PipelineBuildCache);
            DrawFolderShortcut("远端发布根目录", Path_RemoteResOutBuildPath);
            DrawFolderShortcut("手工上传计划", Path_ManualUploadPlans);
            DrawFolderShortcut("分包计划 Planned", Path_PipelinePlanned);
            DrawFolderShortcut("引用烘焙 Baked", Path_PipelineBaked);
#endif
        }

#if UNITY_EDITOR
        private static void DrawFolderShortcut(string label, string path, bool important = false)
        {
            if (string.IsNullOrEmpty(path))
            {
                EditorGUILayout.HelpBox(label + "路径无效，请修正下载持久相对路径。", MessageType.Error);
                return;
            }

            string fullPath = Path.GetFullPath(path);
            string displayPath = GetCompactDisplayPath(fullPath);
            using (new EditorGUILayout.HorizontalScope())
            {
                bool exists = Directory.Exists(fullPath);
                var labelContent = new GUIContent((exists ? "● " : "○ ") + label, fullPath);
                Color previousContentColor = GUI.contentColor;
                if (important) GUI.contentColor = new Color(1f, 0.72f, 0.20f);
                EditorGUILayout.LabelField(labelContent, important ? EditorStyles.boldLabel : EditorStyles.label, GUILayout.Width(155));
                GUI.contentColor = previousContentColor;
                var pathContent = new GUIContent(displayPath, fullPath + "\n点击文本可选择并复制；点击右侧按钮可创建并打开目录。");
                EditorGUILayout.SelectableLabel(pathContent.text, EditorStyles.textField,
                    GUILayout.Height(EditorGUIUtility.singleLineHeight), GUILayout.MinWidth(80));
                Color previousBackgroundColor = GUI.backgroundColor;
                if (important) GUI.backgroundColor = new Color(1f, 0.67f, 0.15f);
                bool open = GUILayout.Button(new GUIContent("打开", fullPath), GUILayout.Width(48), GUILayout.Height(EditorGUIUtility.singleLineHeight));
                GUI.backgroundColor = previousBackgroundColor;
                if (open)
                    OpenGeneratedFolder(fullPath);
            }
        }

        private static string GetCompactDisplayPath(string fullPath)
        {
            string projectRoot = Path.GetFullPath(ProjectRootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string normalized = Path.GetFullPath(fullPath);
            if (normalized.StartsWith(projectRoot + Path.DirectorySeparatorChar, System.StringComparison.OrdinalIgnoreCase))
                return normalized.Substring(projectRoot.Length + 1).Replace('\\', '/');
            return normalized.Replace('\\', '/');
        }
#endif

        public bool IsHotUpdateMode => AssetRunMode == ESAssetRunMode.HotUpdate;

        public static bool IsAssetBundleReleaseMode(ESAssetRunMode mode)
        {
            return mode == ESAssetRunMode.LocalBuild || mode == ESAssetRunMode.HotUpdate;
        }

        public bool ShouldUseRemoteLibrary(bool libraryAllowsRemote)
        {
            return IsHotUpdateMode && libraryAllowsRemote;
        }

        public override void OnEditorInitialized()
        {
#if UNITY_EDITOR
            base.OnEditorInitialized();
            SHOW_Global = () => Selection.activeObject == this;
#endif
        }

        private void OpenOutBuild()
        {
            OpenGeneratedFolder(Path_RemoteResOutBuildPath);
        }

        private static void OpenGeneratedFolder(string path)
        {
            string fullPath = Path.GetFullPath(path);
            string log = ESStandUtility.SafeEditor.Quick_System_CreateDirectory(fullPath).Message;
            Debug.Log(log);
            ESStandUtility.SafeEditor.Quick_OpenInSystemFolder(fullPath, false);
        }

#if UNITY_EDITOR
        [SerializeField, HideInInspector] private bool editorRunModeSnapshotInitialized;
        [SerializeField, HideInInspector] private ESAssetRunMode previousEditorRunMode;

        private void OnValidate()
        {
            if (Application.isPlaying)
                return;

            if (!editorRunModeSnapshotInitialized)
            {
                previousEditorRunMode = AssetRunMode;
                editorRunModeSnapshotInitialized = true;
                return;
            }

            bool switchedFromLocalRelease = previousEditorRunMode == ESAssetRunMode.LocalBuild;
            bool switchedToRemoteRelease = AssetRunMode == ESAssetRunMode.HotUpdate;
            if (switchedFromLocalRelease && switchedToRemoteRelease)
                PromptLocalReleaseCleanup();

            previousEditorRunMode = AssetRunMode;
            EditorUtility.SetDirty(this);
        }

        private void PromptLocalReleaseCleanup()
        {
            string platform = ESAssetBundleUtility.GetBuildPlatformName(applyPlatform);
            string platformRoot = Path.Combine(Application.streamingAssetsPath, ResParentFolderName, platform);
            string rootManifestPath = Path.Combine(platformRoot, "ESAssetReleaseManifest.json");
            if (!File.Exists(rootManifestPath))
                return;

            int choice = EditorUtility.DisplayDialogComplex(
                "切换至热更新模式",
                "检测到 StreamingAssets 中存在当前本地发布资源。是否删除当前版本的 ES 生成资源？\n\n只会删除根清单指定的当前发布版本，以及新版根清单和 Bundle 索引；不会删除旧 ESRes 文件。",
                "删除当前本地发布资源",
                "保留",
                "帮我定位");

            if (choice == 0)
                DeleteCurrentLocalRelease(platformRoot, rootManifestPath);
            else if (choice == 2)
                ESStandUtility.SafeEditor.Quick_OpenInSystemFolder(platformRoot, false);
        }

        private static void DeleteCurrentLocalRelease(string platformRoot, string rootManifestPath)
        {
            try
            {
                ESLocalReleasePointer pointer = JsonUtility.FromJson<ESLocalReleasePointer>(File.ReadAllText(rootManifestPath));
                string releaseVersion = pointer != null ? pointer.releaseVersion : null;
                if (IsSafeReleaseFolderName(releaseVersion))
                {
                    string releaseFolder = Path.GetFullPath(Path.Combine(platformRoot, releaseVersion));
                    string validatedRoot = Path.GetFullPath(platformRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                    if (releaseFolder.StartsWith(validatedRoot, System.StringComparison.OrdinalIgnoreCase) && Directory.Exists(releaseFolder))
                        ESManagedFileIO.DeleteDirectory(releaseFolder, platformRoot);
                }

                ESManagedFileIO.DeleteFile(rootManifestPath, platformRoot);
                string bundleIndexPath = Path.Combine(platformRoot, "ESAssetReleaseBundleIndex.json");
                if (File.Exists(bundleIndexPath))
                    ESManagedFileIO.DeleteFile(bundleIndexPath, platformRoot);
                AssetDatabase.Refresh();
            }
            catch (System.Exception exception)
            {
                Debug.LogError("[ESGlobalResSetting] 清理本地发布资源失败：" + exception.Message);
            }
        }

        private static bool IsSafeReleaseFolderName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            string candidate = value.Trim();
            if (candidate == "." || candidate == ".."
                || Path.IsPathRooted(candidate)
                || candidate.IndexOf('/') >= 0
                || candidate.IndexOf('\\') >= 0
                || candidate.IndexOf(':') >= 0
                || !string.Equals(Path.GetFileName(candidate), candidate, System.StringComparison.Ordinal))
                return false;

            char[] invalid = Path.GetInvalidFileNameChars();
            for (int i = 0; i < invalid.Length; i++)
            {
                if (candidate.IndexOf(invalid[i]) >= 0)
                    return false;
            }

            return true;
        }

        [System.Serializable]
        private sealed class ESLocalReleasePointer
        {
            public string releaseVersion = string.Empty;
        }
#endif

        private void OpenInitialTarget()
        {
            string log = ESStandUtility.SafeEditor.Quick_System_CreateDirectory(Path_BuildInitialTarget).Message;
            Debug.Log(log);
            ESStandUtility.SafeEditor.Quick_OpenInSystemFolder(Path_BuildInitialTarget, false);
        }

        private void OpenPersist()
        {
            if (!TryGetRuntimeDownloadCachePath(out string path, out string error))
            {
                Debug.LogError("[ESGlobalResSetting] 无法打开运行时下载缓存：" + error);
                return;
            }

            OpenGeneratedFolder(path);
        }

        private void Ping_(string path)
        {
            ESStandUtility.SafeEditor.Quick_CreateFolderByFullPath(path);
            ESStandUtility.SafeEditor.Quick_PingAssetByPath(path);
        }
    }

    /// <summary>
    /// ES 资产加载运行模式。
    /// 模式只决定加载后端，不改变业务层的资产查询 API。
    /// </summary>
    public enum ESAssetRunMode
    {
        [InspectorName("编辑器直连")]
        EditorDirect,

        [InspectorName("编辑器模拟发布")]
        EditorSimulateBuild,

        [InspectorName("本地构建资源")]
        LocalBuild,

        [InspectorName("热更新资源")]
        HotUpdate
    }

    public enum ESABCodegenMode
    {
        [InspectorName("不生成代码")]
        NoneCode,

        [InspectorName("默认生成")]
        CodeAsOriginal,

        [InspectorName("转为大写")]
        CodeAsUpper,

        [InspectorName("转为小写")]
        CodeAsLower
    }
}
