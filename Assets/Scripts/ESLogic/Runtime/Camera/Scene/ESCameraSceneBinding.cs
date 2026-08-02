using System.Threading;
using Cinemachine;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// 每个本地 Camera View 唯一的场景挂载点。它拥有当前 Scene Epoch、输出 Camera、
    /// Brain 和独立 Rig Registry；角色、载具与技能均不得挂相机控制组件。
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-8)]
    [AddComponentMenu("ES/Camera/Scene Binding")]
    public sealed class ESCameraSceneBinding : MonoBehaviour
    {
        [SerializeField] private string viewKey = "MainView";
        [SerializeField] private Camera outputCamera;
        [SerializeField] private CinemachineBrain brain;
        [SerializeField] private ESCameraProfileCatalog profileCatalog;
        [SerializeField] private ESCameraRigCatalog rigCatalog;
        [SerializeField] private CinemachineBlenderSettings blenderSettings;
        [SerializeField] private CinemachineBlendDefinition defaultBlend = new CinemachineBlendDefinition(
            CinemachineBlendDefinition.Style.EaseInOut,
            0.25f);
        [SerializeField] private Transform rigRoot;
        [SerializeField] private bool warmupAllRigs = true;

        private static int nextSceneEpoch;

        private ESCameraCinemachine2ViewAdapter adapter;
        private ESCameraViewId viewId;
        private int sceneEpoch;
        private bool registered;
        private bool reportedConfigurationError;

        public ESCameraViewId ViewId => viewId;
        public int SceneEpoch => sceneEpoch;

#if UNITY_EDITOR
        /// <summary>
        /// 场景制作的显式配置入口。Editor 工具使用此 API，而不通过 SerializedObject
        /// 访问私有运行时字段。
        /// </summary>
        public void ConfigureForAuthoring(
            string newViewKey,
            Camera newOutputCamera,
            CinemachineBrain newBrain,
            ESCameraProfileCatalog newProfileCatalog,
            ESCameraRigCatalog newRigCatalog,
            CinemachineBlenderSettings newBlenderSettings,
            Transform newRigRoot)
        {
            viewKey = newViewKey;
            outputCamera = newOutputCamera;
            brain = newBrain;
            profileCatalog = newProfileCatalog;
            rigCatalog = newRigCatalog;
            blenderSettings = newBlenderSettings;
            rigRoot = newRigRoot;
        }
#endif

        private void Awake()
        {
            if (outputCamera == null)
                outputCamera = GetComponent<Camera>();
            if (brain == null)
                brain = GetComponent<CinemachineBrain>();
            if (rigRoot == null)
                rigRoot = transform;

            viewId = new ESCameraViewId(viewKey);
        }

        private void OnEnable()
        {
            TryRegister();
        }

        // GameManager 的 Awake 顺序固定在 -9；Start 只覆盖手动在 Awake 中启用 Binding 的场景。
        private void Start()
        {
            if (!registered)
                TryRegister();
        }

        private void OnDisable()
        {
            ESCameraModule camera = ESGameManager.Camera;
            if (registered && camera != null)
                camera.UnregisterView(viewId, sceneEpoch, adapter);

            registered = false;
            adapter?.Dispose();
            adapter = null;
        }

        private bool TryRegister()
        {
            if (registered)
                return true;

            if (!ValidateConfiguration())
                return false;

            ESCameraModule camera = ESGameManager.Camera;
            if (camera == null)
                return false;

            sceneEpoch = NextSceneEpoch();
            adapter = new ESCameraCinemachine2ViewAdapter(outputCamera, brain, profileCatalog, rigCatalog, rigRoot);
            if (!adapter.IsReady)
            {
                adapter.Dispose();
                adapter = null;
                ReportConfigurationError("无法创建 CM2 ViewAdapter。请检查 Brain、Output Camera 与 Catalog。" );
                return false;
            }

            brain.m_DefaultBlend = defaultBlend;
            brain.m_CustomBlends = blenderSettings;
            if (warmupAllRigs)
                adapter.Warmup();

            registered = camera.RegisterView(viewId, sceneEpoch, adapter);
            if (!registered)
            {
                adapter.Dispose();
                adapter = null;
                ReportConfigurationError("Director 拒绝注册此 View。" );
            }

            return registered;
        }

        private bool ValidateConfiguration()
        {
            if (!viewId.IsValid)
            {
                ReportConfigurationError("ViewId 不能为空。");
                return false;
            }

            if (outputCamera == null || brain == null || brain.gameObject != outputCamera.gameObject)
            {
                ReportConfigurationError("Output Camera 与 CinemachineBrain 必须在同一个场景对象上。");
                return false;
            }

            if (profileCatalog == null || rigCatalog == null || blenderSettings == null || rigRoot == null)
            {
                ReportConfigurationError("ProfileCatalog、RigCatalog、BlenderSettings 和 RigRoot 都是必填项。");
                return false;
            }

            return true;
        }

        private void ReportConfigurationError(string message)
        {
            if (reportedConfigurationError)
                return;

            reportedConfigurationError = true;
            Debug.LogError($"[ESCamera] SceneBinding '{name}' 配置无效：{message}", this);
        }

        private static int NextSceneEpoch()
        {
            int epoch = Interlocked.Increment(ref nextSceneEpoch);
            if (epoch > 0)
                return epoch;

            Interlocked.Exchange(ref nextSceneEpoch, 1);
            return 1;
        }
    }
}
