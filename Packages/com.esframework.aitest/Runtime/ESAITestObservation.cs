using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ESFramework.ESAITest
{
    [Serializable]
    public sealed class ESAITestArtifactDto
    {
        public string relativePath;
        public string kind;
        public long byteLength;
        public string sha256;
    }

    [Serializable]
    public sealed class ESAITestScreenCaptureDto
    {
        public string relativePath;
        public int width;
        public int height;
        public long byteLength;
        public string sha256;
        public long capturedUtcTicks;
        public int frameCount;
        public bool includesDashboard;
    }

    [Serializable]
    public sealed class ESAITestCameraObservationDto
    {
        public string path;
        public bool active;
        public bool mainCamera;
        public float depth;
        public float fieldOfView;
        public bool orthographic;
        public float orthographicSize;
        public float positionX;
        public float positionY;
        public float positionZ;
        public float rotationX;
        public float rotationY;
        public float rotationZ;
        public float viewportX;
        public float viewportY;
        public float viewportWidth;
        public float viewportHeight;
        public int cullingMask;
    }

    [Serializable]
    public sealed class ESAITestUIObservationDto
    {
        public string path;
        public string controlType;
        public string text;
        public string value;
        public bool active;
        public bool interactable;
        public bool selected;
        public float screenX;
        public float screenY;
        public float screenWidth;
        public float screenHeight;
    }

    [Serializable]
    public sealed class ESAITestSceneObjectObservationDto
    {
        public string path;
        public string tag;
        public int layer;
        public bool activeSelf;
        public bool activeInHierarchy;
        public bool rendererVisible;
        public float positionX;
        public float positionY;
        public float positionZ;
        public float screenX;
        public float screenY;
        public float screenDepth;
        public string[] componentTypes = Array.Empty<string>();
    }

    [Serializable]
    public sealed class ESAITestAttentionObservationDto
    {
        public string profile;
        public string reason;
        public bool promptInterrupted;
        public bool refreshedUi;
        public bool refreshedScene;
        public bool returnedRetainedMemory;
        public int retainedUiCount;
        public int retainedSceneObjectCount;
        public float uiSampleAgeSeconds;
        public float sceneSampleAgeSeconds;
        public float samplingCostMilliseconds;
        public int pendingPromptCount;
    }

    [Serializable]
    public sealed class ESAITestObservationSnapshotDto
    {
        public int protocolVersion = ESAITestProtocol.CurrentVersion;
        public string runId;
        public int sceneGeneration;
        public string command;
        public long observedUtcTicks;
        public int frameCount;
        public float realtimeSinceStartup;
        public float timeScale;
        public int screenWidth;
        public int screenHeight;
        public bool fullScreen;
        public float dpi;
        public string platform;
        public string activeScene;
        public int loadedSceneCount;
        public string selectedUiPath;
        public ESAITestAIPromptDto prompt;
        public ESAITestAttentionObservationDto attention;
        public ESAITestScreenCaptureDto latestScreenshot;
        public ESAITestCameraObservationDto[] cameras = Array.Empty<ESAITestCameraObservationDto>();
        public ESAITestUIObservationDto[] uiElements = Array.Empty<ESAITestUIObservationDto>();
        public ESAITestSceneObjectObservationDto[] sceneObjects = Array.Empty<ESAITestSceneObjectObservationDto>();
        public string[] warnings = Array.Empty<string>();
    }

    public static class ESAITestObservationRuntimeState
    {
        public static string LastCommand { get; internal set; } = string.Empty;
        public static long LastObservedUtcTicks { get; internal set; }
        public static int LastUiCount { get; internal set; }
        public static int LastSceneObjectCount { get; internal set; }
        public static string LastAttentionProfile { get; internal set; } = string.Empty;
        public static float LastSamplingCostMilliseconds { get; internal set; }
        public static ESAITestScreenCaptureDto LatestScreenshot { get; internal set; }
        public static ESAITestAIPromptDto LastConsumedPrompt { get; internal set; }

        internal static void Reset()
        {
            LastCommand = string.Empty;
            LastObservedUtcTicks = 0;
            LastUiCount = 0;
            LastSceneObjectCount = 0;
            LastAttentionProfile = string.Empty;
            LastSamplingCostMilliseconds = 0f;
            LatestScreenshot = null;
            LastConsumedPrompt = null;
        }
    }

    public static class ESAITestArtifactStore
    {
        private const string ArtifactRootName = ".artifacts";

        public static string WriteBytes(string runId, string relativePath, byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                throw new ArgumentException("Artifact 内容为空。", nameof(bytes));

            string normalized = NormalizeRelativePath(relativePath);
            string runDirectory = GetRunDirectory(runId);
            string destination = Path.GetFullPath(Path.Combine(runDirectory, normalized));
            EnsureWithin(destination, runDirectory);
            string parent = Path.GetDirectoryName(destination);
            if (!string.IsNullOrEmpty(parent))
                Directory.CreateDirectory(parent);

            string temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllBytes(temporary, bytes);
            if (File.Exists(destination))
                throw new IOException("Artifact 已存在，拒绝覆盖：" + normalized);
            File.Move(temporary, destination);
            return normalized.Replace('\\', '/');
        }

        public static ESAITestArtifactDto[] CreateManifest(string runId)
        {
            string runDirectory = GetRunDirectory(runId);
            if (!Directory.Exists(runDirectory))
                return Array.Empty<ESAITestArtifactDto>();

            string[] files = Directory.GetFiles(runDirectory, "*", SearchOption.AllDirectories);
            Array.Sort(files, StringComparer.Ordinal);
            var result = new ESAITestArtifactDto[files.Length];
            for (int i = 0; i < files.Length; i++)
            {
                string relative = files[i].Substring(runDirectory.Length)
                    .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .Replace('\\', '/');
                var info = new FileInfo(files[i]);
                result[i] = new ESAITestArtifactDto
                {
                    relativePath = "artifacts/" + relative,
                    kind = ClassifyArtifact(relative),
                    byteLength = info.Length,
                    sha256 = ComputeSha256(files[i]),
                };
            }
            return result;
        }

        public static void CopyIntoReport(string runId, string reportTemporaryDirectory)
        {
            string source = GetRunDirectory(runId);
            if (!Directory.Exists(source))
                return;

            string destination = Path.Combine(reportTemporaryDirectory, "artifacts");
            string[] directories = Directory.GetDirectories(source, "*", SearchOption.AllDirectories);
            for (int i = 0; i < directories.Length; i++)
            {
                string relative = directories[i].Substring(source.Length)
                    .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                Directory.CreateDirectory(Path.Combine(destination, relative));
            }

            Directory.CreateDirectory(destination);
            string[] files = Directory.GetFiles(source, "*", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string relative = files[i].Substring(source.Length)
                    .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string target = Path.Combine(destination, relative);
                string parent = Path.GetDirectoryName(target);
                if (!string.IsNullOrEmpty(parent))
                    Directory.CreateDirectory(parent);
                File.Copy(files[i], target, false);
            }
        }

        public static void ClearStaging(string runId)
        {
            string directory = GetRunDirectory(runId);
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }

        public static string ComputeSha256(byte[] bytes)
        {
            using (SHA256 sha256 = SHA256.Create())
                return ToHex(sha256.ComputeHash(bytes));
        }

        private static string ComputeSha256(string path)
        {
            using (FileStream stream = File.OpenRead(path))
            using (SHA256 sha256 = SHA256.Create())
                return ToHex(sha256.ComputeHash(stream));
        }

        private static string GetRunDirectory(string runId)
        {
            string segment = SanitizeSegment(runId);
            return Path.GetFullPath(Path.Combine(Application.persistentDataPath, "ESAITest", ArtifactRootName, segment));
        }

        private static string NormalizeRelativePath(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
                throw new ArgumentException("Artifact 路径必须是非空相对路径。", nameof(relativePath));
            string normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
            string[] segments = normalized.Split(Path.DirectorySeparatorChar);
            for (int i = 0; i < segments.Length; i++)
                if (string.IsNullOrWhiteSpace(segments[i]) || segments[i] == "." || segments[i] == "..")
                    throw new ArgumentException("Artifact 路径包含非法段。", nameof(relativePath));
            return normalized;
        }

        private static void EnsureWithin(string path, string root)
        {
            string prefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                throw new IOException("Artifact 路径越过 Run 目录边界。");
        }

        private static string SanitizeSegment(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("runId 必填。", nameof(value));
            char[] characters = value.ToCharArray();
            char[] invalid = Path.GetInvalidFileNameChars();
            for (int i = 0; i < characters.Length; i++)
                if (Array.IndexOf(invalid, characters[i]) >= 0)
                    characters[i] = '_';
            string result = new string(characters);
            if (result == "." || result == "..")
                throw new ArgumentException("runId 不能作为安全目录名。", nameof(value));
            return result;
        }

        private static string ClassifyArtifact(string relativePath)
        {
            string extension = Path.GetExtension(relativePath);
            if (string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase)) return "screenshot";
            if (string.Equals(extension, ".json", StringComparison.OrdinalIgnoreCase)) return "json";
            return "artifact";
        }

        private static string ToHex(byte[] bytes)
        {
            var builder = new StringBuilder(bytes.Length * 2);
            for (int i = 0; i < bytes.Length; i++)
                builder.Append(bytes[i].ToString("x2"));
            return builder.ToString();
        }
    }

    [DisallowMultipleComponent]
    public sealed class ESAITestObservationCapabilityProvider : MonoBehaviour, ESAITestCapabilityProvider
    {
        private const string Capability = "unity.observe";
        private const int DefaultMaxUi = 128;
        private const int DefaultMaxObjects = 256;
        private const int DefaultMaxDepth = 6;
        private const int DefaultMaxText = 256;
        private const int DefaultAttentionMaxUi = 48;
        private const int DefaultAttentionMaxObjects = 96;
        private const int DefaultAttentionMaxDepth = 4;
        private const float DefaultAttentionUiInterval = 0.25f;
        private const float DefaultAttentionSceneInterval = 1f;
        private const float CameraSnapshotCacheSeconds = 0.1f;
        private readonly Vector3[] corners = new Vector3[4];
        private ESAITestScreenCaptureState captureState;
        private ESAITestUIObservationDto[] retainedUi = Array.Empty<ESAITestUIObservationDto>();
        private ESAITestSceneObjectObservationDto[] retainedScene = Array.Empty<ESAITestSceneObjectObservationDto>();
        private float retainedUiAt = float.NegativeInfinity;
        private float retainedSceneAt = float.NegativeInfinity;
        private int retainedSceneGeneration = -1;
        private string retainedSelectedUiPath = string.Empty;
        private ESAITestCameraObservationDto[] retainedCameras = Array.Empty<ESAITestCameraObservationDto>();
        private float retainedCamerasAt = float.NegativeInfinity;
        private int retainedCameraSceneGeneration = -1;

        public string CapabilityId => Capability;
        public string ProviderId => "esframework.aitest.observation";
        public int ProviderVersion => 1;
        public string[] Commands => new[]
        {
            "attention.snapshot", "prompt.next", "screen.capture", "screen.latest",
            "ui.snapshot", "scene.snapshot", "runtime.snapshot", "snapshot.full"
        };

        private void OnEnable()
        {
            ESAITestRuntime.Activated += Register;
            ESAITestRuntime.SceneGenerationChanged += Register;
            ESAITestRuntime.Deactivated += HandleDeactivated;
            Register();
        }

        private void OnDisable()
        {
            ESAITestRuntime.Activated -= Register;
            ESAITestRuntime.SceneGenerationChanged -= Register;
            ESAITestRuntime.Deactivated -= HandleDeactivated;
            ESAITestRuntime.Registry?.Unregister(this);
        }

        public ESAITestCapabilityResponseDto Execute(ESAITestCapabilityRequestDto request)
        {
            if (request == null || !string.Equals(request.operation, ESAITestProtocol.OperationSee, StringComparison.OrdinalIgnoreCase))
                return ESAITestCapabilityResponseDto.Reject(ESAITestStatusCode.CapabilityRejected, "Observation 仅允许 see 操作。");

            switch (request.command)
            {
                case "attention.snapshot": return ReturnSnapshot(BuildAttentionSnapshot(request));
                case "prompt.next": return ReturnPrompt(request);
                case "screen.capture": return CaptureScreen(request);
                case "screen.latest": return ReturnSnapshot(BuildSnapshot(request, false, false));
                case "ui.snapshot": return ReturnSnapshot(BuildSnapshot(request, true, false));
                case "scene.snapshot": return ReturnSnapshot(BuildSnapshot(request, false, true));
                case "runtime.snapshot": return ReturnSnapshot(BuildSnapshot(request, false, false));
                case "snapshot.full": return ReturnSnapshot(BuildSnapshot(request, true, true));
                default:
                    return ESAITestCapabilityResponseDto.Reject(ESAITestStatusCode.CapabilityRejected, "未知 Observation 命令：" + request.command);
            }
        }

        private ESAITestCapabilityResponseDto ReturnPrompt(ESAITestCapabilityRequestDto request)
        {
            ESAITestObservationSnapshotDto snapshot = BuildSnapshot(request, false, false);
            ESAITestAIPrompt.TryConsume(out snapshot.prompt);
            if (snapshot.prompt != null)
                ESAITestObservationRuntimeState.LastConsumedPrompt = snapshot.prompt;
            snapshot.attention = new ESAITestAttentionObservationDto
            {
                profile = "prompt",
                reason = snapshot.prompt == null ? "当前没有待消费的一次性 AI 提示。" : "已按 P 等级和同级 FIFO 消费一条提示。",
                promptInterrupted = snapshot.prompt != null,
                retainedUiCount = retainedUi.Length,
                retainedSceneObjectCount = retainedScene.Length,
                uiSampleAgeSeconds = SampleAge(retainedUiAt),
                sceneSampleAgeSeconds = SampleAge(retainedSceneAt),
                pendingPromptCount = ESAITestAIPrompt.PendingCount,
            };
            ESAITestObservationRuntimeState.LastAttentionProfile = snapshot.attention.profile;
            ESAITestObservationRuntimeState.LastUiCount = retainedUi.Length;
            ESAITestObservationRuntimeState.LastSceneObjectCount = retainedScene.Length;
            return ReturnSnapshot(snapshot);
        }

        private ESAITestObservationSnapshotDto BuildAttentionSnapshot(ESAITestCapabilityRequestDto request)
        {
            long samplingStarted = System.Diagnostics.Stopwatch.GetTimestamp();
            ESAITestObservationSnapshotDto snapshot = BuildSnapshot(request, false, false);
            ESAITestAIPrompt.TryConsume(out snapshot.prompt);
            if (snapshot.prompt != null)
                ESAITestObservationRuntimeState.LastConsumedPrompt = snapshot.prompt;

            string profile = FindArgument(request.arguments, "attention") ?? "adaptive";
            profile = profile.Trim().ToLowerInvariant();
            if (profile != "minimal" && profile != "focused" && profile != "context" && profile != "adaptive")
                profile = "adaptive";

            float now = Time.realtimeSinceStartup;
            float uiInterval = Mathf.Clamp(ReadFloat(request.arguments, "uiIntervalSeconds", DefaultAttentionUiInterval), 0.1f, 10f);
            float sceneInterval = Mathf.Clamp(ReadFloat(request.arguments, "sceneIntervalSeconds", DefaultAttentionSceneInterval), 0.25f, 30f);
            bool forceRefresh = ReadBool(request.arguments, "forceRefresh", false);
            bool returnRetainedMemory = ReadBool(request.arguments, "returnRetainedMemory", false);
            bool hasTarget = !string.IsNullOrWhiteSpace(request.target);
            bool urgentPrompt = snapshot.prompt != null
                && (snapshot.prompt.priority == ESAITestAIPromptPriority.P0.ToString()
                    || snapshot.prompt.priority == ESAITestAIPromptPriority.P1.ToString());
            bool sceneChanged = retainedSceneGeneration != request.sceneGeneration;
            bool selectedChanged = !string.Equals(retainedSelectedUiPath, snapshot.selectedUiPath, StringComparison.Ordinal);
            bool uiDue = retainedUi.Length == 0 || now - retainedUiAt >= uiInterval || selectedChanged;
            bool sceneDue = retainedScene.Length == 0 || now - retainedSceneAt >= sceneInterval || sceneChanged;

            bool refreshUi = false;
            bool refreshScene = false;
            switch (profile)
            {
                case "minimal":
                    break;
                case "focused":
                    refreshUi = uiDue;
                    refreshScene = hasTarget && sceneDue;
                    break;
                case "context":
                    refreshUi = uiDue;
                    refreshScene = sceneDue;
                    break;
                default:
                    refreshUi = uiDue;
                    refreshScene = sceneDue;
                    break;
            }

            if (forceRefresh)
            {
                // forceRefresh 是诊断/恢复语义，必须无视 minimal 的低频策略并刷新全部缓存。
                refreshUi = true;
                refreshScene = true;
            }
            else if (hasTarget || urgentPrompt)
            {
                refreshUi = profile != "minimal" || urgentPrompt;
                refreshScene = profile == "context" || profile == "adaptive" || hasTarget || urgentPrompt;
            }

            List<string> warnings = null;
            int maxText = Mathf.Clamp(ReadInt(request.arguments, "maxTextLength", DefaultMaxText), 32, 2048);
            if (refreshUi)
            {
                if (warnings == null)
                    warnings = new List<string>(4);
                retainedUi = CollectUi(request, maxText, warnings, DefaultAttentionMaxUi);
                retainedUiAt = now;
                retainedSelectedUiPath = snapshot.selectedUiPath;
                snapshot.uiElements = retainedUi;
            }
            else if (returnRetainedMemory)
            {
                snapshot.uiElements = retainedUi;
            }

            if (refreshScene)
            {
                if (warnings == null)
                    warnings = new List<string>(4);
                retainedScene = CollectScene(
                    request,
                    warnings,
                    DefaultAttentionMaxObjects,
                    DefaultAttentionMaxDepth,
                    false);
                retainedSceneAt = now;
                retainedSceneGeneration = request.sceneGeneration;
                snapshot.sceneObjects = retainedScene;
            }
            else if (returnRetainedMemory)
            {
                snapshot.sceneObjects = retainedScene;
            }

            snapshot.warnings = warnings == null ? Array.Empty<string>() : warnings.ToArray();
            snapshot.attention = new ESAITestAttentionObservationDto
            {
                profile = profile,
                reason = BuildAttentionReason(snapshot.prompt, forceRefresh, hasTarget, selectedChanged, sceneChanged),
                promptInterrupted = urgentPrompt,
                refreshedUi = refreshUi,
                refreshedScene = refreshScene,
                returnedRetainedMemory = returnRetainedMemory,
                retainedUiCount = retainedUi.Length,
                retainedSceneObjectCount = retainedScene.Length,
                uiSampleAgeSeconds = SampleAge(retainedUiAt),
                sceneSampleAgeSeconds = SampleAge(retainedSceneAt),
                samplingCostMilliseconds = (float)((System.Diagnostics.Stopwatch.GetTimestamp() - samplingStarted)
                    * 1000d / System.Diagnostics.Stopwatch.Frequency),
                pendingPromptCount = ESAITestAIPrompt.PendingCount,
            };

            ESAITestObservationRuntimeState.LastAttentionProfile = profile;
            ESAITestObservationRuntimeState.LastUiCount = retainedUi.Length;
            ESAITestObservationRuntimeState.LastSceneObjectCount = retainedScene.Length;
            ESAITestObservationRuntimeState.LastSamplingCostMilliseconds = snapshot.attention.samplingCostMilliseconds;
            return snapshot;
        }

        private ESAITestCapabilityResponseDto CaptureScreen(ESAITestCapabilityRequestDto request)
        {
            if (captureState != null && !string.Equals(captureState.stepId, request.stepId, StringComparison.Ordinal))
            {
                if (!captureState.complete)
                    return ESAITestCapabilityResponseDto.Reject(ESAITestStatusCode.RuntimeBusy, "已有屏幕捕获正在等待帧末完成。");
                captureState = null;
            }

            if (captureState == null)
            {
                captureState = new ESAITestScreenCaptureState
                {
                    stepId = request.stepId ?? string.Empty,
                    includeDashboard = ReadBool(request.arguments, "includeDashboard", false),
                    superSize = Mathf.Clamp(ReadInt(request.arguments, "superSize", 1), 1, 4),
                };
                StartCoroutine(CaptureAtEndOfFrame(captureState, request.runId));
            }

            if (!captureState.complete)
                return Pending("屏幕捕获已排队，等待当前帧完成。");
            if (!string.IsNullOrEmpty(captureState.error))
                return ESAITestCapabilityResponseDto.Reject(ESAITestStatusCode.InternalError, captureState.error);

            ESAITestObservationSnapshotDto snapshot = BuildSnapshot(request, true, false);
            snapshot.latestScreenshot = captureState.result;
            return ReturnSnapshot(snapshot);
        }

        private IEnumerator CaptureAtEndOfFrame(ESAITestScreenCaptureState state, string runId)
        {
            ESAITestRuntimeDashboard[] dashboards = Array.Empty<ESAITestRuntimeDashboard>();
            try
            {
                if (!state.includeDashboard)
                {
                    dashboards = FindObjectsOfType<ESAITestRuntimeDashboard>();
                    for (int i = 0; i < dashboards.Length; i++)
                        dashboards[i].SetPresentationVisible(false);
                }

                yield return new WaitForEndOfFrame();
                try
                {
                    Texture2D texture = ScreenCapture.CaptureScreenshotAsTexture(state.superSize);
                    if (texture == null)
                        throw new InvalidOperationException("ScreenCapture 未返回纹理；当前 Player/图形后端可能不支持屏幕捕获。");

                    byte[] png;
                    int width = texture.width;
                    int height = texture.height;
                    try
                    {
                        png = texture.EncodeToPNG();
                    }
                    finally
                    {
                        Destroy(texture);
                    }

                    string fileName = DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ") + "-"
                        + SanitizeFileSegment(state.stepId) + ".png";
                    string relative = ESAITestArtifactStore.WriteBytes(runId, "screens/" + fileName, png);
                    state.result = new ESAITestScreenCaptureDto
                    {
                        relativePath = "artifacts/" + relative,
                        width = width,
                        height = height,
                        byteLength = png.LongLength,
                        sha256 = ESAITestArtifactStore.ComputeSha256(png),
                        capturedUtcTicks = DateTime.UtcNow.Ticks,
                        frameCount = Time.frameCount,
                        includesDashboard = state.includeDashboard,
                    };
                    ESAITestObservationRuntimeState.LatestScreenshot = state.result;
                }
                catch (Exception exception)
                {
                    state.error = exception.ToString();
                }
            }
            finally
            {
                for (int i = 0; i < dashboards.Length; i++)
                    if (dashboards[i] != null)
                        dashboards[i].SetPresentationVisible(true);
                state.complete = true;
            }
        }

        private ESAITestObservationSnapshotDto BuildSnapshot(
            ESAITestCapabilityRequestDto request,
            bool includeUi,
            bool includeScene)
        {
            int maxText = Mathf.Clamp(ReadInt(request.arguments, "maxTextLength", DefaultMaxText), 32, 2048);
            List<string> warnings = includeUi || includeScene ? new List<string>(4) : null;
            var snapshot = new ESAITestObservationSnapshotDto
            {
                runId = request.runId,
                sceneGeneration = request.sceneGeneration,
                command = request.command,
                observedUtcTicks = DateTime.UtcNow.Ticks,
                frameCount = Time.frameCount,
                realtimeSinceStartup = Time.realtimeSinceStartup,
                timeScale = Time.timeScale,
                screenWidth = Screen.width,
                screenHeight = Screen.height,
                fullScreen = Screen.fullScreen,
                dpi = Screen.dpi,
                platform = Application.platform.ToString(),
                activeScene = SceneManager.GetActiveScene().name,
                loadedSceneCount = SceneManager.sceneCount,
                selectedUiPath = EventSystem.current?.currentSelectedGameObject == null
                    ? string.Empty
                    : BuildPath(EventSystem.current.currentSelectedGameObject.transform),
                latestScreenshot = ESAITestObservationRuntimeState.LatestScreenshot,
                cameras = GetCachedCameras(request.sceneGeneration),
            };

            if (includeUi)
                snapshot.uiElements = CollectUi(request, maxText, warnings, DefaultMaxUi);
            if (includeScene)
                snapshot.sceneObjects = CollectScene(request, warnings, DefaultMaxObjects, DefaultMaxDepth, true);
            snapshot.warnings = warnings == null ? Array.Empty<string>() : warnings.ToArray();

            ESAITestObservationRuntimeState.LastCommand = request.command;
            ESAITestObservationRuntimeState.LastObservedUtcTicks = snapshot.observedUtcTicks;
            ESAITestObservationRuntimeState.LastUiCount = snapshot.uiElements.Length;
            ESAITestObservationRuntimeState.LastSceneObjectCount = snapshot.sceneObjects.Length;
            ESAITestObservationRuntimeState.LastSamplingCostMilliseconds = 0f;
            return snapshot;
        }

        private ESAITestCameraObservationDto[] GetCachedCameras(int sceneGeneration)
        {
            float now = Time.realtimeSinceStartup;
            if (retainedCameraSceneGeneration == sceneGeneration
                && now - retainedCamerasAt < CameraSnapshotCacheSeconds)
                return retainedCameras;

            retainedCameras = CollectCameras();
            retainedCamerasAt = now;
            retainedCameraSceneGeneration = sceneGeneration;
            return retainedCameras;
        }

        private ESAITestCameraObservationDto[] CollectCameras()
        {
            Camera[] values = Camera.allCameras;
            var ordered = new ESAITestCameraPath[values.Length];
            for (int i = 0; i < values.Length; i++)
                ordered[i] = new ESAITestCameraPath(values[i], BuildPath(values[i].transform));
            Array.Sort(ordered, (left, right) => string.CompareOrdinal(left.path, right.path));
            int count = Mathf.Min(ordered.Length, 16);
            var result = new ESAITestCameraObservationDto[count];
            for (int i = 0; i < count; i++)
            {
                Camera camera = ordered[i].camera;
                Vector3 position = camera.transform.position;
                Vector3 rotation = camera.transform.eulerAngles;
                Rect rect = camera.rect;
                result[i] = new ESAITestCameraObservationDto
                {
                    path = ordered[i].path,
                    active = camera.isActiveAndEnabled,
                    mainCamera = camera.CompareTag("MainCamera"),
                    depth = camera.depth,
                    fieldOfView = camera.fieldOfView,
                    orthographic = camera.orthographic,
                    orthographicSize = camera.orthographicSize,
                    positionX = position.x,
                    positionY = position.y,
                    positionZ = position.z,
                    rotationX = rotation.x,
                    rotationY = rotation.y,
                    rotationZ = rotation.z,
                    viewportX = rect.x,
                    viewportY = rect.y,
                    viewportWidth = rect.width,
                    viewportHeight = rect.height,
                    cullingMask = camera.cullingMask,
                };
            }
            return result;
        }

        private ESAITestUIObservationDto[] CollectUi(
            ESAITestCapabilityRequestDto request,
            int maxText,
            ICollection<string> warnings,
            int defaultLimit)
        {
            int limit = Mathf.Clamp(ReadInt(request.arguments, "maxUi", defaultLimit), 1, 512);
            bool includeInfrastructure = ReadBool(request.arguments, "includeInfrastructure", false);
            string filter = request.target ?? string.Empty;
            Selectable[] selectables = FindObjectsOfType<Selectable>();
            var ordered = new List<ESAITestSelectablePath>(selectables.Length);
            for (int i = 0; i < selectables.Length; i++)
            {
                Selectable selectable = selectables[i];
                if (selectable == null || (!includeInfrastructure && selectable.GetComponentInParent<ESAITestRuntimeDashboard>() != null))
                    continue;
                string path = BuildPath(selectable.transform);
                if (MatchesFilter(path, filter))
                    ordered.Add(new ESAITestSelectablePath(selectable, path));
            }
            ordered.Sort((left, right) => string.CompareOrdinal(left.path, right.path));

            if (ordered.Count > limit)
                warnings.Add("UI 元素达到上限：" + limit + "/" + ordered.Count);
            int count = Mathf.Min(ordered.Count, limit);
            var result = new ESAITestUIObservationDto[count];
            for (int i = 0; i < count; i++)
            {
                Selectable selectable = ordered[i].selectable;
                Rect rect = GetScreenRect(selectable.transform as RectTransform);
                result[i] = new ESAITestUIObservationDto
                {
                    path = ordered[i].path,
                    controlType = selectable.GetType().Name,
                    text = Truncate(ReadControlText(selectable), maxText),
                    value = Truncate(ReadControlValue(selectable), maxText),
                    active = selectable.isActiveAndEnabled,
                    interactable = selectable.IsInteractable(),
                    selected = EventSystem.current?.currentSelectedGameObject == selectable.gameObject,
                    screenX = rect.x,
                    screenY = rect.y,
                    screenWidth = rect.width,
                    screenHeight = rect.height,
                };
            }
            return result;
        }

        private ESAITestSceneObjectObservationDto[] CollectScene(
            ESAITestCapabilityRequestDto request,
            ICollection<string> warnings,
            int defaultLimit,
            int defaultMaxDepth,
            bool defaultIncludeComponents)
        {
            int limit = Mathf.Clamp(ReadInt(request.arguments, "maxObjects", defaultLimit), 1, 1024);
            int maxDepth = Mathf.Clamp(ReadInt(request.arguments, "maxDepth", defaultMaxDepth), 0, 16);
            bool includeComponents = ReadBool(request.arguments, "includeComponents", defaultIncludeComponents);
            bool includeInfrastructure = ReadBool(request.arguments, "includeInfrastructure", false);
            string filter = request.target ?? string.Empty;
            Scene scene = SceneManager.GetActiveScene();
            GameObject[] roots = scene.GetRootGameObjects();
            Array.Sort(roots, (left, right) => string.CompareOrdinal(left.name, right.name));
            var queue = new Queue<ESAITestSceneNode>();
            for (int i = 0; i < roots.Length; i++)
                queue.Enqueue(new ESAITestSceneNode(roots[i].transform, 0));

            Camera camera = null;
            Camera[] cameras = Camera.allCameras;
            for (int i = 0; i < cameras.Length; i++)
                if (cameras[i] != null && cameras[i].isActiveAndEnabled && (camera == null || cameras[i].depth > camera.depth))
                    camera = cameras[i];
            var result = new List<ESAITestSceneObjectObservationDto>(Mathf.Min(limit, 256));
            int matchedCount = 0;
            while (queue.Count > 0)
            {
                ESAITestSceneNode node = queue.Dequeue();
                Transform transformValue = node.transform;
                if (transformValue == null)
                    continue;

                if (node.depth < maxDepth)
                    for (int i = 0; i < transformValue.childCount; i++)
                        queue.Enqueue(new ESAITestSceneNode(transformValue.GetChild(i), node.depth + 1));

                GameObject gameObject = transformValue.gameObject;
                if (!includeInfrastructure && IsInfrastructure(gameObject))
                    continue;
                string path = BuildPath(transformValue);
                if (!MatchesFilter(path, filter))
                    continue;

                matchedCount++;
                if (result.Count >= limit)
                    continue;

                Vector3 position = transformValue.position;
                Vector3 screen = camera == null ? Vector3.zero : camera.WorldToScreenPoint(position);
                Renderer renderer = gameObject.GetComponent<Renderer>();
                result.Add(new ESAITestSceneObjectObservationDto
                {
                    path = path,
                    tag = SafeTag(gameObject),
                    layer = gameObject.layer,
                    activeSelf = gameObject.activeSelf,
                    activeInHierarchy = gameObject.activeInHierarchy,
                    rendererVisible = renderer != null && renderer.isVisible,
                    positionX = position.x,
                    positionY = position.y,
                    positionZ = position.z,
                    screenX = screen.x,
                    screenY = screen.y,
                    screenDepth = screen.z,
                    componentTypes = includeComponents ? ReadComponentTypes(gameObject) : Array.Empty<string>(),
                });
            }

            if (matchedCount > limit)
                warnings.Add("场景对象达到上限：" + limit + "/" + matchedCount);
            return result.ToArray();
        }

        private Rect GetScreenRect(RectTransform rectTransform)
        {
            if (rectTransform == null)
                return Rect.zero;
            rectTransform.GetWorldCorners(corners);
            Canvas canvas = rectTransform.GetComponentInParent<Canvas>();
            Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            Vector2 bottomLeft = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            Vector2 topRight = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
            return Rect.MinMaxRect(bottomLeft.x, bottomLeft.y, topRight.x, topRight.y);
        }

        private void Register()
        {
            if (!isActiveAndEnabled || !ESAITestRuntime.IsActive || ESAITestRuntime.Registry == null)
                return;
            if (!ESAITestRuntime.Registry.Register(this, ESAITestRuntime.RunId, ESAITestRuntime.SceneGeneration, out string error))
                Debug.LogError("[ESAITest] Observation Capability 注册失败：" + error, this);
        }

        private void HandleDeactivated()
        {
            StopAllCoroutines();
            captureState = null;
            retainedUi = Array.Empty<ESAITestUIObservationDto>();
            retainedScene = Array.Empty<ESAITestSceneObjectObservationDto>();
            retainedUiAt = float.NegativeInfinity;
            retainedSceneAt = float.NegativeInfinity;
            retainedSceneGeneration = -1;
            retainedSelectedUiPath = string.Empty;
            retainedCameras = Array.Empty<ESAITestCameraObservationDto>();
            retainedCamerasAt = float.NegativeInfinity;
            retainedCameraSceneGeneration = -1;
            ESAITestObservationRuntimeState.Reset();
        }

        private static ESAITestCapabilityResponseDto ReturnSnapshot(ESAITestObservationSnapshotDto snapshot)
        {
            return new ESAITestCapabilityResponseDto
            {
                accepted = true,
                conditionMet = true,
                retryable = false,
                statusCode = ESAITestStatusCode.Passed,
                message = "Observation 已采集：UI=" + snapshot.uiElements.Length
                    + "，Scene=" + snapshot.sceneObjects.Length
                    + "，Camera=" + snapshot.cameras.Length
                    + (snapshot.attention == null ? string.Empty : "，Attention=" + snapshot.attention.profile)
                    + (snapshot.prompt == null ? string.Empty : "，Prompt=" + snapshot.prompt.priority)
                    + (snapshot.latestScreenshot == null ? string.Empty : "，Screenshot=" + snapshot.latestScreenshot.relativePath),
                value = ESAITestValueDto.FromString(JsonUtility.ToJson(snapshot)),
            };
        }

        private static ESAITestCapabilityResponseDto Pending(string message)
        {
            return new ESAITestCapabilityResponseDto
            {
                accepted = true,
                conditionMet = false,
                retryable = true,
                statusCode = ESAITestStatusCode.VerificationFailed,
                message = message,
            };
        }

        private static string ReadControlText(Selectable selectable)
        {
            if (selectable is InputField inputField) return inputField.text;
            if (selectable is Dropdown dropdown) return dropdown.captionText == null ? string.Empty : dropdown.captionText.text;
            Text text = selectable.GetComponentInChildren<Text>(true);
            return text == null ? string.Empty : text.text;
        }

        private static string ReadControlValue(Selectable selectable)
        {
            if (selectable is Toggle toggle) return toggle.isOn.ToString();
            if (selectable is Slider slider) return slider.value.ToString("R");
            if (selectable is Scrollbar scrollbar) return scrollbar.value.ToString("R");
            if (selectable is Dropdown dropdown) return dropdown.value.ToString();
            if (selectable is InputField inputField) return inputField.text;
            return string.Empty;
        }

        private static string[] ReadComponentTypes(GameObject gameObject)
        {
            Component[] components = gameObject.GetComponents<Component>();
            int count = Mathf.Min(components.Length, 24);
            var result = new string[count];
            for (int i = 0; i < count; i++)
                result[i] = components[i] == null ? "<MissingScript>" : components[i].GetType().FullName;
            return result;
        }

        private static bool IsInfrastructure(GameObject gameObject)
        {
            Transform current = gameObject.transform;
            while (current != null)
            {
                if (current.name.StartsWith("ESAITest", StringComparison.Ordinal))
                    return true;
                current = current.parent;
            }
            return false;
        }

        private static string SafeTag(GameObject gameObject)
        {
            try { return gameObject.tag; }
            catch { return string.Empty; }
        }

        private static string BuildPath(Transform transformValue)
        {
            if (transformValue == null)
                return string.Empty;
            var parts = new List<string>(8);
            Transform current = transformValue;
            while (current != null)
            {
                parts.Add(current.name);
                current = current.parent;
            }
            parts.Reverse();
            return string.Join("/", parts);
        }

        private static bool MatchesFilter(string path, string filter)
        {
            return string.IsNullOrWhiteSpace(filter)
                || (path ?? string.Empty).IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string FindArgument(ESAITestArgumentDto[] arguments, string key)
        {
            if (arguments == null) return null;
            for (int i = 0; i < arguments.Length; i++)
                if (arguments[i] != null && string.Equals(arguments[i].key, key, StringComparison.OrdinalIgnoreCase))
                    return arguments[i].value;
            return null;
        }

        private static int ReadInt(ESAITestArgumentDto[] arguments, string key, int fallback)
        {
            return int.TryParse(FindArgument(arguments, key), out int value) ? value : fallback;
        }

        private static float ReadFloat(ESAITestArgumentDto[] arguments, string key, float fallback)
        {
            return float.TryParse(
                FindArgument(arguments, key),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out float value)
                ? value
                : fallback;
        }

        private static bool ReadBool(ESAITestArgumentDto[] arguments, string key, bool fallback)
        {
            return bool.TryParse(FindArgument(arguments, key), out bool value) ? value : fallback;
        }

        private static string Truncate(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
                return value ?? string.Empty;
            return value.Substring(0, maxLength) + "…";
        }

        private static string SanitizeFileSegment(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "capture";
            char[] characters = value.ToCharArray();
            char[] invalid = Path.GetInvalidFileNameChars();
            for (int i = 0; i < characters.Length; i++)
                if (Array.IndexOf(invalid, characters[i]) >= 0)
                    characters[i] = '_';
            return new string(characters);
        }

        private static float SampleAge(float sampledAt)
        {
            return float.IsNegativeInfinity(sampledAt)
                ? -1f
                : Mathf.Max(0f, Time.realtimeSinceStartup - sampledAt);
        }

        private static string BuildAttentionReason(
            ESAITestAIPromptDto prompt,
            bool forceRefresh,
            bool hasTarget,
            bool selectedChanged,
            bool sceneChanged)
        {
            if (prompt != null && (prompt.priority == "P0" || prompt.priority == "P1"))
                return "高优先级一次性提示触发注意力中断和上下文刷新。";
            if (forceRefresh)
                return "调用方要求立即刷新注意力样本。";
            if (hasTarget)
                return "按目标路径聚焦采样。";
            if (sceneChanged)
                return "场景代际变化，刷新环境记忆。";
            if (selectedChanged)
                return "UI 焦点变化，刷新交互注意力。";
            return "按 UI/Scene 独立冷却与预算进行增量采样。";
        }

        private sealed class ESAITestScreenCaptureState
        {
            public string stepId;
            public int superSize;
            public bool includeDashboard;
            public bool complete;
            public string error;
            public ESAITestScreenCaptureDto result;
        }

        private readonly struct ESAITestCameraPath
        {
            public readonly Camera camera;
            public readonly string path;

            public ESAITestCameraPath(Camera camera, string path)
            {
                this.camera = camera;
                this.path = path;
            }
        }

        private readonly struct ESAITestSelectablePath
        {
            public readonly Selectable selectable;
            public readonly string path;

            public ESAITestSelectablePath(Selectable selectable, string path)
            {
                this.selectable = selectable;
                this.path = path;
            }
        }

        private readonly struct ESAITestSceneNode
        {
            public readonly Transform transform;
            public readonly int depth;

            public ESAITestSceneNode(Transform transform, int depth)
            {
                this.transform = transform;
                this.depth = depth;
            }
        }
    }

    public static class ESAITestObservationProviderBootstrap
    {
        private static GameObject host;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            ESAITestRuntime.Activated -= EnsureProvider;
            ESAITestRuntime.Activated += EnsureProvider;
            ESAITestRuntime.Deactivated -= DestroyProvider;
            ESAITestRuntime.Deactivated += DestroyProvider;
        }

        private static void EnsureProvider()
        {
            if (host != null)
                return;
            host = new GameObject("ESAITest Observation Provider");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<ESAITestObservationCapabilityProvider>();
            host.AddComponent<ESAITestAIPromptCapabilityProvider>();
            host.AddComponent<ESAITestExternalPromptInbox>();
        }

        private static void DestroyProvider()
        {
            if (host == null)
                return;
            UnityEngine.Object.Destroy(host);
            host = null;
        }
    }
}
