#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UIElements;

namespace ES
{
    [Serializable]
    internal sealed class ESWorkbenchVisualEvidenceManifest
    {
        public int schemaVersion = 5;
        public string runId = string.Empty;
        public string scenarioId = string.Empty;
        public string capturedUtc = string.Empty;
        public string projectRoot = string.Empty;
        public string workbenchId = string.Empty;
        public string windowType = string.Empty;
        public string windowTitle = string.Empty;
        public string unityVersion = string.Empty;
        public string assemblyModuleVersionId = string.Empty;
        public string sourceAssetPath = string.Empty;
        public string sourceAssetGuid = string.Empty;
        public string theme = string.Empty;
        public string layoutTier = string.Empty;
        public string activeDocument = string.Empty;
        public string activeViewport = string.Empty;
        public float pixelsPerPoint;
        public float screenX;
        public float screenY;
        public float logicalWidth;
        public float logicalHeight;
        public float availableCenterWidth;
        public int capturedPixelWidth;
        public int capturedPixelHeight;
        public bool longChineseContent;
        public bool layoutContractPassed;
        public string expectedScenarioId = string.Empty;
        public bool scenarioMatch;
        public string scenarioMatchSummary = string.Empty;
        public bool interactionMatrixPassed;
        public List<ESWorkbenchVisualInteractionCheck> interactionChecks =
            new List<ESWorkbenchVisualInteractionCheck>();
        public float capturePixelsPerPoint;
        public string captureGeometrySource = string.Empty;
        public bool captureGeometryTrusted;
        public string captureGeometrySummary = string.Empty;
        public bool nativeWindowMetricsAvailable;
        public bool nativeWindowOwnedByUnity;
        public int nativeDpi;
        public float nativeDpiScale;
        public int nativeWindowX;
        public int nativeWindowY;
        public int nativeWindowWidth;
        public int nativeWindowHeight;
        public int nativeClientX;
        public int nativeClientY;
        public int nativeClientWidth;
        public int nativeClientHeight;
        public float logicalToPhysicalScaleX;
        public float logicalToPhysicalScaleY;
        public bool layoutProbePassed;
        public List<ESWorkbenchVisualLayoutProbe> layoutProbes =
            new List<ESWorkbenchVisualLayoutProbe>();
        public bool pixelVariancePassed;
        public string pixelVarianceSummary = string.Empty;
        public string layoutSummary = string.Empty;
        public string screenshotAbsolutePath = string.Empty;
        public string screenshotProjectPath = string.Empty;
        public string manifestAbsolutePath = string.Empty;
        public string manifestProjectPath = string.Empty;
        public string evidenceBoundary = string.Empty;
    }

    [Serializable]
    internal sealed class ESWorkbenchVisualLayoutProbe
    {
        public string probeId = string.Empty;
        public string title = string.Empty;
        public bool passed;
        public float x;
        public float y;
        public float width;
        public float height;
        public string diagnostic = string.Empty;

        public ESWorkbenchVisualLayoutProbe Clone()
        {
            return new ESWorkbenchVisualLayoutProbe
            {
                probeId = probeId,
                title = title,
                passed = passed,
                x = x,
                y = y,
                width = width,
                height = height,
                diagnostic = diagnostic
            };
        }
    }

    internal readonly struct ESWorkbenchScreenCaptureGeometry
    {
        public ESWorkbenchScreenCaptureGeometry(
            RectInt captureRect,
            RectInt nativeWindowRect,
            RectInt nativeClientRect,
            bool nativeMetricsAvailable,
            bool nativeWindowOwnedByUnity,
            int nativeDpi,
            float nativeDpiScale,
            float editorPixelsPerPoint,
            bool trusted,
            string source,
            string summary)
        {
            CaptureRect = captureRect;
            NativeWindowRect = nativeWindowRect;
            NativeClientRect = nativeClientRect;
            NativeMetricsAvailable = nativeMetricsAvailable;
            NativeWindowOwnedByUnity = nativeWindowOwnedByUnity;
            NativeDpi = nativeDpi;
            NativeDpiScale = nativeDpiScale;
            EditorPixelsPerPoint = editorPixelsPerPoint;
            Trusted = trusted;
            Source = source ?? string.Empty;
            Summary = summary ?? string.Empty;
        }

        public RectInt CaptureRect { get; }
        public RectInt NativeWindowRect { get; }
        public RectInt NativeClientRect { get; }
        public bool NativeMetricsAvailable { get; }
        public bool NativeWindowOwnedByUnity { get; }
        public int NativeDpi { get; }
        public float NativeDpiScale { get; }
        public float EditorPixelsPerPoint { get; }
        public bool Trusted { get; }
        public string Source { get; }
        public string Summary { get; }
    }

    [Serializable]
    internal sealed class ESWorkbenchVisualInteractionCheck
    {
        public string checkId = string.Empty;
        public string title = string.Empty;
        public string expected = string.Empty;
        public bool passed;
        public int requiredObservationCount;
        public int observationCount;
        public string evidenceSource = string.Empty;
        public string observedUtc = string.Empty;
        public string observationSummary = string.Empty;

        public ESWorkbenchVisualInteractionCheck Clone()
        {
            return new ESWorkbenchVisualInteractionCheck
            {
                checkId = checkId,
                title = title,
                expected = expected,
                passed = passed,
                requiredObservationCount = requiredObservationCount,
                observationCount = observationCount,
                evidenceSource = evidenceSource,
                observedUtc = observedUtc,
                observationSummary = observationSummary
            };
        }
    }

    /// <summary>
    /// 一次真实 UI 事件的聚合记录。它只存在于当前窗口会话，最终清单只写入不可变的摘要，
    /// 不把 Unity 对象、VisualElement 或 InstanceId 持久化。
    /// </summary>
    internal sealed class ESWorkbenchVisualInteractionObservation
    {
        private readonly HashSet<string> targets = new HashSet<string>(StringComparer.Ordinal);

        public int EventCount { get; private set; }
        public string LastObservedUtc { get; private set; } = string.Empty;
        public string Source { get; private set; } = string.Empty;

        public IReadOnlyCollection<string> Targets => targets;

        public void Record(string source, string target)
        {
            EventCount++;
            LastObservedUtc = DateTime.UtcNow.ToString("O");
            if (!string.IsNullOrWhiteSpace(source)) Source = source;
            if (!string.IsNullOrWhiteSpace(target)) targets.Add(target);
        }
    }

    [Serializable]
    internal sealed class ESWorkbenchVisualEvidenceLatestPointer
    {
        public string runId = string.Empty;
        public string sourceAssetGuid = string.Empty;
        public string assemblyModuleVersionId = string.Empty;
        public string runDirectory = string.Empty;
        public string screenshotPath = string.Empty;
        public string manifestPath = string.Empty;
    }

    [Serializable]
    internal sealed class ESWorkbenchVisualEvidenceIndexEntry
    {
        public string scenarioId = string.Empty;
        public string sourceAssetGuid = string.Empty;
        public string assemblyModuleVersionId = string.Empty;
        public string runId = string.Empty;
        public string capturedUtc = string.Empty;
        public string runDirectory = string.Empty;
        public string screenshotPath = string.Empty;
        public string manifestPath = string.Empty;
    }

    [Serializable]
    internal sealed class ESWorkbenchVisualEvidenceIndex
    {
        public int schemaVersion = 3;
        public List<ESWorkbenchVisualEvidenceIndexEntry> entries =
            new List<ESWorkbenchVisualEvidenceIndexEntry>();
    }

    internal readonly struct ESWorkbenchVisualEvidenceCaptureRequest
    {
        public ESWorkbenchVisualEvidenceCaptureRequest(
            string workbenchId,
            ESWorkbenchVisualEnvironment environment,
            ESWorkbenchResponsiveTier layoutTier,
            bool layoutContractPassed,
            string layoutSummary,
            string activeDocument,
            string activeViewport,
            string expectedScenarioId = null,
            bool scenarioMatch = true,
            string scenarioMatchSummary = null,
            IReadOnlyList<ESWorkbenchVisualInteractionCheck> interactionChecks = null,
            string sourceAssetPath = null,
            string sourceAssetGuid = null)
        {
            WorkbenchId = workbenchId ?? string.Empty;
            Environment = environment;
            LayoutTier = layoutTier;
            LayoutContractPassed = layoutContractPassed;
            LayoutSummary = layoutSummary ?? string.Empty;
            ActiveDocument = activeDocument ?? string.Empty;
            ActiveViewport = activeViewport ?? string.Empty;
            ExpectedScenarioId = expectedScenarioId ?? string.Empty;
            ScenarioMatch = scenarioMatch;
            ScenarioMatchSummary = scenarioMatchSummary ?? string.Empty;
            SourceAssetPath = (sourceAssetPath ?? string.Empty).Replace('\\', '/');
            SourceAssetGuid = sourceAssetGuid?.Trim() ?? string.Empty;
            InteractionChecks = interactionChecks == null
                ? Array.Empty<ESWorkbenchVisualInteractionCheck>()
                : interactionChecks.Where(value => value != null)
                    .Select(value => value.Clone())
                    .ToArray();
            InteractionMatrixPassed = ESWorkbenchVisualEvidenceCapture
                .IsCommercialInteractionMatrixComplete(InteractionChecks);
        }

        public string WorkbenchId { get; }
        public ESWorkbenchVisualEnvironment Environment { get; }
        public ESWorkbenchResponsiveTier LayoutTier { get; }
        public bool LayoutContractPassed { get; }
        public string LayoutSummary { get; }
        public string ActiveDocument { get; }
        public string ActiveViewport { get; }
        public string ExpectedScenarioId { get; }
        public bool ScenarioMatch { get; }
        public string ScenarioMatchSummary { get; }
        public string SourceAssetPath { get; }
        public string SourceAssetGuid { get; }
        public IReadOnlyList<ESWorkbenchVisualInteractionCheck> InteractionChecks { get; }
        public bool InteractionMatrixPassed { get; }
    }

    internal readonly struct ESWorkbenchVisualEvidenceCaptureResult
    {
        public ESWorkbenchVisualEvidenceCaptureResult(
            bool success,
            string message,
            string runDirectory,
            string screenshotPath,
            string manifestPath)
        {
            Success = success;
            Message = message ?? string.Empty;
            RunDirectory = runDirectory ?? string.Empty;
            ScreenshotPath = screenshotPath ?? string.Empty;
            ManifestPath = manifestPath ?? string.Empty;
        }

        public bool Success { get; }
        public string Message { get; }
        public string RunDirectory { get; }
        public string ScreenshotPath { get; }
        public string ManifestPath { get; }
    }

    /// <summary>Captures the current editor window only after an explicit user action.</summary>
    internal static class ESWorkbenchVisualEvidenceCapture
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint
        {
            public int x;
            public int y;
        }

        private readonly struct NativeWindowMetrics
        {
            public NativeWindowMetrics(
                RectInt windowRect,
                RectInt clientRect,
                bool ownedByUnity,
                int dpi,
                bool dpiAvailable)
            {
                WindowRect = windowRect;
                ClientRect = clientRect;
                OwnedByUnity = ownedByUnity;
                Dpi = dpi;
                DpiAvailable = dpiAvailable;
            }

            public RectInt WindowRect { get; }
            public RectInt ClientRect { get; }
            public bool OwnedByUnity { get; }
            public int Dpi { get; }
            public bool DpiAvailable { get; }
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr window, out NativeRect rect);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetClientRect(IntPtr window, out NativeRect rect);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ClientToScreen(IntPtr window, ref NativePoint point);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr window);

        private const int MaximumCaptureDimension = 8192;
        private const long MaximumCapturePixels = 64L * 1024L * 1024L;
        private const long MaximumIndexBytes = 1L * 1024L * 1024L;
        private const long MaximumManifestBytes = 8L * 1024L * 1024L;
        private static readonly string[] CommercialInteractionCheckIds =
        {
            "window-open-focus",
            "pane-collapse-restore",
            "pane-resize",
            "viewport-switch",
            "viewport-input",
            "bottom-channel-switch",
            "command-overflow"
        };
        private static readonly string[] CommercialInteractionCheckTitles =
        {
            "窗口打开、聚焦和重绘正常",
            "面板折叠与恢复正常",
            "左右面板拖拽比例正常",
            "2D、3D、游戏构图视口均完成切换",
            "视口指针与滚轮输入正常",
            "问题、历史、任务、日志等底部通道切换正常",
            "当前宽度下固定命令与溢出菜单均可执行"
        };
        private static readonly string[] CommercialInteractionCheckExpected =
        {
            "窗口没有空白、闪烁、异常遮挡或错误覆盖层。",
            "左右或底部面板至少完成一次收起和一次恢复。",
            "至少完成一次面板分隔线拖拽，中心作者区仍受保护。",
            "至少切换过 2D、3D 和游戏构图三个视口。",
            "至少收到指针和滚轮两类真实视口事件。",
            "切换后内容对应当前通道，旧面板内容已释放且没有残留交互。",
            "命令不会丢失、重复或被截断，危险操作仍保留明确语义。"
        };
        private static readonly int[] CommercialInteractionMinimumCounts =
        {
            1, 2, 1, 3, 2, 2, 1
        };
        private const string EvidenceBoundary =
            "当前记录只证明截图时刻的真实 EditorWindow 外观、原生窗口边界、布局探针与环境；不会自动切换主题、DPI 或 Unity 缩放，也不替代 Profiler、Player 或发布验收。";

        private static string EvidenceRoot => Path.GetFullPath(Path.Combine(
            Directory.GetCurrentDirectory(), "Library", "ESWorkbench", "VisualValidation"));

        internal static string CurrentAssemblyModuleVersionId =>
            typeof(ESWorkbenchVisualEvidenceCapture).Assembly.ManifestModule.ModuleVersionId.ToString("D");

        public static ESWorkbenchVisualEvidenceCaptureResult Capture(
            EditorWindow window,
            ESWorkbenchVisualEvidenceCaptureRequest request)
        {
            if (window == null)
                return Failure("当前工作台窗口已经关闭，无法采集视觉证据。");
            if (!request.LayoutContractPassed)
                return Failure("当前窗口未通过布局保护合同，不能生成商业视觉证据。");
            if (!request.ScenarioMatch || string.IsNullOrWhiteSpace(request.ExpectedScenarioId))
                return Failure("当前窗口与所选视觉矩阵场景不匹配。");
            if (!request.InteractionMatrixPassed
                || !IsCommercialInteractionMatrixComplete(request.InteractionChecks))
                return Failure("当前场景尚未完成真实 UI 交互矩阵。");
            if (string.IsNullOrWhiteSpace(request.SourceAssetPath)
                || string.IsNullOrWhiteSpace(request.SourceAssetGuid))
                return Failure("视觉证据采集要求绑定已经保存的 Source 资产。");
            string resolvedSourcePath = AssetDatabase.GUIDToAssetPath(request.SourceAssetGuid);
            if (string.IsNullOrWhiteSpace(resolvedSourcePath)
                || !string.Equals(
                    resolvedSourcePath.Replace('\\', '/'),
                    request.SourceAssetPath,
                    StringComparison.OrdinalIgnoreCase)
                || AssetDatabase.LoadMainAssetAtPath(resolvedSourcePath) == null)
                return Failure("视觉证据 Source 身份已失效，请重新绑定当前资产后再采集。");

            window.Focus();
            window.Repaint();
            Rect rect = window.position;
            float pixelsPerPoint = Mathf.Clamp(EditorGUIUtility.pixelsPerPoint, 1f, 4f);
            bool hasNativeMetrics = TryGetNativeWindowMetrics(out NativeWindowMetrics nativeMetrics);
            ESWorkbenchScreenCaptureGeometry geometry = ResolveCaptureGeometry(
                rect,
                pixelsPerPoint,
                hasNativeMetrics,
                hasNativeMetrics ? nativeMetrics.WindowRect : default,
                hasNativeMetrics ? nativeMetrics.ClientRect : default,
                hasNativeMetrics && nativeMetrics.OwnedByUnity,
                hasNativeMetrics && nativeMetrics.DpiAvailable ? nativeMetrics.Dpi : 0);
            if (!geometry.Trusted)
                return Failure("当前窗口物理边界不可信，已拒绝商业视觉证据：" + geometry.Summary);

            IReadOnlyList<ESWorkbenchVisualLayoutProbe> layoutProbes =
                CreateLayoutProbeSnapshot(window.rootVisualElement);
            if (!IsLayoutProbeSnapshotComplete(layoutProbes))
            {
                string failed = string.Join("；", layoutProbes
                    .Where(value => value != null && !value.passed)
                    .Select(value => value.title + "：" + value.diagnostic));
                return Failure("当前窗口关键布局探针未通过，已拒绝商业视觉证据：" + failed);
            }

            int width = geometry.CaptureRect.width;
            int height = geometry.CaptureRect.height;
            if (width < 64 || height < 64)
                return Failure("当前窗口尺寸过小，无法生成有效截图。");
            if (width > MaximumCaptureDimension || height > MaximumCaptureDimension
                || (long)width * height > MaximumCapturePixels)
                return Failure("当前窗口像素范围超过视觉证据采集上限。");

            string safeWorkbenchId = SanitizePathSegment(request.WorkbenchId);
            string runId = DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ")
                + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            string workbenchRoot = Path.GetFullPath(Path.Combine(EvidenceRoot, safeWorkbenchId));
            string runDirectory = Path.GetFullPath(Path.Combine(workbenchRoot, runId));
            string screenshotPath = Path.Combine(runDirectory, "window.png");
            string manifestPath = Path.Combine(runDirectory, "manifest.json");
            if (!IsWithinRoot(runDirectory, EvidenceRoot))
                return Failure("视觉证据输出路径未通过项目 Library 安全边界检查。");

            Texture2D texture = null;
            try
            {
                Vector2 screenPosition = new Vector2(
                    geometry.CaptureRect.x,
                    geometry.CaptureRect.y);
                Color[] pixels = InternalEditorUtility.ReadScreenPixel(screenPosition, width, height);
                if (pixels == null || pixels.Length != width * height)
                    return Failure("Unity 未返回完整的当前窗口屏幕像素。");
                if (!HasUsablePixelVariance(pixels, out string pixelVarianceSummary))
                    return Failure("当前截图像素近乎空白或单色，已拒绝商业视觉证据：" + pixelVarianceSummary);

                Directory.CreateDirectory(runDirectory);
                texture = new Texture2D(width, height, UnityEngine.TextureFormat.RGB24, false)
                {
                    name = "ES Workbench Visual Evidence",
                    hideFlags = HideFlags.HideAndDontSave
                };
                texture.SetPixels(pixels);
                texture.Apply(false, false);
                byte[] png = texture.EncodeToPNG();
                if (png == null || png.Length == 0)
                    return Failure("当前窗口截图编码失败。");
                File.WriteAllBytes(screenshotPath, png);

                string projectRoot = Path.GetFullPath(Directory.GetCurrentDirectory());
                var manifest = new ESWorkbenchVisualEvidenceManifest
                {
                    runId = runId,
                    scenarioId = BuildScenarioId(request.Environment, request.LayoutTier),
                    capturedUtc = DateTime.UtcNow.ToString("O"),
                    projectRoot = projectRoot,
                    workbenchId = request.WorkbenchId,
                    windowType = window.GetType().FullName,
                    windowTitle = window.titleContent?.text ?? string.Empty,
                    unityVersion = Application.unityVersion,
                    assemblyModuleVersionId = CurrentAssemblyModuleVersionId,
                    sourceAssetPath = request.SourceAssetPath,
                    sourceAssetGuid = request.SourceAssetGuid,
                    theme = request.Environment.Theme == ESWorkbenchVisualTheme.Dark ? "Dark" : "Light",
                    layoutTier = request.LayoutTier.ToString(),
                    activeDocument = request.ActiveDocument,
                    activeViewport = request.ActiveViewport,
                    pixelsPerPoint = request.Environment.PixelsPerPoint,
                    screenX = rect.x,
                    screenY = rect.y,
                    logicalWidth = rect.width,
                    logicalHeight = rect.height,
                    availableCenterWidth = request.Environment.CenterWidth,
                    capturedPixelWidth = width,
                    capturedPixelHeight = height,
                    longChineseContent = request.Environment.LongChineseContent,
                    layoutContractPassed = request.LayoutContractPassed,
                    expectedScenarioId = request.ExpectedScenarioId,
                    scenarioMatch = request.ScenarioMatch,
                    scenarioMatchSummary = request.ScenarioMatchSummary,
                    interactionMatrixPassed = request.InteractionMatrixPassed,
                    interactionChecks = request.InteractionChecks
                        .Select(value => value.Clone())
                        .ToList(),
                    capturePixelsPerPoint = pixelsPerPoint,
                    captureGeometrySource = geometry.Source,
                    captureGeometryTrusted = geometry.Trusted,
                    captureGeometrySummary = geometry.Summary,
                    nativeWindowMetricsAvailable = geometry.NativeMetricsAvailable,
                    nativeWindowOwnedByUnity = geometry.NativeWindowOwnedByUnity,
                    nativeDpi = geometry.NativeDpi,
                    nativeDpiScale = geometry.NativeDpiScale,
                    nativeWindowX = geometry.NativeWindowRect.x,
                    nativeWindowY = geometry.NativeWindowRect.y,
                    nativeWindowWidth = geometry.NativeWindowRect.width,
                    nativeWindowHeight = geometry.NativeWindowRect.height,
                    nativeClientX = geometry.NativeClientRect.x,
                    nativeClientY = geometry.NativeClientRect.y,
                    nativeClientWidth = geometry.NativeClientRect.width,
                    nativeClientHeight = geometry.NativeClientRect.height,
                    logicalToPhysicalScaleX = rect.width <= 0f ? 0f : width / rect.width,
                    logicalToPhysicalScaleY = rect.height <= 0f ? 0f : height / rect.height,
                    layoutProbePassed = true,
                    layoutProbes = layoutProbes.Select(value => value.Clone()).ToList(),
                    pixelVariancePassed = true,
                    pixelVarianceSummary = pixelVarianceSummary,
                    layoutSummary = request.LayoutSummary,
                    screenshotAbsolutePath = Path.GetFullPath(screenshotPath),
                    screenshotProjectPath = ToProjectPath(projectRoot, screenshotPath),
                    manifestAbsolutePath = Path.GetFullPath(manifestPath),
                    manifestProjectPath = ToProjectPath(projectRoot, manifestPath),
                    evidenceBoundary = EvidenceBoundary
                };
                WriteUtf8Atomic(manifestPath, JsonUtility.ToJson(manifest, true));
                UpdateScenarioIndex(workbenchRoot, manifest);

                var latest = new ESWorkbenchVisualEvidenceLatestPointer
                {
                    runId = runId,
                    sourceAssetGuid = request.SourceAssetGuid,
                    assemblyModuleVersionId = CurrentAssemblyModuleVersionId,
                    runDirectory = runDirectory,
                    screenshotPath = Path.GetFullPath(screenshotPath),
                    manifestPath = Path.GetFullPath(manifestPath)
                };
                WriteUtf8Atomic(Path.Combine(workbenchRoot, "latest.json"), JsonUtility.ToJson(latest, true));
                return new ESWorkbenchVisualEvidenceCaptureResult(
                    true,
                    "已采集当前真实工作台窗口；其他主题、DPI 和缩放场景仍需在对应环境分别采集。",
                    runDirectory,
                    latest.screenshotPath,
                    latest.manifestPath);
            }
            catch (Exception exception)
            {
                return Failure("视觉证据采集失败：" + exception.Message);
            }
            finally
            {
                if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        public static bool TryGetLatest(
            string workbenchId,
            string sourceAssetGuid,
            out ESWorkbenchVisualEvidenceCaptureResult result)
        {
            result = default;
            if (string.IsNullOrWhiteSpace(sourceAssetGuid)) return false;
            try
            {
                string workbenchRoot = Path.GetFullPath(Path.Combine(
                    EvidenceRoot, SanitizePathSegment(workbenchId)));
                ESWorkbenchVisualEvidenceIndex index = ReadIndex(workbenchRoot);
                ESWorkbenchVisualEvidenceIndexEntry latest = index.entries
                    .Where(value => value != null
                        && string.Equals(
                            value.sourceAssetGuid, sourceAssetGuid, StringComparison.Ordinal))
                    .OrderByDescending(value => value.capturedUtc, StringComparer.Ordinal)
                    .FirstOrDefault(value => IsValidEntry(
                        value, workbenchRoot, workbenchId, value.scenarioId, sourceAssetGuid));
                if (latest == null) return false;
                result = new ESWorkbenchVisualEvidenceCaptureResult(
                    true, "已读取最近一次视觉证据。",
                    latest.runDirectory, latest.screenshotPath, latest.manifestPath);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool TryGetScenario(
            string workbenchId,
            string scenarioId,
            string sourceAssetGuid,
            out ESWorkbenchVisualEvidenceCaptureResult result)
        {
            result = default;
            if (string.IsNullOrWhiteSpace(scenarioId)
                || string.IsNullOrWhiteSpace(sourceAssetGuid)) return false;
            try
            {
                string workbenchRoot = Path.GetFullPath(Path.Combine(
                    EvidenceRoot, SanitizePathSegment(workbenchId)));
                ESWorkbenchVisualEvidenceIndex index = ReadIndex(workbenchRoot);
                ESWorkbenchVisualEvidenceIndexEntry entry = index.entries
                    .FirstOrDefault(value => value != null
                        && string.Equals(value.scenarioId, scenarioId, StringComparison.Ordinal)
                        && string.Equals(
                            value.sourceAssetGuid, sourceAssetGuid, StringComparison.Ordinal));
                if (!IsValidEntry(
                    entry, workbenchRoot, workbenchId, scenarioId, sourceAssetGuid)) return false;
                result = new ESWorkbenchVisualEvidenceCaptureResult(
                    true,
                    "已读取该矩阵场景的真实窗口证据。",
                    entry.runDirectory,
                    entry.screenshotPath,
                    entry.manifestPath);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static int CountCapturedScenarios(
            string workbenchId,
            IReadOnlyList<ESWorkbenchVisualValidationScenario> scenarios,
            string sourceAssetGuid)
        {
            if (scenarios == null || scenarios.Count == 0
                || string.IsNullOrWhiteSpace(sourceAssetGuid)) return 0;
            int captured = 0;
            for (int i = 0; i < scenarios.Count; i++)
            {
                ESWorkbenchVisualValidationScenario scenario = scenarios[i];
                if (scenario != null && TryGetScenario(
                    workbenchId, scenario.ScenarioId, sourceAssetGuid, out _)) captured++;
            }
            return captured;
        }

        internal static Vector2Int ResolveCapturePixelSize(Rect logicalRect, float pixelsPerPoint)
        {
            float scale = Mathf.Clamp(pixelsPerPoint, 1f, 4f);
            return new Vector2Int(
                Mathf.Max(1, Mathf.RoundToInt(logicalRect.width * scale)),
                Mathf.Max(1, Mathf.RoundToInt(logicalRect.height * scale)));
        }

        internal static ESWorkbenchScreenCaptureGeometry ResolveCaptureGeometry(
            Rect logicalRect,
            float editorPixelsPerPoint,
            bool nativeMetricsAvailable,
            RectInt nativeWindowRect,
            RectInt nativeClientRect,
            bool nativeWindowOwnedByUnity,
            int nativeDpi)
        {
            float editorScale = Mathf.Clamp(editorPixelsPerPoint, 1f, 4f);
            float nativeScale = nativeDpi >= 72 && nativeDpi <= 768
                ? Mathf.Clamp(nativeDpi / 96f, 0.75f, 4f)
                : editorScale;
            Vector2Int expectedSize = ResolveCapturePixelSize(logicalRect, nativeScale);
            RectInt fallback = new RectInt(
                Mathf.RoundToInt(logicalRect.x * editorScale),
                Mathf.RoundToInt(logicalRect.y * editorScale),
                Mathf.Max(1, Mathf.RoundToInt(logicalRect.width * editorScale)),
                Mathf.Max(1, Mathf.RoundToInt(logicalRect.height * editorScale)));

            if (!nativeMetricsAvailable)
                return new ESWorkbenchScreenCaptureGeometry(
                    fallback, default, default, false, false, 0, nativeScale, editorScale,
                    false, "EditorPixelsPerPointFallback",
                    "当前平台没有可核对的原生窗口边界，不能证明截图与目标窗口一致。");
            if (!nativeWindowOwnedByUnity)
                return new ESWorkbenchScreenCaptureGeometry(
                    fallback, nativeWindowRect, nativeClientRect, true, false, nativeDpi,
                    nativeScale, editorScale, false, "ForeignForegroundWindow",
                    "前台原生窗口不属于当前 Unity Editor 进程。");
            if (nativeDpi < 72 || nativeDpi > 768)
                return new ESWorkbenchScreenCaptureGeometry(
                    fallback, nativeWindowRect, nativeClientRect, true, true, nativeDpi,
                    nativeScale, editorScale, false, "NativeDpiUnavailable",
                    "原生窗口存在，但无法取得可信 DPI。");

            if (MatchesExpectedSize(nativeClientRect, expectedSize, 0.08f, 0.12f))
                return TrustedGeometry(
                    nativeClientRect, nativeWindowRect, nativeClientRect, nativeDpi,
                    nativeScale, editorScale, "WindowsClientRect",
                    "原生客户区与目标 EditorWindow 逻辑尺寸匹配。");
            if (MatchesExpectedSize(nativeWindowRect, expectedSize, 0.08f, 0.12f))
                return TrustedGeometry(
                    nativeWindowRect, nativeWindowRect, nativeClientRect, nativeDpi,
                    nativeScale, editorScale, "WindowsWindowRect",
                    "原生窗口边界与目标 EditorWindow 逻辑尺寸匹配。");

            RectInt scaledPosition = new RectInt(
                Mathf.RoundToInt(logicalRect.x * nativeScale),
                Mathf.RoundToInt(logicalRect.y * nativeScale),
                expectedSize.x,
                expectedSize.y);
            if (ContainsWithTolerance(nativeClientRect, scaledPosition, 4))
                return TrustedGeometry(
                    scaledPosition, nativeWindowRect, nativeClientRect, nativeDpi,
                    nativeScale, editorScale, "LogicalRectNativeDpi",
                    "目标 EditorWindow 的原生 DPI 映射完整位于 Unity 客户区内。");

            RectInt physicalPosition = new RectInt(
                Mathf.RoundToInt(logicalRect.x),
                Mathf.RoundToInt(logicalRect.y),
                expectedSize.x,
                expectedSize.y);
            if (ContainsWithTolerance(nativeClientRect, physicalPosition, 4))
                return TrustedGeometry(
                    physicalPosition, nativeWindowRect, nativeClientRect, nativeDpi,
                    nativeScale, editorScale, "PhysicalPositionNativeSize",
                    "目标 EditorWindow 的屏幕位置已是物理坐标，尺寸按原生 DPI 映射。");

            return new ESWorkbenchScreenCaptureGeometry(
                fallback, nativeWindowRect, nativeClientRect, true, true, nativeDpi,
                nativeScale, editorScale, false, "UnmatchedNativeBoundary",
                "目标 EditorWindow 的候选物理边界均未完整落入当前 Unity 原生客户区。"
                + " 逻辑=" + logicalRect.width.ToString("0.#") + "x" + logicalRect.height.ToString("0.#")
                + "，原生客户区=" + nativeClientRect.width + "x" + nativeClientRect.height
                + "，DPI=" + nativeDpi + "。");
        }

        private static ESWorkbenchScreenCaptureGeometry TrustedGeometry(
            RectInt captureRect,
            RectInt nativeWindowRect,
            RectInt nativeClientRect,
            int nativeDpi,
            float nativeScale,
            float editorScale,
            string source,
            string summary)
        {
            return new ESWorkbenchScreenCaptureGeometry(
                captureRect,
                nativeWindowRect,
                nativeClientRect,
                true,
                true,
                nativeDpi,
                nativeScale,
                editorScale,
                true,
                source,
                summary + " EditorScale=" + editorScale.ToString("0.###")
                    + "，NativeScale=" + nativeScale.ToString("0.###")
                    + "，Capture=" + captureRect.width + "x" + captureRect.height + "。");
        }

        private static bool MatchesExpectedSize(
            RectInt rect,
            Vector2Int expected,
            float widthTolerance,
            float heightTolerance)
        {
            if (rect.width <= 0 || rect.height <= 0 || expected.x <= 0 || expected.y <= 0) return false;
            float widthError = Mathf.Abs(rect.width - expected.x) / (float)expected.x;
            float heightError = Mathf.Abs(rect.height - expected.y) / (float)expected.y;
            return widthError <= widthTolerance && heightError <= heightTolerance;
        }

        private static bool ContainsWithTolerance(RectInt container, RectInt candidate, int tolerance)
        {
            if (container.width <= 0 || container.height <= 0
                || candidate.width <= 0 || candidate.height <= 0) return false;
            return candidate.xMin >= container.xMin - tolerance
                && candidate.yMin >= container.yMin - tolerance
                && candidate.xMax <= container.xMax + tolerance
                && candidate.yMax <= container.yMax + tolerance;
        }

        internal static IReadOnlyList<ESWorkbenchVisualLayoutProbe> CreateLayoutProbeSnapshot(
            VisualElement windowRoot)
        {
            var probes = new List<ESWorkbenchVisualLayoutProbe>();
            VisualElement host = windowRoot?.Q<VisualElement>("ESWorkbenchHost");
            if (host == null)
            {
                probes.Add(CreateMissingProbe("host", "工作台根节点"));
                return probes;
            }

            Rect hostBounds = host.worldBound;
            AddElementProbe(probes, host, hostBounds, "host", "工作台根节点", 560f, 420f);
            VisualElement commandBar = host.Q<VisualElement>("ESWorkbenchCommandBar");
            VisualElement workspace = host.Q<VisualElement>("ESWorkbenchWorkspaceSplit");
            VisualElement center = host.Q<VisualElement>("ESWorkbenchCenterPanel");
            VisualElement viewport = host.Q<VisualElement>("ESWorkbenchViewportHost");
            VisualElement bottom = host.Q<VisualElement>("ESWorkbenchBottomDrawer");
            VisualElement status = host.Q<VisualElement>("ESWorkbenchStatusBar");
            AddElementProbe(probes, commandBar, hostBounds, "command-bar", "顶部命令栏", 320f, 28f);
            AddElementProbe(probes, workspace, hostBounds, "workspace", "作者工作区", 320f, 240f);
            AddElementProbe(probes, center, hostBounds, "center", "中心作者区", 280f, 220f);
            AddElementProbe(probes, viewport, hostBounds, "viewport", "当前视口", 240f, 160f);
            AddElementProbe(probes, bottom, hostBounds, "bottom-drawer", "底部通道", 280f, 80f);
            AddElementProbe(probes, status, hostBounds, "status-bar", "底部状态栏", 280f, 18f);

            bool sequencePassed = commandBar != null && workspace != null && status != null
                && IsFinite(commandBar.worldBound) && IsFinite(workspace.worldBound) && IsFinite(status.worldBound)
                && commandBar.worldBound.yMax <= workspace.worldBound.yMin + 2f
                && workspace.worldBound.yMax <= status.worldBound.yMin + 2f;
            probes.Add(new ESWorkbenchVisualLayoutProbe
            {
                probeId = "vertical-sequence",
                title = "顶部/工作区/状态栏顺序",
                passed = sequencePassed,
                diagnostic = sequencePassed
                    ? "纵向区域顺序稳定且没有相互覆盖。"
                    : "顶部命令栏、作者工作区或状态栏发生重叠或顺序异常。"
            });

            bool commandChildrenContained = commandBar != null
                && commandBar.Children().Where(IsVisibleWithArea)
                    .All(child => Contains(commandBar.worldBound, child.worldBound, 2f));
            probes.Add(new ESWorkbenchVisualLayoutProbe
            {
                probeId = "command-children-contained",
                title = "顶部命令完整可见",
                passed = commandChildrenContained,
                diagnostic = commandChildrenContained
                    ? "可见命令、资产字段和溢出入口均位于顶部栏边界内。"
                    : "顶部栏存在越界、裁切或覆盖的可见子元素。"
            });
            return probes;
        }

        internal static bool IsLayoutProbeSnapshotComplete(
            IReadOnlyList<ESWorkbenchVisualLayoutProbe> probes)
        {
            return probes != null && probes.Count >= 9
                && probes.All(value => value != null && value.passed);
        }

        private static void AddElementProbe(
            ICollection<ESWorkbenchVisualLayoutProbe> probes,
            VisualElement element,
            Rect hostBounds,
            string id,
            string title,
            float minimumWidth,
            float minimumHeight)
        {
            if (element == null)
            {
                probes.Add(CreateMissingProbe(id, title));
                return;
            }

            Rect bounds = element.worldBound;
            bool visible = IsVisibleWithArea(element);
            bool sizeValid = bounds.width >= minimumWidth && bounds.height >= minimumHeight;
            bool contained = Contains(hostBounds, bounds, 2f);
            bool passed = visible && IsFinite(bounds) && sizeValid && contained;
            probes.Add(new ESWorkbenchVisualLayoutProbe
            {
                probeId = id,
                title = title,
                passed = passed,
                x = bounds.x,
                y = bounds.y,
                width = bounds.width,
                height = bounds.height,
                diagnostic = passed
                    ? "可见、尺寸稳定且完整位于工作台边界内。"
                    : "visible=" + visible
                        + "，size=" + bounds.width.ToString("0.#") + "x" + bounds.height.ToString("0.#")
                        + "，contained=" + contained + "。"
            });
        }

        private static ESWorkbenchVisualLayoutProbe CreateMissingProbe(string id, string title)
        {
            return new ESWorkbenchVisualLayoutProbe
            {
                probeId = id,
                title = title,
                passed = false,
                diagnostic = "关键视觉节点不存在。"
            };
        }

        private static bool IsVisibleWithArea(VisualElement element)
        {
            return element != null
                && element.resolvedStyle.display != DisplayStyle.None
                && element.resolvedStyle.visibility == Visibility.Visible
                && IsFinite(element.worldBound)
                && element.worldBound.width > 0.5f
                && element.worldBound.height > 0.5f;
        }

        private static bool IsFinite(Rect rect)
        {
            return !float.IsNaN(rect.x) && !float.IsInfinity(rect.x)
                && !float.IsNaN(rect.y) && !float.IsInfinity(rect.y)
                && !float.IsNaN(rect.width) && !float.IsInfinity(rect.width)
                && !float.IsNaN(rect.height) && !float.IsInfinity(rect.height);
        }

        private static bool Contains(Rect container, Rect candidate, float tolerance)
        {
            return candidate.xMin >= container.xMin - tolerance
                && candidate.yMin >= container.yMin - tolerance
                && candidate.xMax <= container.xMax + tolerance
                && candidate.yMax <= container.yMax + tolerance;
        }

        internal static bool HasUsablePixelVariance(Color[] pixels, out string summary)
        {
            summary = "没有像素。";
            if (pixels == null || pixels.Length == 0) return false;
            int stride = Mathf.Max(1, pixels.Length / 8192);
            float minimum = 1f;
            float maximum = 0f;
            var buckets = new HashSet<int>();
            int sampled = 0;
            for (int i = 0; i < pixels.Length; i += stride)
            {
                Color color = pixels[i];
                float luminance = Mathf.Clamp01(color.r * 0.2126f + color.g * 0.7152f + color.b * 0.0722f);
                minimum = Mathf.Min(minimum, luminance);
                maximum = Mathf.Max(maximum, luminance);
                int bucket = Mathf.Clamp(Mathf.RoundToInt(color.r * 15f), 0, 15)
                    | (Mathf.Clamp(Mathf.RoundToInt(color.g * 15f), 0, 15) << 4)
                    | (Mathf.Clamp(Mathf.RoundToInt(color.b * 15f), 0, 15) << 8);
                buckets.Add(bucket);
                sampled++;
            }
            float range = maximum - minimum;
            bool passed = sampled >= 32 && range >= 0.025f && buckets.Count >= 8;
            summary = "采样=" + sampled + "，亮度范围=" + range.ToString("0.000")
                + "，颜色桶=" + buckets.Count + "，" + (passed ? "通过" : "未通过") + "。";
            return passed;
        }

        private static bool TryGetNativeWindowMetrics(out NativeWindowMetrics metrics)
        {
            metrics = default;
            if (Application.platform != RuntimePlatform.WindowsEditor) return false;
            try
            {
                IntPtr window = GetForegroundWindow();
                if (window == IntPtr.Zero || !GetWindowRect(window, out NativeRect nativeWindow)) return false;
                if (!GetClientRect(window, out NativeRect nativeClient)) return false;
                var topLeft = new NativePoint { x = nativeClient.left, y = nativeClient.top };
                var bottomRight = new NativePoint { x = nativeClient.right, y = nativeClient.bottom };
                if (!ClientToScreen(window, ref topLeft) || !ClientToScreen(window, ref bottomRight)) return false;
                GetWindowThreadProcessId(window, out uint processId);
                bool ownedByUnity = processId == (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
                int dpi = 0;
                bool dpiAvailable = false;
                try
                {
                    dpi = (int)GetDpiForWindow(window);
                    dpiAvailable = dpi >= 72 && dpi <= 768;
                }
                catch (EntryPointNotFoundException)
                {
                    dpi = 0;
                }
                metrics = new NativeWindowMetrics(
                    ToRectInt(nativeWindow),
                    new RectInt(
                        topLeft.x,
                        topLeft.y,
                        Mathf.Max(0, bottomRight.x - topLeft.x),
                        Mathf.Max(0, bottomRight.y - topLeft.y)),
                    ownedByUnity,
                    dpi,
                    dpiAvailable);
                return true;
            }
            catch (DllNotFoundException)
            {
                return false;
            }
            catch (EntryPointNotFoundException)
            {
                return false;
            }
        }

        private static RectInt ToRectInt(NativeRect rect)
        {
            return new RectInt(
                rect.left,
                rect.top,
                Mathf.Max(0, rect.right - rect.left),
                Mathf.Max(0, rect.bottom - rect.top));
        }

        internal static IReadOnlyList<ESWorkbenchVisualInteractionCheck> CreateCommercialInteractionChecklist(
            IEnumerable<string> passedCheckIds = null)
        {
            var passed = new HashSet<string>(
                passedCheckIds ?? Array.Empty<string>(),
                StringComparer.Ordinal);
            var observations = new Dictionary<string, ESWorkbenchVisualInteractionObservation>(StringComparer.Ordinal);
            for (int i = 0; i < CommercialInteractionCheckIds.Length; i++)
            {
                string id = CommercialInteractionCheckIds[i];
                if (!passed.Contains(id)) continue;
                var observation = new ESWorkbenchVisualInteractionObservation();
                int count = CommercialInteractionMinimumCounts[i];
                for (int j = 0; j < count; j++)
                {
                    observation.Record("ui-event/test-fixture", id + "-" + j);
                }
                observations[id] = observation;
            }
            return CreateObservedInteractionChecklist(observations);
        }

        internal static IReadOnlyList<ESWorkbenchVisualInteractionCheck> CreateObservedInteractionChecklist(
            IReadOnlyDictionary<string, ESWorkbenchVisualInteractionObservation> observations)
        {
            var result = new List<ESWorkbenchVisualInteractionCheck>(CommercialInteractionCheckIds.Length);
            for (int i = 0; i < CommercialInteractionCheckIds.Length; i++)
            {
                string id = CommercialInteractionCheckIds[i];
                ESWorkbenchVisualInteractionObservation observation = null;
                if (observations != null)
                {
                    ESWorkbenchVisualInteractionObservation candidate;
                    if (observations.TryGetValue(id, out candidate)) observation = candidate;
                }
                int count = observation?.Targets?.Count ?? 0;
                int eventCount = observation?.EventCount ?? 0;
                bool passed = observation != null
                    && count >= CommercialInteractionMinimumCounts[i]
                    && eventCount > 0
                    && !string.IsNullOrWhiteSpace(observation.Source)
                    && !string.IsNullOrWhiteSpace(observation.LastObservedUtc);
                result.Add(new ESWorkbenchVisualInteractionCheck
                {
                    checkId = id,
                    title = CommercialInteractionCheckTitles[i],
                    expected = CommercialInteractionCheckExpected[i],
                    passed = passed,
                    requiredObservationCount = CommercialInteractionMinimumCounts[i],
                    observationCount = count,
                    evidenceSource = observation?.Source ?? string.Empty,
                    observedUtc = observation?.LastObservedUtc ?? string.Empty,
                    observationSummary = observation == null
                        ? string.Empty
                        : string.Join(", ", observation.Targets.OrderBy(value => value, StringComparer.Ordinal))
                });
            }
            return result;
        }

        internal static bool IsCommercialInteractionMatrixComplete(
            IEnumerable<ESWorkbenchVisualInteractionCheck> checks)
        {
            if (checks == null) return false;
            ESWorkbenchVisualInteractionCheck[] values = checks
                .Where(value => value != null)
                .ToArray();
            if (values.Length != CommercialInteractionCheckIds.Length) return false;
            return CommercialInteractionCheckIds.All(requiredId =>
            {
                int index = Array.IndexOf(CommercialInteractionCheckIds, requiredId);
                ESWorkbenchVisualInteractionCheck[] matches = values.Where(
                    candidate => string.Equals(candidate.checkId, requiredId, StringComparison.Ordinal))
                    .ToArray();
                if (matches.Length != 1) return false;
                ESWorkbenchVisualInteractionCheck value = matches[0];
                return value != null
                    && value.passed
                    && value.observationCount >= CommercialInteractionMinimumCounts[index]
                    && value.requiredObservationCount >= CommercialInteractionMinimumCounts[index]
                    && value.evidenceSource.StartsWith("ui-event/", StringComparison.Ordinal)
                    && !string.IsNullOrWhiteSpace(value.observedUtc);
            });
        }

        public static bool TryRevealDirectory(string path) => TryOpenGuarded(path, true);

        public static bool TryOpenFile(string path) => TryOpenGuarded(path, false);

        internal static string BuildScenarioId(
            ESWorkbenchVisualEnvironment environment,
            ESWorkbenchResponsiveTier tier)
        {
            int scalePercent = Mathf.RoundToInt(Mathf.Max(1f, environment.PixelsPerPoint) * 100f);
            return tier.ToString().ToLowerInvariant()
                + "-" + (environment.Theme == ESWorkbenchVisualTheme.Dark ? "dark" : "light")
                + "-" + scalePercent
                + (environment.LongChineseContent ? "-long-cn" : string.Empty);
        }

        internal static bool HasCurrentArtifactIdentity(
            ESWorkbenchVisualEvidenceManifest manifest,
            string expectedWorkbenchId = null,
            string expectedSourceAssetGuid = null)
        {
            if (manifest == null
                || manifest.schemaVersion < 5
                || string.IsNullOrWhiteSpace(manifest.workbenchId)
                || !string.Equals(
                    manifest.unityVersion, Application.unityVersion, StringComparison.Ordinal)
                || !string.Equals(
                    manifest.assemblyModuleVersionId,
                    CurrentAssemblyModuleVersionId,
                    StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(manifest.sourceAssetPath)
                || string.IsNullOrWhiteSpace(manifest.sourceAssetGuid))
                return false;
            if (!string.IsNullOrWhiteSpace(expectedWorkbenchId)
                && !string.Equals(
                    manifest.workbenchId, expectedWorkbenchId, StringComparison.Ordinal))
                return false;
            return string.IsNullOrWhiteSpace(expectedSourceAssetGuid)
                || string.Equals(
                    manifest.sourceAssetGuid, expectedSourceAssetGuid, StringComparison.Ordinal);
        }

        internal static ESWorkbenchVisualEvidenceIndex MergeIndex(
            ESWorkbenchVisualEvidenceIndex index,
            ESWorkbenchVisualEvidenceManifest manifest)
        {
            index ??= new ESWorkbenchVisualEvidenceIndex();
            index.schemaVersion = 3;
            index.entries ??= new List<ESWorkbenchVisualEvidenceIndexEntry>();
            if (!HasCurrentArtifactIdentity(manifest) || !manifest.scenarioMatch
                || !manifest.layoutContractPassed
                || !manifest.captureGeometryTrusted
                || !manifest.layoutProbePassed
                || !IsLayoutProbeSnapshotComplete(manifest.layoutProbes)
                || !manifest.pixelVariancePassed
                || !manifest.interactionMatrixPassed
                || !IsCommercialInteractionMatrixComplete(manifest.interactionChecks)
                || string.IsNullOrWhiteSpace(manifest.expectedScenarioId)
                || !string.Equals(
                    manifest.expectedScenarioId, manifest.scenarioId, StringComparison.Ordinal))
                return index;

            index.entries.RemoveAll(value => value == null
                || (string.Equals(
                        value.scenarioId, manifest.expectedScenarioId, StringComparison.Ordinal)
                    && string.Equals(
                        value.sourceAssetGuid, manifest.sourceAssetGuid, StringComparison.Ordinal)));
            index.entries.Add(new ESWorkbenchVisualEvidenceIndexEntry
            {
                scenarioId = manifest.expectedScenarioId,
                sourceAssetGuid = manifest.sourceAssetGuid,
                assemblyModuleVersionId = manifest.assemblyModuleVersionId,
                runId = manifest.runId,
                capturedUtc = manifest.capturedUtc,
                runDirectory = Path.GetDirectoryName(manifest.manifestAbsolutePath) ?? string.Empty,
                screenshotPath = manifest.screenshotAbsolutePath,
                manifestPath = manifest.manifestAbsolutePath
            });
            index.entries = index.entries
                .OrderBy(value => value.sourceAssetGuid, StringComparer.Ordinal)
                .ThenBy(value => value.scenarioId, StringComparer.Ordinal)
                .ToList();
            return index;
        }

        private static bool TryOpenGuarded(string path, bool directory)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            try
            {
                string full = Path.GetFullPath(path);
                if (!IsWithinRoot(full, EvidenceRoot)) return false;
                if (directory)
                {
                    if (!Directory.Exists(full)) return false;
                    EditorUtility.RevealInFinder(full);
                }
                else
                {
                    if (!File.Exists(full)) return false;
                    EditorUtility.OpenWithDefaultApp(full);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void UpdateScenarioIndex(
            string workbenchRoot,
            ESWorkbenchVisualEvidenceManifest manifest)
        {
            ESWorkbenchVisualEvidenceIndex index = MergeIndex(ReadIndex(workbenchRoot), manifest);
            WriteUtf8Atomic(
                Path.Combine(workbenchRoot, "matrix-index-v3.json"),
                JsonUtility.ToJson(index, true));
        }

        private static ESWorkbenchVisualEvidenceIndex ReadIndex(string workbenchRoot)
        {
            string path = Path.Combine(workbenchRoot, "matrix-index-v3.json");
            try
            {
                if (!File.Exists(path) || new FileInfo(path).Length > MaximumIndexBytes)
                    return new ESWorkbenchVisualEvidenceIndex();
                ESWorkbenchVisualEvidenceIndex index =
                    JsonUtility.FromJson<ESWorkbenchVisualEvidenceIndex>(
                        File.ReadAllText(path, Encoding.UTF8));
                if (index == null || index.schemaVersion < 3)
                    return new ESWorkbenchVisualEvidenceIndex();
                index.entries ??= new List<ESWorkbenchVisualEvidenceIndexEntry>();
                return index;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("ES Workbench 视觉证据索引读取失败，已忽略损坏索引：" + exception.Message);
                return new ESWorkbenchVisualEvidenceIndex();
            }
        }

        private static bool IsValidEntry(
            ESWorkbenchVisualEvidenceIndexEntry entry,
            string workbenchRoot,
            string expectedWorkbenchId,
            string expectedScenarioId,
            string expectedSourceAssetGuid)
        {
            if (entry == null
                || !string.Equals(entry.scenarioId, expectedScenarioId, StringComparison.Ordinal)
                || !string.Equals(
                    entry.sourceAssetGuid, expectedSourceAssetGuid, StringComparison.Ordinal)
                || !string.Equals(
                    entry.assemblyModuleVersionId,
                    CurrentAssemblyModuleVersionId,
                    StringComparison.OrdinalIgnoreCase)
                || !IsWithinRoot(entry.runDirectory, workbenchRoot)
                || !IsWithinRoot(entry.screenshotPath, workbenchRoot)
                || !IsWithinRoot(entry.manifestPath, workbenchRoot)
                || !File.Exists(entry.screenshotPath)
                || !File.Exists(entry.manifestPath)) return false;
            string currentSourcePath = AssetDatabase.GUIDToAssetPath(expectedSourceAssetGuid);
            if (string.IsNullOrWhiteSpace(currentSourcePath)
                || AssetDatabase.LoadMainAssetAtPath(currentSourcePath) == null) return false;

            try
            {
                if (new FileInfo(entry.manifestPath).Length > MaximumManifestBytes) return false;
            }
            catch
            {
                return false;
            }
            ESWorkbenchVisualEvidenceManifest manifest;
            try
            {
                manifest = JsonUtility.FromJson<ESWorkbenchVisualEvidenceManifest>(
                    File.ReadAllText(entry.manifestPath, Encoding.UTF8));
            }
            catch
            {
                return false;
            }
            if (manifest == null) return false;
            return HasCurrentArtifactIdentity(
                    manifest, expectedWorkbenchId, expectedSourceAssetGuid)
                && manifest.scenarioMatch
                && manifest.layoutContractPassed
                && manifest.captureGeometryTrusted
                && manifest.layoutProbePassed
                && IsLayoutProbeSnapshotComplete(manifest.layoutProbes)
                && manifest.pixelVariancePassed
                && manifest.interactionMatrixPassed
                && IsCommercialInteractionMatrixComplete(manifest.interactionChecks)
                && string.Equals(
                    manifest.sourceAssetGuid, entry.sourceAssetGuid, StringComparison.Ordinal)
                && string.Equals(
                    manifest.assemblyModuleVersionId,
                    entry.assemblyModuleVersionId,
                    StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(manifest.sourceAssetPath)
                && string.Equals(
                    manifest.expectedScenarioId, expectedScenarioId, StringComparison.Ordinal)
                && string.Equals(
                    manifest.scenarioId, expectedScenarioId, StringComparison.Ordinal)
                && string.Equals(
                    Path.GetFullPath(manifest.screenshotAbsolutePath),
                    Path.GetFullPath(entry.screenshotPath),
                    StringComparison.OrdinalIgnoreCase)
                && string.Equals(
                    Path.GetFullPath(manifest.manifestAbsolutePath),
                    Path.GetFullPath(entry.manifestPath),
                    StringComparison.OrdinalIgnoreCase);
        }

        private static string SanitizePathSegment(string value)
        {
            string source = string.IsNullOrWhiteSpace(value) ? "workbench" : value.Trim();
            char[] invalid = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder(source.Length);
            for (int i = 0; i < source.Length; i++)
                builder.Append(Array.IndexOf(invalid, source[i]) >= 0 ? '_' : source[i]);
            return builder.ToString();
        }

        private static bool IsWithinRoot(string path, string root)
        {
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(root)) return false;
            string fullPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return string.Equals(fullPath, fullRoot, StringComparison.OrdinalIgnoreCase)
                || fullPath.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || fullPath.StartsWith(fullRoot + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }

        private static string ToProjectPath(string projectRoot, string path)
        {
            string fullRoot = Path.GetFullPath(projectRoot)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string fullPath = Path.GetFullPath(path);
            if (!IsWithinRoot(fullPath, fullRoot)) return string.Empty;
            return fullPath.Substring(fullRoot.Length + 1).Replace('\\', '/');
        }

        private static void WriteUtf8Atomic(string path, string content)
        {
            string directory = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(directory)) throw new InvalidOperationException("输出目录无效。");
            Directory.CreateDirectory(directory);
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, content ?? string.Empty, new UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
        }

        private static ESWorkbenchVisualEvidenceCaptureResult Failure(string message)
        {
            return new ESWorkbenchVisualEvidenceCaptureResult(false, message, string.Empty, string.Empty, string.Empty);
        }
    }
}
#endif
