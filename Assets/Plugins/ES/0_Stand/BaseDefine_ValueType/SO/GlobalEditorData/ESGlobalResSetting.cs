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

        [VerticalGroup("Main/FolderPath")]
        [LabelText("远程资源库构建文件夹（权威路径）"), ShowInInspector, DisplayAsString, InlineButton("OpenOutBuild", "打开远程构建文件夹")]
        public string Path_RemoteResOutBuildPath => Path.Combine(ProjectRootPath, ESOutputRootFolderName, ResParentFolderName);

        [VerticalGroup("Main/FolderPath")]
        [LabelText("构建初始目标（权威路径）"), ShowInInspector, DisplayAsString, InlineButton("OpenInitialTarget", "打开初始目标文件夹")]
        public string Path_BuildInitialTarget => Path.Combine(ProjectRootPath, ESOutputRootFolderName, InitialTargetFolderName);

        private static string ProjectRootPath => Directory.GetParent(Application.dataPath).FullName;

        [VerticalGroup("Main/FolderPath")]
        [FolderPath, LabelText("默认资源库放置文件夹")]
        [InlineButton("Ping_", "<*>")]
        [FormerlySerializedAs("Path_ResLibraryFolder")]
        public string Path_AssetLibraryFolder = "";

        [VerticalGroup("Main/FolderPath")]
        [FolderPath, LabelText("AB帮助代码生成文件夹")]
        [InlineButton("Ping_", "<*>")]
        public string Path_ABHelperCodeGen = "";

        [VerticalGroup("Main/FolderPath")]
        [FolderPath, LabelText("本地AB包资源父路径")]
        [InlineButton("Ping_", "<*>")]
        public string Path_LocalBuildOnEditorPath_ => Path.Combine("Assets/StreamingAssets", ResParentFolderName);

        [VerticalGroup("Main/FolderPath")]
        [LabelText("下载持久相对路径")]
        [InlineButton("OpenPersist", "打开持久下载文件夹")]
        public string Path_Sub_DownloadRelative_ = ResParentFolderName;

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
            string log = ESStandUtility.SafeEditor.Quick_System_CreateDirectory(Path_RemoteResOutBuildPath).Message;
            Debug.Log(log);
            ESStandUtility.SafeEditor.Quick_OpenInSystemFolder(Path_RemoteResOutBuildPath, false);
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
            string platform = ESResMaster.GetValidBuildTargetByRuntimePlatform(applyPlatform).ToString();
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
                if (!string.IsNullOrEmpty(releaseVersion) && string.Equals(Path.GetFileName(releaseVersion), releaseVersion, System.StringComparison.Ordinal))
                {
                    string releaseFolder = Path.GetFullPath(Path.Combine(platformRoot, releaseVersion));
                    string validatedRoot = Path.GetFullPath(platformRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                    if (releaseFolder.StartsWith(validatedRoot, System.StringComparison.OrdinalIgnoreCase) && Directory.Exists(releaseFolder))
                        Directory.Delete(releaseFolder, true);
                }

                File.Delete(rootManifestPath);
                string bundleIndexPath = Path.Combine(platformRoot, "ESAssetReleaseBundleIndex.json");
                if (File.Exists(bundleIndexPath))
                    File.Delete(bundleIndexPath);
                AssetDatabase.Refresh();
            }
            catch (System.Exception exception)
            {
                Debug.LogError("[ESGlobalResSetting] 清理本地发布资源失败：" + exception.Message);
            }
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
            string path = Application.persistentDataPath + "/" + Path_Sub_DownloadRelative_;
            ESStandUtility.SafeEditor.Quick_System_CreateDirectory(path);
            ESStandUtility.SafeEditor.Quick_OpenInSystemFolder(path, false);
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
