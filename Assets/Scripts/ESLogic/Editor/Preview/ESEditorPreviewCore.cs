using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ES
{
    public enum ESEditorPreviewSceneMode
    {
        PreviewScene,
        HiddenObjectsInActiveScene
    }

    public enum ESEditorPreviewQuality
    {
        Fast,
        Balanced,
        High
    }

    public readonly struct ESEditorPreviewCameraPose
    {
        public readonly Vector3 Center;
        public readonly float Radius;
        public readonly float Yaw;
        public readonly float Pitch;
        public readonly float Zoom;

        public ESEditorPreviewCameraPose(Vector3 center, float radius, float yaw, float pitch, float zoom)
        {
            Center = center;
            Radius = Mathf.Max(0.05f, radius);
            Yaw = yaw;
            Pitch = pitch;
            Zoom = Mathf.Max(0.05f, zoom);
        }
    }

    public readonly struct ESEditorPreviewRenderOptions
    {
        public readonly ESEditorPreviewQuality Quality;
        public readonly float RenderScale;
        public readonly double MinRenderInterval;

        public ESEditorPreviewRenderOptions(ESEditorPreviewQuality quality, float renderScale, double minRenderInterval = 0d)
        {
            Quality = quality;
            RenderScale = Mathf.Clamp(renderScale, 0.5f, 4f);
            MinRenderInterval = Math.Max(0d, minRenderInterval);
        }

        public static ESEditorPreviewRenderOptions Fast => new ESEditorPreviewRenderOptions(ESEditorPreviewQuality.Fast, 1f, 1d / 15d);
        public static ESEditorPreviewRenderOptions Balanced => new ESEditorPreviewRenderOptions(ESEditorPreviewQuality.Balanced, 2f, 1d / 30d);
        public static ESEditorPreviewRenderOptions High => new ESEditorPreviewRenderOptions(ESEditorPreviewQuality.High, 3f, 0d);
    }

    public sealed class ESEditorPreviewModelHandle : IDisposable
    {
        private readonly ESEditorPreviewRenderContext ownerContext;
        private bool disposed;

        public GameObject Source { get; }
        public GameObject Instance { get; private set; }
        public Bounds Bounds { get; private set; }
        public Vector3 StableCenter { get; private set; }
        public float StableRadius { get; private set; }

        internal ESEditorPreviewModelHandle(ESEditorPreviewRenderContext ownerContext, GameObject source, GameObject instance)
        {
            this.ownerContext = ownerContext;
            Source = source;
            Instance = instance;
            RefreshBounds(lockStableView: true);
        }

        public T GetComponentInPreview<T>() where T : Component
        {
            return Instance != null ? Instance.GetComponentInChildren<T>(true) : null;
        }

        public Bounds RefreshBounds(bool lockStableView = false)
        {
            Bounds = ESEditorPreviewUtility.CalculateBounds(Instance);
            float radius = Mathf.Max(0.5f, Bounds.extents.magnitude);
            if (lockStableView || StableRadius <= 0f)
            {
                StableCenter = Bounds.center;
                StableRadius = radius;
            }

            return Bounds;
        }

        public ESEditorPreviewCameraPose GetCameraPose(float yaw, float pitch, float zoom, bool followAnimatedBounds)
        {
            Bounds bounds = RefreshBounds(lockStableView: false);
            Vector3 center = followAnimatedBounds ? bounds.center : StableCenter;
            float radius = followAnimatedBounds ? Mathf.Max(0.5f, bounds.extents.magnitude) : StableRadius;
            return new ESEditorPreviewCameraPose(center, radius, yaw, pitch, zoom);
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            ownerContext?.UnregisterModel(this);
            ESEditorPreviewUtility.DestroyObject(Instance);
            Instance = null;
        }

        internal void DisposeFromOwner()
        {
            if (disposed)
                return;

            disposed = true;
            ESEditorPreviewUtility.DestroyObject(Instance);
            Instance = null;
        }
    }

    /// <summary>
    /// 全局预览生命周期入口。窗口和预览模块可以注册 IDisposable，上下文重载、退出、切 PlayMode 时统一清理。
    /// </summary>
    public static class ESEditorPreviewLifecycleHub
    {
        private static readonly HashSet<IDisposable> ActiveScopes = new HashSet<IDisposable>();
        private static readonly List<IDisposable> DisposeBuffer = new List<IDisposable>(32);
        private static bool registered;

        public static void RegisterGlobalHooks()
        {
            if (registered)
            {
                AssemblyReloadEvents.beforeAssemblyReload -= CleanupBeforeAssemblyReload;
                EditorApplication.quitting -= CleanupBeforeEditorQuit;
                EditorApplication.playModeStateChanged -= CleanupOnPlayModeChanged;
            }

            registered = true;
            AssemblyReloadEvents.beforeAssemblyReload += CleanupBeforeAssemblyReload;
            EditorApplication.quitting += CleanupBeforeEditorQuit;
            EditorApplication.playModeStateChanged += CleanupOnPlayModeChanged;
        }

        public static void RegisterScope(IDisposable scope)
        {
            if (scope == null)
                return;

            RegisterGlobalHooks();
            ActiveScopes.Add(scope);
        }

        public static void UnregisterScope(IDisposable scope)
        {
            if (scope == null)
                return;

            ActiveScopes.Remove(scope);
        }

        public static int CleanupAll(string reason, bool includeMarkedObjects = true)
        {
            DisposeBuffer.Clear();
            DisposeBuffer.AddRange(ActiveScopes);
            ActiveScopes.Clear();

            int disposed = 0;
            for (int i = DisposeBuffer.Count - 1; i >= 0; i--)
            {
                try
                {
                    DisposeBuffer[i]?.Dispose();
                    disposed++;
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[ESEditorPreviewLifecycle] Dispose failed. reason=" + reason + " error=" + e.Message);
                }
            }
            DisposeBuffer.Clear();

            if (includeMarkedObjects)
                disposed += ESEditorPreviewUtility.CleanupAllMarkedPreviewObjects();

            return disposed;
        }

        [MenuItem(MenuItemPathDefine.PREVIEW_CLEANUP_PATH + "清理全部ES预览上下文", false, -20)]
        public static void CleanupAllMenu()
        {
            int removed = CleanupAll("Menu");
            Debug.Log("[ESEditorPreviewLifecycle] 已清理预览上下文和残留对象: " + removed);
        }

        private static void CleanupBeforeAssemblyReload()
        {
            CleanupAll("AssemblyReload");
        }

        private static void CleanupBeforeEditorQuit()
        {
            CleanupAll("EditorQuit");
        }

        private static void CleanupOnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.ExitingPlayMode)
                CleanupAll("PlayModeChanged");
        }
    }

    /// <summary>
    /// 可复用预览渲染上下文。负责隔离点、相机、灯光、RT、截图和生命周期。
    /// 业务只需要提供预览对象、中心点、半径和采样后的当前姿态。
    /// </summary>
    public sealed class ESEditorPreviewRenderContext : IDisposable
    {
        private const float GroupSpacing = 100f;
        private const float CameraFarClip = 80f;
        private const int MaxCellProbeAttempts = 4096;
        private static readonly object AllocationLock = new object();
        private static readonly HashSet<Vector2Int> OccupiedCells = new HashSet<Vector2Int>();
        private static readonly Queue<Vector2Int> ReleasedCells = new Queue<Vector2Int>();
        private static int nextAllocationId;

        private readonly string owner;
        private readonly ESEditorPreviewSceneMode sceneMode;
        private readonly int previewLayer;
        private readonly int allocationId;
        private readonly Vector2Int allocatedCell;
        private readonly Vector3 groupOrigin;
        private readonly string allocationReport;
        private Scene previewScene;
        private GameObject cameraObject;
        private GameObject keyLightObject;
        private GameObject fillLightObject;
        private readonly List<ESEditorPreviewModelHandle> modelHandles = new List<ESEditorPreviewModelHandle>(4);
        private RenderTexture renderTexture;
        private int renderTextureWidth;
        private int renderTextureHeight;
        private ESEditorPreviewQuality renderTextureQuality;
        private double lastRenderTime;
        private bool disposed;

        public Camera Camera { get; private set; }
        public Vector3 GroupOrigin => groupOrigin;
        public bool IsReady => Camera != null && (sceneMode != ESEditorPreviewSceneMode.PreviewScene || previewScene.IsValid());
        public string LastStatus { get; private set; } = "Preview context not created.";
        public string LastObjectFlowStatus { get; private set; } = "Preview object flow not requested.";
        public string IsolationReport => IsReady
            ? sceneMode + ", Layer=" + previewLayer + ", Origin=" + FormatVector(groupOrigin) + ", Cell=" + allocatedCell + ", FarClip=" + CameraFarClip.ToString("F0") + "m, " + allocationReport
            : "Preview render context not ready.";

        public ESEditorPreviewRenderContext(
            string owner,
            ESEditorPreviewSceneMode sceneMode = ESEditorPreviewSceneMode.HiddenObjectsInActiveScene,
            int previewLayer = ESEditorPreviewUtility.DefaultPreviewLayer)
        {
            this.owner = string.IsNullOrWhiteSpace(owner) ? "EditorPreview" : owner;
            this.sceneMode = sceneMode;
            this.previewLayer = Mathf.Clamp(previewLayer, 0, 31);
            allocationId = System.Threading.Interlocked.Increment(ref nextAllocationId);
            allocatedCell = AllocateCell(allocationId, out allocationReport);
            groupOrigin = new Vector3(allocatedCell.x * GroupSpacing, 0f, allocatedCell.y * GroupSpacing);
            ESEditorPreviewLifecycleHub.RegisterScope(this);
        }

        public void Ensure()
        {
            if (IsReady)
                return;

            EnsurePreviewScene();
            EnsureCamera();
            EnsureLights();
            LastStatus = "Preview render context ready.";
        }

        public void PreparePreviewObject(GameObject obj, string note, bool samplingTarget)
        {
            if (obj == null)
                return;

            Ensure();
            bool moved = MoveToContextScene(obj);
            HideFlags flags = samplingTarget ? ESEditorPreviewUtility.SamplingSafeHideFlags : ESEditorPreviewUtility.PreviewHideFlags;
            ESEditorPreviewUtility.SetHideFlagsRecursive(obj.transform, flags);
            ESEditorPreviewUtility.SetLayerRecursive(obj.transform, previewLayer);
            ESEditorPreviewUtility.TryMarkPreviewObject(obj, owner, note, out string markerStatus);
            LastObjectFlowStatus =
                "Object=" + obj.name
                + ", HideFlags=" + flags
                + ", SamplingTarget=" + samplingTarget
                + ", Scene=" + FormatScene(obj.scene)
                + ", Move=" + moved
                + ", Layer=" + previewLayer
                + ", Marker=" + markerStatus;
        }

        public ESEditorPreviewModelHandle CreateModelGroup(
            GameObject source,
            string instanceName = null,
            bool samplingTarget = true,
            bool copyRendererState = true,
            bool disableRuntimeBehaviours = true)
        {
            if (source == null)
                return null;

            Ensure();
            GameObject instance = UnityEngine.Object.Instantiate(source);
            instance.name = string.IsNullOrWhiteSpace(instanceName) ? source.name + "_ESPreview" : instanceName;
            instance.SetActive(true);
            NormalizeTransform(instance.transform);
            PreparePreviewObject(instance, "Preview model group.", samplingTarget);
            MoveToGroupOrigin(instance.transform);

            if (copyRendererState)
                ESEditorPreviewUtility.CopyRendererState(source, instance);
            if (disableRuntimeBehaviours)
                ESEditorPreviewUtility.DisableRuntimeBehaviours(instance);

            ESEditorPreviewUtility.EnsureRenderersEnabled(instance);
            var handle = new ESEditorPreviewModelHandle(this, source, instance);
            modelHandles.Add(handle);
            return handle;
        }

        public void DestroyAllModelGroups()
        {
            for (int i = modelHandles.Count - 1; i >= 0; i--)
                modelHandles[i]?.DisposeFromOwner();

            modelHandles.Clear();
        }

        public bool RenderGUI(Rect rect, ESEditorPreviewCameraPose pose, ESEditorPreviewRenderOptions options)
        {
            Ensure();
            if (Camera == null)
                return false;

            ApplyCameraPose(rect.width / Mathf.Max(1f, rect.height), pose, options.Quality);
            if (Event.current == null || Event.current.type != EventType.Repaint)
                return true;

            float scale = Mathf.Clamp(EditorGUIUtility.pixelsPerPoint * options.RenderScale, 0.5f, 4f);
            int width = Mathf.Max(1, Mathf.CeilToInt(rect.width * scale));
            int height = Mathf.Max(1, Mathf.CeilToInt(rect.height * scale));
            EnsureRenderTexture(width, height, options.Quality);
            if (renderTexture == null)
                return false;

            double now = EditorApplication.timeSinceStartup;
            if (options.MinRenderInterval > 0d && lastRenderTime > 0d && now - lastRenderTime < options.MinRenderInterval)
            {
                GUI.DrawTexture(rect, renderTexture, ScaleMode.StretchToFill, false);
                return true;
            }

            RenderTexture oldTarget = Camera.targetTexture;
            RenderTexture oldActive = RenderTexture.active;
            try
            {
                Camera.targetTexture = renderTexture;
                Camera.Render();
                lastRenderTime = now;
                GUI.DrawTexture(rect, renderTexture, ScaleMode.StretchToFill, false);
            }
            finally
            {
                Camera.targetTexture = oldTarget;
                RenderTexture.active = oldActive;
            }

            return true;
        }

        public Texture2D Snapshot(int width, int height, ESEditorPreviewCameraPose pose, ESEditorPreviewQuality quality, string textureName)
        {
            Ensure();
            if (Camera == null)
                return null;

            width = Mathf.Clamp(width, 64, 2048);
            height = Mathf.Clamp(height, 64, 2048);
            ApplyCameraPose(width / (float)Mathf.Max(1, height), pose, quality);
            EnsureRenderTexture(width, height, quality);
            return ESEditorPreviewUtility.RenderCameraSnapshot(Camera, renderTexture, width, height, textureName);
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            ESEditorPreviewLifecycleHub.UnregisterScope(this);
            DestroyAllModelGroups();
            ESEditorPreviewUtility.ReleaseRenderTexture(ref renderTexture);
            ESEditorPreviewUtility.DestroyObject(cameraObject);
            ESEditorPreviewUtility.DestroyObject(keyLightObject);
            ESEditorPreviewUtility.DestroyObject(fillLightObject);
            Camera = null;
            ReleaseCell(allocatedCell);

            if (previewScene.IsValid())
            {
                EditorSceneManager.ClosePreviewScene(previewScene);
                previewScene = default;
            }
        }

        private void EnsurePreviewScene()
        {
            if (sceneMode != ESEditorPreviewSceneMode.PreviewScene || previewScene.IsValid())
                return;

            previewScene = EditorSceneManager.NewPreviewScene();
        }

        internal void UnregisterModel(ESEditorPreviewModelHandle handle)
        {
            if (handle != null)
                modelHandles.Remove(handle);
        }

        private void EnsureCamera()
        {
            if (Camera != null)
                return;

            cameraObject = ESEditorPreviewUtility.CreatePreviewGameObject(owner + " Preview Camera", typeof(Camera));
            MoveToContextScene(cameraObject);
            ESEditorPreviewUtility.TryMarkPreviewObject(cameraObject, owner, "Preview camera.", out _);
            Camera = cameraObject.GetComponent<Camera>();
            Camera.enabled = false;
            Camera.fieldOfView = 30f;
            Camera.clearFlags = CameraClearFlags.Color;
            Camera.backgroundColor = new Color(0.06f, 0.065f, 0.075f, 1f);
            Camera.cullingMask = 1 << previewLayer;
            Camera.nearClipPlane = 0.01f;
            Camera.farClipPlane = CameraFarClip;
            TrySetCameraScene(Camera, previewScene);
            ESEditorPreviewUtility.TryConfigureUniversalCameraData(Camera);
        }

        private void EnsureLights()
        {
            if (keyLightObject == null)
                keyLightObject = CreateLight(owner + " Preview Key Light", 1.2f, Quaternion.Euler(35f, 35f, 0f));
            if (fillLightObject == null)
                fillLightObject = CreateLight(owner + " Preview Fill Light", 0.55f, Quaternion.Euler(340f, 210f, 0f));
        }

        private GameObject CreateLight(string name, float intensity, Quaternion rotation)
        {
            GameObject go = ESEditorPreviewUtility.CreatePreviewGameObject(name, typeof(Light));
            MoveToContextScene(go);
            ESEditorPreviewUtility.SetLayerRecursive(go.transform, previewLayer);
            ESEditorPreviewUtility.TryMarkPreviewObject(go, owner, "Preview light.", out _);
            Light light = go.GetComponent<Light>();
            light.type = sceneMode == ESEditorPreviewSceneMode.PreviewScene ? LightType.Directional : LightType.Spot;
            light.intensity = sceneMode == ESEditorPreviewSceneMode.PreviewScene ? intensity : intensity * 5f;
            light.range = 60f;
            light.spotAngle = 75f;
            light.cullingMask = 1 << previewLayer;
            light.transform.rotation = rotation;
            if (sceneMode != ESEditorPreviewSceneMode.PreviewScene)
                light.transform.position = groupOrigin - light.transform.forward * 18f + Vector3.up * 8f;
            return go;
        }

        private void ApplyCameraPose(float aspect, ESEditorPreviewCameraPose pose, ESEditorPreviewQuality quality)
        {
            Camera.aspect = Mathf.Max(0.25f, aspect);
            Quaternion orbit = Quaternion.Euler(pose.Pitch, pose.Yaw, 0f);
            float distance = Mathf.Max(1.2f, pose.Radius * 2.8f * pose.Zoom);
            Camera.transform.position = pose.Center + orbit * new Vector3(0f, pose.Radius * 0.18f, distance);
            Camera.transform.LookAt(pose.Center);
            Camera.farClipPlane = CameraFarClip;
            Camera.cullingMask = 1 << previewLayer;
            Camera.allowHDR = quality == ESEditorPreviewQuality.High;
            Camera.allowMSAA = quality != ESEditorPreviewQuality.Fast;
            TrySetCameraScene(Camera, previewScene);
        }

        private void EnsureRenderTexture(int width, int height, ESEditorPreviewQuality quality)
        {
            if (renderTexture != null && renderTextureWidth == width && renderTextureHeight == height && renderTextureQuality == quality)
                return;

            ESEditorPreviewUtility.ReleaseRenderTexture(ref renderTexture);
            renderTextureWidth = width;
            renderTextureHeight = height;
            renderTextureQuality = quality;
            renderTexture = ESEditorPreviewUtility.CreateRenderTexture(width, height, 24, GetAntiAliasing(quality), owner + " Preview RT");
        }

        private bool MoveToContextScene(GameObject obj)
        {
            if (obj == null)
                return false;

            try
            {
                if (sceneMode == ESEditorPreviewSceneMode.PreviewScene)
                {
                    EnsurePreviewScene();
                    SceneManager.MoveGameObjectToScene(obj, previewScene);
                    return obj.scene == previewScene;
                }

                Scene activeScene = SceneManager.GetActiveScene();
                if (activeScene.IsValid() && obj.scene != activeScene)
                    SceneManager.MoveGameObjectToScene(obj, activeScene);
                return obj.scene.IsValid();
            }
            catch
            {
                return obj.scene.IsValid();
            }
        }

        private void MoveToGroupOrigin(Transform root)
        {
            if (root == null)
                return;

            root.position = groupOrigin;
            root.rotation = Quaternion.identity;
            root.localScale = Vector3.one;
        }

        private static void NormalizeTransform(Transform transform)
        {
            if (transform == null)
                return;

            transform.position = Vector3.zero;
            transform.rotation = Quaternion.identity;
            transform.localScale = Vector3.one;
        }

        private static int GetAntiAliasing(ESEditorPreviewQuality quality)
        {
            switch (quality)
            {
                case ESEditorPreviewQuality.High:
                    return 8;
                case ESEditorPreviewQuality.Balanced:
                    return 4;
                default:
                    return 1;
            }
        }

        private static Vector2Int AllocateCell(int allocationId, out string report)
        {
            unchecked
            {
                int seed = allocationId * 73856093 ^ Environment.TickCount * 19349663 ^ Guid.NewGuid().GetHashCode();
                var random = new System.Random(seed);
                lock (AllocationLock)
                {
                    while (ReleasedCells.Count > 0)
                    {
                        Vector2Int reusable = ReleasedCells.Dequeue();
                        if (OccupiedCells.Contains(reusable))
                            continue;

                        OccupiedCells.Add(reusable);
                        report = "CellAlloc=reused, Free=" + ReleasedCells.Count + ", Occupied=" + OccupiedCells.Count;
                        return reusable;
                    }

                    for (int attempt = 0; attempt < MaxCellProbeAttempts; attempt++)
                    {
                        int hash = seed ^ (attempt * 83492791);
                        int ring = 1 + Mathf.Abs(hash % 128);
                        int x = ((hash >> 8) % (ring * 2 + 1)) - ring + random.Next(-2, 3);
                        int y = ((hash >> 20) % (ring * 2 + 1)) - ring + random.Next(-2, 3);
                        var candidate = new Vector2Int(x, y);
                        if (OccupiedCells.Contains(candidate))
                            continue;

                        OccupiedCells.Add(candidate);
                        report = "CellAlloc=hash-random, Attempt=" + attempt + ", Occupied=" + OccupiedCells.Count;
                        return candidate;
                    }
                }
            }

            report = "CellAlloc=fallback-zero";
            return Vector2Int.zero;
        }

        private static void ReleaseCell(Vector2Int cell)
        {
            lock (AllocationLock)
            {
                if (OccupiedCells.Remove(cell))
                    ReleasedCells.Enqueue(cell);
            }
        }

        private static bool TrySetCameraScene(Camera camera, Scene scene)
        {
            if (camera == null || !scene.IsValid())
                return false;

            PropertyInfo sceneProperty = typeof(Camera).GetProperty("scene", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (sceneProperty == null || !sceneProperty.CanWrite || sceneProperty.PropertyType != typeof(Scene))
                return false;

            sceneProperty.SetValue(camera, scene);
            return true;
        }

        private static string FormatScene(Scene scene)
        {
            if (!scene.IsValid())
                return "<invalid>";

            return string.IsNullOrEmpty(scene.name) ? "<untitled-active-scene>" : scene.name;
        }

        private static string FormatVector(Vector3 value)
        {
            return "(" + value.x.ToString("F1") + ", " + value.y.ToString("F1") + ", " + value.z.ToString("F1") + ")";
        }
    }

    public static class ESEditorPreviewPersistentFramePaths
    {
        private const string RootFolderName = "ESPreviewFrames";

        public static string RootFolder
        {
            get
            {
                string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
                return Path.Combine(projectRoot, "Library", RootFolderName);
            }
        }

        public static string GetFrameFolder(string workflow, string stableKey, string viewName)
        {
            workflow = SanitizePathPart(workflow, "General");
            stableKey = SanitizePathPart(stableKey, "Unknown");
            viewName = SanitizePathPart(viewName, "Default");
            return Path.Combine(RootFolder, workflow, stableKey, viewName);
        }

        public static string GetFramePath(string workflow, string stableKey, string viewName, int frameIndex)
        {
            return Path.Combine(GetFrameFolder(workflow, stableKey, viewName), "preview_" + Mathf.Max(1, frameIndex).ToString("000") + ".png");
        }

        public static void EnsureFrameFolder(string workflow, string stableKey, string viewName)
        {
            Directory.CreateDirectory(GetFrameFolder(workflow, stableKey, viewName));
        }

        private static string SanitizePathPart(string value, string fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
                value = fallback;

            char[] invalid = Path.GetInvalidFileNameChars();
            for (int i = 0; i < invalid.Length; i++)
                value = value.Replace(invalid[i], '_');

            return value.Trim();
        }
    }
}
