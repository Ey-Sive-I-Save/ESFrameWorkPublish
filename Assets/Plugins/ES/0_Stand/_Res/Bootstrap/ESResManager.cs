using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ES
{
    public enum ESResBootstrapState { Created, ValidatingLocalResources, CheckingRemotePolicy, DownloadingRequiredResources, ReadyToEnter, Blocked }

    /// <summary>唯一启动场景入口。模式完全由 ESAssetRunMode 决定，不另设本地/远端开关。</summary>
    public sealed partial class ESResManager : MonoBehaviour
    {
        public static ESResManager Instance { get; private set; }
        public ESResBootstrapState State { get; private set; } = ESResBootstrapState.Created;
        public event Action<ESResBootstrapState> StateChanged;

        [SerializeField] private string initialGameplayScene = string.Empty;
        [SerializeField] private ESGlobalResSetting globalResSetting;
        [SerializeField] private ESResBootstrapTheme bootstrapTheme;

        private ESResBootstrapView bootstrapView;
        private CancellationTokenSource bootstrapCancellation;
        private ESRuntimeReleaseDownloader releaseDownloader;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            bootstrapView = GetComponent<ESResBootstrapView>() ?? gameObject.AddComponent<ESResBootstrapView>();
            bootstrapView.ApplyTheme(bootstrapTheme);
        }

        private void Start()
        {
            // 唯一运行时启动入口：发布模式默认走新版 Release Bootstrap。
            RestartBootstrapFlow();
        }
        private void OnDisable()
        {
            bool isPrimaryInstance = Instance == this;
            bool explicitComponentDisable = Application.isPlaying && isPrimaryInstance && !enabled && gameObject.activeInHierarchy;
            string message = "[ESRes][Bootstrap][Lifecycle] ESResManager.OnDisable"
                + " | ExplicitComponentDisable=" + explicitComponentDisable
                + " | Enabled=" + enabled
                + " | ActiveSelf=" + gameObject.activeSelf
                + " | ActiveInHierarchy=" + gameObject.activeInHierarchy
                + " | Scene=" + gameObject.scene.name
                + " | SceneLoaded=" + gameObject.scene.isLoaded
                + " | IsPrimaryInstance=" + isPrimaryInstance
                + " | IsPlaying=" + Application.isPlaying;
            if (explicitComponentDisable)
                Debug.LogError(message
                    + "\n主启动实例处于 Behaviour.enabled=false 状态。若没有业务代码主动关闭，请优先检查此前 Awake/OnEnable 初始化异常；Unity 可能会禁用初始化失败的组件。\n"
                    + StackTraceUtility.ExtractStackTrace(), this);
            else
                Debug.LogWarning(message, this);
        }
        private void OnDestroy()
        {
            bootstrapCancellation?.Cancel();
            bootstrapCancellation?.Dispose();
            if (Instance == this) Instance = null;
        }

        public void SetState(ESResBootstrapState state)
        {
            if (State == state) return;
            State = state;
            StateChanged?.Invoke(state);
        }

        public void RestartBootstrapFlow()
        {
            bootstrapCancellation?.Cancel();
            bootstrapCancellation?.Dispose();
            bootstrapCancellation = new CancellationTokenSource();
            lastBootstrapError = string.Empty;
            SetState(ESResBootstrapState.Created);
            bootstrapView.SetVisible(true);
            bootstrapView.SetProgress(0f, "正在准备资源启动", string.Empty);
            bootstrapView.SetAction(null, null);
            RunBootstrapFlowAsync(bootstrapCancellation.Token).Forget();
        }

        private async UniTaskVoid RunBootstrapFlowAsync(CancellationToken cancellationToken)
        {
            try
            {
                if (globalResSetting == null)
                    throw new InvalidOperationException("Bootstrap 未配置 ESGlobalResSetting。");

                ESAssetRunMode runMode = ESAssetRunModeSession.Lock(globalResSetting);
                if (runMode != ESAssetRunMode.LocalBuild && runMode != ESAssetRunMode.HotUpdate)
                    throw new InvalidOperationException("Bootstrap 只支持 LocalBuild 或 HotUpdate。Player 中 Editor 模式会自动升级为 LocalBuild；编辑器测试请切换到 LocalBuild 或 HotUpdate。");

                SetState(runMode == ESAssetRunMode.HotUpdate
                    ? ESResBootstrapState.CheckingRemotePolicy
                    : ESResBootstrapState.ValidatingLocalResources);
                bootstrapView.SetStatus(
                    runMode == ESAssetRunMode.HotUpdate ? "正在检查远端资源版本" : "正在验证本地资源包",
                    runMode == ESAssetRunMode.HotUpdate ? "读取发布根清单" : "读取初始包发布清单");

                releaseDownloader = new ESRuntimeReleaseDownloader(globalResSetting, runMode);
                lastReleaseDownloader = releaseDownloader;
                releaseDownloader.ProgressChanged += OnReleaseDownloadProgress;
                releaseDownloader.DownloadSnapshotChanged += OnReleaseDownloadSnapshot;
                SetState(ESResBootstrapState.DownloadingRequiredResources);
                ESRuntimeReleaseDownloadResult result = await ESRuntimeReleaseBootstrap.InitializeAsync(globalResSetting, releaseDownloader, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                bootstrapView.SetStatus("正在初始化游戏资源", "注入 Catalog、资源加载器并预热 GameCore");
                await ESResBootstrapRuntimeBridge.InitializeAsync(globalResSetting, result, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                SetState(ESResBootstrapState.ReadyToEnter);
                bootstrapView.SetProgress(1f, "资源准备完成", "即将进入游戏");
                await EnterConfiguredGameplaySceneAsync(cancellationToken);
            }
            catch (OperationCanceledException) { }
            catch (Exception exception)
            {
                lastBootstrapError = exception.ToString();
                SetState(ESResBootstrapState.Blocked);
                bootstrapView.SetProgress(0f, "资源启动失败", exception.Message);
                bootstrapView.SetAction(RestartBootstrapFlow, "重试");
                Debug.LogException(exception, this);
            }
            finally
            {
                if (releaseDownloader != null)
                {
                    releaseDownloader.ProgressChanged -= OnReleaseDownloadProgress;
                    releaseDownloader.DownloadSnapshotChanged -= OnReleaseDownloadSnapshot;
                }
                releaseDownloader = null;
            }
        }

        private void OnReleaseDownloadProgress(ESRuntimeReleaseDownloadProgress progress)
        {
            string status;
            switch (progress.Stage)
            {
                case ESRuntimeReleaseDownloadStage.ReadingRelease: status = "读取发布根清单"; break;
                case ESRuntimeReleaseDownloadStage.ReadingConsumer: status = "验证 Consumer 清单"; break;
                case ESRuntimeReleaseDownloadStage.ReadingLibraryIdentity: status = "验证 Library 身份"; break;
                case ESRuntimeReleaseDownloadStage.ReadingCatalog: status = "读取资源目录"; break;
                case ESRuntimeReleaseDownloadStage.ReadingAssetBundleManifest: status = "读取 AssetBundle 清单"; break;
                case ESRuntimeReleaseDownloadStage.PreparingTransfer: status = "正在整理资源下载计划"; break;
                case ESRuntimeReleaseDownloadStage.InitializingRuntime: status = "正在初始化运行时资源"; break;
                case ESRuntimeReleaseDownloadStage.Completed: status = "资源文件校验完成"; break;
                default: status = "正在处理资源文件"; break;
            }
            string detail = string.IsNullOrWhiteSpace(progress.Subject) ? string.Empty : progress.Subject;
            bootstrapView.SetStatus(status, detail);
        }

        private void OnReleaseDownloadSnapshot(ESRuntimeReleaseDownloadSnapshot snapshot)
        {
            bootstrapView.SetTransferProgress(snapshot);
        }

        public void EnterConfiguredGameplayScene()
        {
            EnterConfiguredGameplaySceneAsync(destroyCancellationToken).Forget();
        }

        private async UniTask EnterConfiguredGameplaySceneAsync(CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(initialGameplayScene))
                throw new InvalidOperationException("[ESRes][Bootstrap][Scene] 未配置初始游戏场景。");
            if (!Application.CanStreamedLevelBeLoaded(initialGameplayScene))
                throw new InvalidOperationException("[ESRes][Bootstrap][Scene] 初始游戏场景未进入 Player 构建或名称无效：" + initialGameplayScene);

            Debug.Log("[ESRes][Bootstrap][Scene] 开始进入初始游戏场景：" + initialGameplayScene, this);
            AsyncOperation operation = SceneManager.LoadSceneAsync(initialGameplayScene, LoadSceneMode.Single);
            if (operation == null)
                throw new InvalidOperationException("[ESRes][Bootstrap][Scene] Unity 未能创建场景加载任务：" + initialGameplayScene);

            await operation.ToUniTask(cancellationToken: cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            bootstrapView.SetVisible(false);
            Debug.Log("[ESRes][Bootstrap][Scene] 初始游戏场景加载完成：" + SceneManager.GetActiveScene().name, this);
        }

    }
}
