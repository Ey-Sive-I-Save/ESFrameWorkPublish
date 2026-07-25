using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ES
{
    public enum ESResBootstrapState { Created, ValidatingLocalResources, CheckingRemotePolicy, DownloadingRequiredResources, ReadyToEnter, Blocked }

    /// <summary>唯一启动场景入口。模式完全由 ESAssetRunMode 决定，不另设本地/远端开关。</summary>
    public sealed class ESResManager : MonoBehaviour
    {
        public static ESResManager Instance { get; private set; }
        public ESResBootstrapState State { get; private set; } = ESResBootstrapState.Created;
        public event Action<ESResBootstrapState> StateChanged;

        [SerializeField] private string initialGameplayScene = string.Empty;
        [SerializeField] private ESGlobalResSetting globalResSetting;
        [SerializeField] private ESResBootstrapTheme bootstrapTheme;

        private ESResBootstrapView bootstrapView;
        private CancellationTokenSource bootstrapCancellation;

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
            SetState(ESResBootstrapState.Created);
            bootstrapView.SetProgress(0f, "资源启动服务已就绪", "下载/验证服务尚未接入 Boot 场景，请由宿主显式调用 ESRuntimeReleaseDownloader。");
        }

        public void EnterConfiguredGameplayScene()
        {
            if (string.IsNullOrWhiteSpace(initialGameplayScene))
            {
                Debug.LogWarning("[ESResManager] No initial gameplay scene is configured.", this);
                return;
            }
            SceneManager.LoadScene(initialGameplayScene, LoadSceneMode.Single);
        }

    }
}
