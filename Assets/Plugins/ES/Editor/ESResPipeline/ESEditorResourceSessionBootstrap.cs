using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace ES
{
    internal static class ESEditorResourceSessionPrompt
    {
        private static bool handledThisPlaySession;
        private static bool missingConfigPromptShown;

        internal static void Register()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            ESConfigKeyDiagnostics.MissingKey -= OnMissingConfigKey;
            ESConfigKeyDiagnostics.MissingKey += OnMissingConfigKey;
        }

        private static void OnMissingConfigKey(string scope, string description)
        {
            if (missingConfigPromptShown || !EditorApplication.isPlaying || !ESAssets.IsReady)
                return;
            missingConfigPromptShown = true;
            EditorApplication.delayCall += () =>
            {
                if (!EditorApplication.isPlaying || Application.isBatchMode)
                    return;
                int choice = EditorUtility.DisplayDialogComplex(
                    "ES ConfigKey 未完成烘焙",
                    "检测到 ConfigKey/ConfigData 未注入当前运行表。\n\n"
                    + "Scope=" + scope + "\n"
                    + description + "\n\n"
                    + "建议返回编辑器执行 Consumer/GameCore/资源 Catalog Bake，然后重新进入 PlayMode。",
                    "打开资源配置",
                    "忽略本进程后续提示",
                    "确定");
                if (choice == 0)
                {
                    ESGlobalResSetting settings = ESGlobalResSetting.Instance;
                    if (settings != null)
                    {
                        Selection.activeObject = settings;
                        EditorGUIUtility.PingObject(settings);
                    }
                }
            };
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                handledThisPlaySession = false;
                return;
            }

            if (state != PlayModeStateChange.EnteredPlayMode || handledThisPlaySession)
                return;

            handledThisPlaySession = true;
            EditorApplication.delayCall += PromptIfNeeded;
        }

        private static void PromptIfNeeded()
        {
            if (!EditorApplication.isPlaying || ESAssets.IsReady || ESResManager.Instance != null
                || UnityEngine.Object.FindAnyObjectByType<ESEditorResourceSessionBootstrap>() != null)
                return;

            ESGlobalResSetting settings = ESGlobalResSetting.Instance;
            if (settings == null)
            {
                Debug.LogError("[ESRes][EditorSession] 未找到 ESGlobalResSetting GlobalData，无法建立临时资源会话。");
                return;
            }

            if (Application.isBatchMode)
            {
                if (HasCommandLineArgument("-esInitializeTemporaryResourceSession"))
                {
                    ESEditorResourceSessionBootstrap.Create(settings);
                    return;
                }
                throw new InvalidOperationException(
                    "[ESRes][EditorSession] 批处理 PlayMode 未配置正式资源 Bootstrap。"
                    + "如需显式创建临时资源会话，请传入 -esInitializeTemporaryResourceSession；否则失败关闭。");
            }

            int choice = EditorUtility.DisplayDialogComplex(
                "ES 临时资源会话",
                "当前 PlayMode 场景没有新版资源会话，且资源 Provider 尚未初始化。\n\n"
                + "可以使用项目 GlobalData 创建仅属于本次 PlayMode 的临时资源会话。该操作不会修改或弄脏当前 Scene。",
                "初始化本次资源会话",
                "本次不初始化",
                "打开全局资源配置");

            if (choice == 0)
                ESEditorResourceSessionBootstrap.Create(settings);
            else if (choice == 2)
            {
                Selection.activeObject = settings;
                EditorGUIUtility.PingObject(settings);
            }
        }

        private static bool HasCommandLineArgument(string expected)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int i = 0; i < arguments.Length; i++)
                if (string.Equals(arguments[i], expected, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }
    }

    internal sealed class ESEditorResourceSessionAssemblyStreamInitializer : EditorInvoker_Level0
    {
        public override void InitInvoke()
        {
            ESEditorResourceSessionPrompt.Register();
        }
    }

    [DisallowMultipleComponent]
    internal sealed class ESEditorResourceSessionBootstrap : MonoBehaviour
    {
        private sealed class EditorCatalogConflictException : InvalidOperationException
        {
            public EditorCatalogConflictException(string message) : base(message) { }
        }

        private ESGlobalResSetting settings;
        private CancellationTokenSource cancellation;
        private bool runModeSessionTouched;
        private bool destroyed;
        private ESGameManager createdGameManager;
        private ESRuntimeDataModule runtimeData;
        private IESAssetRuntimeProvider providerBeforeSession;
        private IESAssetRuntimeProvider ownedProvider;
        private int ownedProviderGeneration;
        private ESGlobalAssetRuntimeMap temporaryRuntimeMap;

        internal static void Create(ESGlobalResSetting globalSettings)
        {
            if (globalSettings == null) throw new ArgumentNullException(nameof(globalSettings));
            if (ESAssets.IsReady || ESResManager.Instance != null
                || UnityEngine.Object.FindAnyObjectByType<ESEditorResourceSessionBootstrap>() != null)
                return;

            var host = new GameObject("ES Editor Temporary Resource Session");
            host.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(host);
            var bootstrap = host.AddComponent<ESEditorResourceSessionBootstrap>();
            bootstrap.settings = globalSettings;
            bootstrap.BeginAsync().Forget();
        }

        private async UniTaskVoid BeginAsync()
        {
            cancellation = new CancellationTokenSource();
            try
            {
                runtimeData = EnsureRuntimeDataModule();
                providerBeforeSession = runtimeData.ExistingAssetLoadingService?.RuntimeBackend;
                if (providerBeforeSession != null)
                    throw new InvalidOperationException("[ESRes][EditorSession] 资源 Provider 正在已有会话或切换流程中，不能创建第二个临时会话。");
                ESAssetRunMode effectiveMode = ESAssetRunModeSession.Lock(settings);
                runModeSessionTouched = true;
                switch (effectiveMode)
                {
                    case ESAssetRunMode.EditorDirect:
                    {
                        temporaryRuntimeMap = ScriptableObject.CreateInstance<ESGlobalAssetRuntimeMap>();
                        temporaryRuntimeMap.hideFlags = HideFlags.HideAndDontSave;
                        runtimeData.InitializeAssetLoadingForRunMode(temporaryRuntimeMap, settings, ESRuntimeRetryPolicy.Default);
                        await InitializeEditorCatalogsAndGameCoreAsync();
                        CaptureOwnedProvider();
                        break;
                    }
                    case ESAssetRunMode.LocalBuild:
                    case ESAssetRunMode.HotUpdate:
                    {
                        ESRuntimeReleaseDownloadResult result = await ESRuntimeReleaseBootstrap.InitializeAsync(settings, cancellation.Token);
                        cancellation.Token.ThrowIfCancellationRequested();
                        await runtimeData.InitializeAssetLoadingFromReleaseResultAsync(settings, result, cancellation.Token);
                        CaptureOwnedProvider();
                        break;
                    }
                    case ESAssetRunMode.EditorSimulateBuild:
                    {
                        bool hasLocalRelease = Directory.Exists(settings.Path_LocalBuildPlatform)
                            && File.Exists(Path.Combine(settings.Path_LocalBuildPlatform, "ESAssetReleaseManifest.json"))
                            && File.Exists(Path.Combine(settings.Path_LocalBuildPlatform, "ESAssetReleaseBundleIndex.json"));
                        ESAssetRunMode metadataSource = hasLocalRelease ? ESAssetRunMode.LocalBuild : ESAssetRunMode.HotUpdate;
                        var downloader = new ESRuntimeReleaseDownloader(settings, metadataSource);
                        ESRuntimeReleaseDownloadResult result = await downloader.DownloadEditorSimulationMetadataAsync(cancellation.Token);
                        cancellation.Token.ThrowIfCancellationRequested();
                        await runtimeData.InitializeAssetLoadingFromReleaseResultAsync(settings, result, cancellation.Token);
                        CaptureOwnedProvider();
                        break;
                    }
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                if (destroyed)
                {
                    DisposeOwnedSession();
                    return;
                }

                Debug.Log("[ESRes][EditorSession] 临时资源会话初始化完成。ConfiguredMode=" + settings.AssetRunMode
                    + ", EffectiveMode=" + effectiveMode, this);
            }
            catch (OperationCanceledException) { }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                if (!destroyed && !Application.isBatchMode)
                    EditorUtility.DisplayDialog("ES 临时资源会话初始化失败", exception.Message, "确定");
                DisposeOwnedSession();
            }
        }

        private ESRuntimeDataModule EnsureRuntimeDataModule()
        {
            if (ESGameManager.RuntimeData == null && ESGameManager.Instance == null)
            {
                var gameManagerObject = new GameObject("ESGameManager (Editor Resource Session)");
                gameManagerObject.hideFlags = HideFlags.HideAndDontSave;
                gameManagerObject.SetActive(false);
                createdGameManager = gameManagerObject.AddComponent<ESGameManager>();
                createdGameManager.autoCreateCommandModule = false;
                createdGameManager.autoCreateInputModule = false;
                createdGameManager.autoCreateRuntimeDataModule = true;
                createdGameManager.autoCreateGameObjectPoolModule = false;
                createdGameManager.autoCreateAudioModule = false;
                createdGameManager.autoCreateCameraModule = false;
                createdGameManager.autoCreatePhysicsQueryModule = false;
                createdGameManager.autoCreateLODModule = false;
                createdGameManager.dontDestroyOnLoad = true;
                gameManagerObject.SetActive(true);
            }
            else if (ESGameManager.RuntimeData == null && ESGameManager.Instance != null)
            {
                ESGameManager.GetOrCreateModule<ESRuntimeDataModule>();
                ESGameManager.RefreshStaticCache();
            }
            return ESGameManager.RuntimeData
                ?? throw new InvalidOperationException("[ESRes][EditorSession] ESGameManager 未能创建 ESRuntimeDataModule。");
        }

        private void CaptureOwnedProvider()
        {
            ownedProvider = runtimeData?.ExistingAssetLoadingService?.RuntimeBackend;
            ownedProviderGeneration = ESAssets.RuntimeBackendGeneration;
            if (ownedProvider == null)
                throw new InvalidOperationException("[ESRes][EditorSession] 资源服务初始化完成但未绑定 Provider。");
        }

        private async UniTask InitializeEditorCatalogsAndGameCoreAsync()
        {
            List<ESRuntimeCatalog> catalogs = DiscoverEditorRuntimeCatalogs();
            if (catalogs.Count == 0)
            {
                Debug.LogWarning("[ESRes][EditorSession] 未发现 Editor Catalog；直接 ESAssetRefer 仍可使用，但 ConfigKey/ConfigData 表不会自动填充。", this);
            }
            else
            {
                ESRuntimeDataAsset.RebuildAssetConfigTablesFromCatalogs(catalogs);
            }

            string[] catalogGuids = AssetDatabase.FindAssets("t:ESGameCoreAssetPreloadCatalog");
            var gameCoreReferences = new List<ESRuntimeConsumerGameCoreReference>();
            var identities = new HashSet<ESAssetIdentity>();
            for (int i = 0; i < catalogGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(catalogGuids[i]);
                ESGameCoreAssetPreloadCatalog catalog = AssetDatabase.LoadAssetAtPath<ESGameCoreAssetPreloadCatalog>(path);
                if (catalog == null) continue;
                foreach (ESAssetReferBase refer in catalog.assets)
                {
                    if (refer == null || !refer.IsValid || !refer.SupportsGameCorePreload || !identities.Add(refer.AssetIdentity))
                        continue;
                    gameCoreReferences.Add(new ESRuntimeConsumerGameCoreReference
                    {
                        guid = refer.AssetIdentity.Guid,
                        localFileId = refer.AssetIdentity.LocalFileId
                    });
                }
                foreach (ESAssetReferBase refer in catalog.generatedAssets)
                {
                    if (refer == null || !refer.IsValid || !refer.SupportsGameCorePreload || !identities.Add(refer.AssetIdentity))
                        continue;
                    gameCoreReferences.Add(new ESRuntimeConsumerGameCoreReference
                    {
                        guid = refer.AssetIdentity.Guid,
                        localFileId = refer.AssetIdentity.LocalFileId
                    });
                }
            }

            // A standalone GameCore preload catalog is optional. The normal editor source
            // of truth is the baked Consumer data; using it here keeps EditorDirect zero-config
            // for projects that already have Consumer GameCore ownership.
            foreach (ESAssetLibraryConsumer consumer in ESEditorSO.GetGroupOfType<ESAssetLibraryConsumer>() ?? Array.Empty<ESAssetLibraryConsumer>())
            {
                AddEditorGameCoreReferences(consumer?.GameCoreAssets, gameCoreReferences, identities);
                AddEditorGameCoreReferences(consumer?.ManualGameCoreAssets, gameCoreReferences, identities);
            }

            if (gameCoreReferences.Count > 0)
                await runtimeData.PreloadGameCoreAssetsAsync(gameCoreReferences, cancellation.Token);
        }

        private static void AddEditorGameCoreReferences(IEnumerable<ESAssetReferBase> source,
            List<ESRuntimeConsumerGameCoreReference> destination, HashSet<ESAssetIdentity> identities)
        {
            foreach (ESAssetReferBase refer in source ?? Array.Empty<ESAssetReferBase>())
            {
                if (refer == null || !refer.IsValid || !refer.SupportsGameCorePreload)
                    continue;
                AddEditorGameCoreReference(refer.AssetIdentity.Guid, refer.AssetIdentity.LocalFileId, destination, identities);
            }
        }

        private static void AddEditorGameCoreReference(string guid, long localFileId,
            List<ESRuntimeConsumerGameCoreReference> destination, HashSet<ESAssetIdentity> identities)
        {
            if (string.IsNullOrEmpty(guid) || !identities.Add(new ESAssetIdentity(guid, localFileId)))
                return;
            destination.Add(new ESRuntimeConsumerGameCoreReference { guid = guid, localFileId = localFileId });
        }

        private List<ESRuntimeCatalog> DiscoverEditorRuntimeCatalogs()
        {
            var result = new List<ESRuntimeCatalog>();
            var paths = new List<string>();
            var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            AddCatalogFiles(settings.Path_LocalBuildPlatform, paths, seenPaths);
            string bakedRoot = Path.Combine(projectRoot, "ES", "ResourcePipeline", "Baked");
            AddCatalogFiles(bakedRoot, paths, seenPaths);
            var libraries = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string path in paths)
            {
                try
                {
                    ESRuntimeCatalog catalog = ESAssetPipelineIO.ReadJson<ESRuntimeCatalog>(path);
                    if (catalog == null)
                        throw new InvalidDataException("Catalog JSON 为空：" + path);
                    if (catalog.formatVersion != 3)
                        throw new InvalidDataException("Catalog 协议版本不匹配：" + path + "，Version=" + catalog.formatVersion);
                    string libraryKey = string.IsNullOrWhiteSpace(catalog.libraryFolder)
                        ? catalog.libraryName
                        : catalog.libraryFolder;
                    if (string.IsNullOrWhiteSpace(libraryKey))
                        throw new InvalidDataException("Catalog 缺少 libraryFolder/libraryName：" + path);
                    string signature = JsonConvert.SerializeObject(catalog);
                    if (libraries.TryGetValue(libraryKey, out string existingSignature))
                    {
                        if (!string.Equals(existingSignature, signature, StringComparison.Ordinal))
                            throw new EditorCatalogConflictException("同名 Library 的 Editor Catalog 内容冲突：" + libraryKey + "。请重新 Bake 或清理本地发布目录。");
                        continue;
                    }
                    libraries.Add(libraryKey, signature);
                    result.Add(catalog);
                }
                catch (EditorCatalogConflictException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("[ESRes][EditorSession] Catalog 读取失败，已跳过：" + path + "，" + exception.Message);
                }
            }
            return result;
        }

        private static void AddCatalogFiles(string root, List<string> paths, HashSet<string> seenPaths)
        {
            if (string.IsNullOrWhiteSpace(root)) return;
            string fullRoot = Path.GetFullPath(root);
            if (!Directory.Exists(fullRoot)) return;
            foreach (string path in Directory.GetFiles(fullRoot, "ESAssetLibraryCatalog.json", SearchOption.AllDirectories))
                if (seenPaths.Add(path)) paths.Add(path);
        }

        private void OnDestroy()
        {
            destroyed = true;
            cancellation?.Cancel();
            cancellation?.Dispose();
            cancellation = null;
            DisposeOwnedSession();
        }

        private void DisposeOwnedSession()
        {
            if (ownedProvider == null && runModeSessionTouched && providerBeforeSession == null
                && runtimeData != null && ESResManager.Instance == null)
            {
                IESAssetRuntimeProvider candidate = runtimeData.ExistingAssetLoadingService?.RuntimeBackend;
                if (candidate != null)
                {
                    ownedProvider = candidate;
                    ownedProviderGeneration = ESAssets.RuntimeBackendGeneration;
                }
            }

            if (ownedProvider != null)
            {
                if (runtimeData != null
                    && ReferenceEquals(runtimeData.ExistingAssetLoadingService?.RuntimeBackend, ownedProvider)
                    && ESAssets.RuntimeBackendGeneration == ownedProviderGeneration)
                    runtimeData.ExistingAssetLoadingService.Dispose();
                ownedProvider = null;
            }
            if (temporaryRuntimeMap != null)
            {
                UnityEngine.Object.Destroy(temporaryRuntimeMap);
                temporaryRuntimeMap = null;
            }
            if (runModeSessionTouched && !ESAssets.IsReady)
            {
                runModeSessionTouched = false;
                ESAssetRunModeSession.ResetAfterEditorSession();
            }

            if (createdGameManager != null && ReferenceEquals(ESGameManager.Instance, createdGameManager))
            {
                UnityEngine.Object.Destroy(createdGameManager.gameObject);
                createdGameManager = null;
            }
        }
    }
}
