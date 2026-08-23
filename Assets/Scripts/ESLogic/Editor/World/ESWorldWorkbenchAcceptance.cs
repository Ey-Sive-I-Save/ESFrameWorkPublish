#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

namespace ES
{
    [Serializable]
    internal sealed class ESWorldAcceptanceStateEvidence
    {
        public string draftHash = string.Empty;
        public string sourceHash = string.Empty;
        public int changeCount;
        public bool dirty;
        public bool externalConflict;
        public bool consistencyPassed;
        public string prefabGuid = string.Empty;
    }

    [Serializable]
    internal sealed class ESWorldAcceptanceCheckEvidence
    {
        public bool executed;
        public bool passed;
        public string summary = string.Empty;
        public string diagnostic = string.Empty;
        public int activeSameSourceSessionCount;
        public bool externalConflictObserved;
        public string conflictOwnerSessionId = string.Empty;
        public bool writeAttempted;
        public bool writeRejected;
        public bool localDraftPreserved;
        public bool sourcePreserved;
        public string writeResult = string.Empty;
        public string sourceAssetPath = string.Empty;
        public string sourceAssetGuid = string.Empty;
        public string ownerSessionId = string.Empty;
        public List<string> activeOwnerSessionIds = new List<string>();
        public ESWorldAcceptanceStateEvidence initialState;
        public ESWorldAcceptanceStateEvidence changedState;
        public ESWorldAcceptanceStateEvidence undoState;
        public ESWorldAcceptanceStateEvidence redoState;
        public ESWorldAcceptanceStateEvidence restoredState;
        public ESWorldAcceptanceStateEvidence reopenedState;
        public ESWorldAcceptanceStateEvidence conflictState;
        public ESWorldAcceptanceStateEvidence rejectedState;
        public string draftHashBeforeReject = string.Empty;
        public string draftHashAfterReject = string.Empty;
        public string sourceHashBeforeReject = string.Empty;
        public string sourceHashAfterReject = string.Empty;
    }

    [Serializable]
    internal sealed class ESWorldAcceptancePreviewSample
    {
        public int iteration;
        public double elapsedSeconds;
        public string phase = string.Empty;
        public int activeScopeCount;
        public int activeRenderContextCount;
        public int activeResourceScopeCount;
        public int activeModelGroupCount;
        public int activeTemporaryObjectCount;
        public int activeRenderTextureCount;
        public long activeRenderTexturePixels;
        public long estimatedRenderTextureBytes;
        public long totalScopeRegistrations;
        public long totalScopeReleases;
        public long outstandingScopeCount;
        public int cleanupFailureCount;
        public long totalAllocatedMemory;
        public long monoUsedMemory;
        public int generation0Collections;
    }

    [Serializable]
    internal sealed class ESWorldAcceptancePreviewEvidence
    {
        public bool executed;
        public bool passed;
        public bool cancelled;
        public int requestedIterations;
        public int completedIterations;
        public double minimumDurationSeconds;
        public double durationSeconds;
        public bool iterationRequirementPassed;
        public bool durationRequirementPassed;
        public bool lifecycleTrendStable;
        public int trendSampleCount;
        public List<ESWorldAcceptancePreviewSample> samples =
            new List<ESWorldAcceptancePreviewSample>();
        public string beforeSummary = string.Empty;
        public string afterSummary = string.Empty;
        public long totalAllocatedMemoryBefore;
        public long totalAllocatedMemoryAfter;
        public long monoUsedMemoryBefore;
        public long monoUsedMemoryAfter;
        public int generation0CollectionsBefore;
        public int generation0CollectionsAfter;
        public int activeScopeDelta;
        public int activeRenderContextDelta;
        public int activeResourceScopeDelta;
        public int activeModelGroupDelta;
        public int activeTemporaryObjectDelta;
        public int activeRenderTextureDelta;
        public long activeRenderTexturePixelDelta;
        public long estimatedRenderTextureByteDelta;
        public long totalScopeRegistrationDelta;
        public long totalScopeReleaseDelta;
        public int cleanupFailureDelta;
        public bool previewSceneObserved;
        public bool cameraObserved;
        public int peakActiveScopeCount;
        public int peakActiveRenderContextCount;
        public int peakActiveModelGroupCount;
        public int peakActiveTemporaryObjectCount;
        public int peakActiveRenderTextureCount;
        public long peakActiveRenderTexturePixels;
        public long peakEstimatedRenderTextureBytes;
        public bool memoryProfilerCaptureAvailable;
        public bool memoryProfilerCaptureSupported;
        public bool memoryProfilerSnapshotCaptured;
        public string memoryProfilerSnapshotPath = string.Empty;
        public string memoryProfilerSnapshotMessage = string.Empty;
        public string summary = string.Empty;
        public string evidenceBoundary = string.Empty;
    }

    [Serializable]
    internal sealed class ESWorldAcceptanceVisualEvidence
    {
        public int requiredScenarioCount;
        public int capturedScenarioCount;
        public bool complete;
        public string summary = string.Empty;
    }

    [Serializable]
    internal sealed class ESWorldWorkbenchAcceptanceManifest
    {
        public int schemaVersion = 7;
        public string runId = string.Empty;
        public string capturedUtc = string.Empty;
        public string projectRoot = string.Empty;
        public string unityVersion = string.Empty;
        public string assemblyModuleVersionId = string.Empty;
        public string workbenchId = "world";
        public string sourceName = string.Empty;
        public string sourceAssetPath = string.Empty;
        public string sourceAssetGuid = string.Empty;
        public string executionMode = "Unity Editor 显式用户操作";
        public ESWorldAcceptanceCheckEvidence currentSession =
            new ESWorldAcceptanceCheckEvidence();
        public ESWorldAcceptanceCheckEvidence undoRedoRecovery =
            new ESWorldAcceptanceCheckEvidence();
        public ESWorldAcceptanceCheckEvidence liveWindowUndoRedo =
            new ESWorldAcceptanceCheckEvidence();
        public ESWorldAcceptanceCheckEvidence multiWindowConflict =
            new ESWorldAcceptanceCheckEvidence();
        public ESWorldAcceptanceCheckEvidence liveWindowConflict =
            new ESWorldAcceptanceCheckEvidence();
        public ESWorldAcceptancePreviewEvidence previewStress =
            new ESWorldAcceptancePreviewEvidence();
        public ESWorldAcceptanceVisualEvidence visualMatrix =
            new ESWorldAcceptanceVisualEvidence();
        public bool automatedChecksPassed;
        public bool accepted;
        public string verdict = string.Empty;
        public string evidenceBoundary = string.Empty;
        public string manifestAbsolutePath = string.Empty;
        public string manifestProjectPath = string.Empty;
    }

    [Serializable]
    internal sealed class ESWorldWorkbenchAcceptanceLatestPointer
    {
        public string runId = string.Empty;
        public string sourceAssetGuid = string.Empty;
        public string assemblyModuleVersionId = string.Empty;
        public string runDirectory = string.Empty;
        public string manifestPath = string.Empty;
    }

    internal readonly struct ESWorldWorkbenchAcceptanceResult
    {
        public ESWorldWorkbenchAcceptanceResult(
            bool success,
            bool automatedChecksPassed,
            bool accepted,
            bool cancelled,
            string message,
            string runDirectory,
            string manifestPath,
            string sourceAssetGuid = null,
            string assemblyModuleVersionId = null)
        {
            Success = success;
            AutomatedChecksPassed = automatedChecksPassed;
            Accepted = accepted;
            Cancelled = cancelled;
            Message = message ?? string.Empty;
            RunDirectory = runDirectory ?? string.Empty;
            ManifestPath = manifestPath ?? string.Empty;
            SourceAssetGuid = sourceAssetGuid ?? string.Empty;
            AssemblyModuleVersionId = assemblyModuleVersionId ?? string.Empty;
        }

        public bool Success { get; }
        public bool AutomatedChecksPassed { get; }
        public bool Accepted { get; }
        public bool Cancelled { get; }
        public string Message { get; }
        public string RunDirectory { get; }
        public string ManifestPath { get; }
        public string SourceAssetGuid { get; }
        public string AssemblyModuleVersionId { get; }
    }

    /// <summary>
    /// Explicit World acceptance workflow. It mutates only isolated HideAndDontSave specimens
    /// and the caller-owned preview draft, then records reproducible evidence under Library.
    /// </summary>
    internal static class ESWorldWorkbenchAcceptance
    {
        private const string EvidenceBoundary =
            "本清单分别记录隔离样本检查、真实窗口 Undo/Redo、真实同源窗口冲突状态和预览生命周期基线；"
            + "它不等价于人工交互矩阵、Unity Game View、Memory Profiler、Player 或发布验收。";

        private static string EvidenceRoot => Path.GetFullPath(Path.Combine(
            Directory.GetCurrentDirectory(), "Library", "ESWorkbench", "Acceptance", "world"));

        internal static string CurrentAssemblyModuleVersionId =>
            typeof(ESWorldWorkbenchAcceptance).Assembly.ManifestModule.ModuleVersionId.ToString("D");

        public static ESWorldWorkbenchAcceptanceResult Execute(
            ESWorldMapAsset draft,
            ESWorldEditSession currentSession,
            IReadOnlyList<ESWorkbenchVisualValidationScenario> visualScenarios,
            int previewIterations,
            bool showProgress,
            double previewMinimumDurationSeconds = 0d)
        {
            string projectRoot = Path.GetFullPath(Directory.GetCurrentDirectory());
            string runId = DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ")
                + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            string runDirectory = Path.GetFullPath(Path.Combine(EvidenceRoot, runId));
            string manifestPath = Path.Combine(runDirectory, "manifest.json");
            var manifest = new ESWorldWorkbenchAcceptanceManifest
            {
                runId = runId,
                capturedUtc = DateTime.UtcNow.ToString("O"),
                projectRoot = projectRoot,
                unityVersion = Application.unityVersion,
                assemblyModuleVersionId = CurrentAssemblyModuleVersionId,
                sourceName = currentSession?.Source == null
                    ? draft == null ? string.Empty : draft.name
                    : currentSession.Source.name,
                evidenceBoundary = EvidenceBoundary,
                manifestAbsolutePath = Path.GetFullPath(manifestPath),
                manifestProjectPath = ToProjectPath(projectRoot, manifestPath)
            };
            ESWorldMapAsset formalSource = currentSession?.Source;
            manifest.sourceAssetPath = formalSource == null
                ? string.Empty : AssetDatabase.GetAssetPath(formalSource);
            manifest.sourceAssetGuid = string.IsNullOrEmpty(manifest.sourceAssetPath)
                ? string.Empty : AssetDatabase.AssetPathToGUID(manifest.sourceAssetPath);

            manifest.currentSession = CaptureCurrentSession(currentSession);
            manifest.undoRedoRecovery = RunUndoRedoRecovery(draft);
            manifest.liveWindowUndoRedo = CaptureLiveWindowUndoRedo(currentSession);
            manifest.multiWindowConflict = RunMultiWindowConflict(draft);
            manifest.liveWindowConflict = CaptureLiveWindowConflict(currentSession);
            manifest.previewStress = RunPreviewStress(
                draft,
                previewIterations,
                showProgress,
                previewMinimumDurationSeconds);
            int required = visualScenarios?.Count ?? 0;
            int captured = ESWorkbenchVisualEvidenceCapture.CountCapturedScenarios(
                "world", visualScenarios, manifest.sourceAssetGuid);
            manifest.visualMatrix = new ESWorldAcceptanceVisualEvidence
            {
                requiredScenarioCount = required,
                capturedScenarioCount = captured,
                complete = required > 0 && captured == required,
                summary = required == 0
                    ? "没有可用的视觉验收矩阵。"
                    : "真实窗口截图与交互确认 " + captured + "/" + required
            };
            manifest.automatedChecksPassed = manifest.currentSession.passed
                && manifest.undoRedoRecovery.passed
                && manifest.liveWindowUndoRedo.passed
                && manifest.multiWindowConflict.passed
                && manifest.previewStress.passed;
            manifest.accepted = manifest.automatedChecksPassed
                && manifest.liveWindowConflict.passed
                && manifest.visualMatrix.complete
                && manifest.previewStress.memoryProfilerSnapshotCaptured;
            manifest.verdict = manifest.accepted
                ? "当前清单覆盖项全部通过。"
                : manifest.automatedChecksPassed
                    ? manifest.liveWindowConflict.passed
                        ? "专项自动检查通过；视觉矩阵或 Memory Profiler 证据仍未闭环。"
                        : "专项自动检查通过；尚未取得真实同源协作窗口冲突证据。"
                    : "至少一项专项自动检查失败或被取消。";

            try
            {
                if (!IsWithinRoot(runDirectory, EvidenceRoot))
                    throw new InvalidOperationException("验收输出路径未通过项目 Library 安全边界检查。");
                WriteUtf8Atomic(manifestPath, JsonUtility.ToJson(manifest, true));
                var latest = new ESWorldWorkbenchAcceptanceLatestPointer
                {
                    runId = runId,
                    sourceAssetGuid = manifest.sourceAssetGuid,
                    assemblyModuleVersionId = manifest.assemblyModuleVersionId,
                    runDirectory = runDirectory,
                    manifestPath = Path.GetFullPath(manifestPath)
                };
                string latestPointerPath = ResolveLatestPointerPath(manifest.sourceAssetGuid);
                if (!string.IsNullOrWhiteSpace(latestPointerPath))
                    WriteUtf8Atomic(latestPointerPath, JsonUtility.ToJson(latest, true));
                return new ESWorldWorkbenchAcceptanceResult(
                    true,
                    manifest.automatedChecksPassed,
                    manifest.accepted,
                    manifest.previewStress.cancelled,
                    manifest.verdict,
                    runDirectory,
                    latest.manifestPath,
                    manifest.sourceAssetGuid,
                    manifest.assemblyModuleVersionId);
            }
            catch (Exception exception)
            {
                return new ESWorldWorkbenchAcceptanceResult(
                    false, false, false, manifest.previewStress.cancelled,
                    "专项验收证据写入失败：" + exception.Message,
                    runDirectory, manifestPath);
            }
        }

        internal static void CaptureStateEvidenceForTest(
            ESWorldMapAsset draft,
            ESWorldEditSession currentSession,
            out ESWorldAcceptanceCheckEvidence current,
            out ESWorldAcceptanceCheckEvidence undoRedo,
            out ESWorldAcceptanceCheckEvidence conflict)
        {
            current = CaptureCurrentSession(currentSession);
            undoRedo = RunUndoRedoRecovery(draft);
            conflict = RunMultiWindowConflict(draft);
        }

        internal static ESWorldAcceptanceCheckEvidence CaptureLiveWindowConflictForTest(
            ESWorldEditSession currentSession)
        {
            return CaptureLiveWindowConflict(currentSession);
        }

        internal static ESWorldAcceptanceCheckEvidence CaptureLiveWindowUndoRedoForTest(
            ESWorldEditSession currentSession)
        {
            return CaptureLiveWindowUndoRedo(currentSession);
        }

        internal static ESWorldAcceptancePreviewEvidence RunPreviewStress(
            ESWorldMapAsset draft,
            int iterations,
            bool showProgress,
            double minimumDurationSeconds = 0d)
        {
            var evidence = new ESWorldAcceptancePreviewEvidence
            {
                executed = true,
                requestedIterations = Mathf.Clamp(iterations, 1, 600),
                minimumDurationSeconds = Math.Max(0d, minimumDurationSeconds),
                memoryProfilerCaptureAvailable = false,
                memoryProfilerCaptureSupported = IsMemoryProfilerCaptureSupported(out _),
                memoryProfilerSnapshotCaptured = false,
                evidenceBoundary =
                    "生命周期趋势只判断活动预览资源和未决 Scope 是否持续增长；"
                    + "内存数值来自 UnityEngine.Profiling.Profiler 的过程采样与运行前后快照，"
                    + "只作观测。未生成 Memory Profiler 快照，不能据此宣称无泄漏或无 GC。"
            };
            if (draft == null)
            {
                evidence.summary = "未绑定世界草稿，无法运行预览压力检查。";
                return evidence;
            }

            ESEditorPreviewDiagnosticsSnapshot before =
                ESEditorPreviewLifecycleHub.CaptureDiagnosticsSnapshot();
            evidence.beforeSummary = before.ToSummary();
            evidence.peakActiveScopeCount = before.ActiveScopeCount;
            evidence.peakActiveRenderContextCount = before.ActiveRenderContextCount;
            evidence.peakActiveModelGroupCount = before.ActiveModelGroupCount;
            evidence.peakActiveTemporaryObjectCount = before.ActiveTemporaryObjectCount;
            evidence.peakActiveRenderTextureCount = before.ActiveRenderTextureCount;
            evidence.peakActiveRenderTexturePixels = before.ActiveRenderTexturePixels;
            evidence.peakEstimatedRenderTextureBytes = before.EstimatedRenderTextureBytes;
            evidence.totalAllocatedMemoryBefore = Profiler.GetTotalAllocatedMemoryLong();
            evidence.monoUsedMemoryBefore = Profiler.GetMonoUsedSizeLong();
            evidence.generation0CollectionsBefore = GC.CollectionCount(0);
            double started = EditorApplication.timeSinceStartup;
            AddPreviewSample(evidence, 0, 0d, "Before", before);
            ESWorldAuthoringViewport viewport = null;
            string failure = string.Empty;
            double nextTimedSampleSeconds = 0d;
            try
            {
                viewport = new ESWorldAuthoringViewport(_ => { }, null, true);
                viewport.Bind(draft, true);
                while (evidence.completedIterations < evidence.requestedIterations
                    || EditorApplication.timeSinceStartup - started
                        < evidence.minimumDurationSeconds)
                {
                    double elapsedBeforeIteration = EditorApplication.timeSinceStartup - started;
                    float iterationProgress = Mathf.Clamp01(
                        evidence.completedIterations / (float)evidence.requestedIterations);
                    float durationProgress = evidence.minimumDurationSeconds <= 0d
                        ? 1f
                        : Mathf.Clamp01((float)(elapsedBeforeIteration
                            / evidence.minimumDurationSeconds));
                    if (showProgress && EditorUtility.DisplayCancelableProgressBar(
                        "ES World 商业验收",
                        "重复重建 PreviewScene、Camera、临时对象与 RenderTexture "
                        + evidence.completedIterations + "/" + evidence.requestedIterations
                        + " · " + elapsedBeforeIteration.ToString("0.0") + "/"
                        + evidence.minimumDurationSeconds.ToString("0.0") + " 秒",
                        Mathf.Min(iterationProgress, durationProgress)))
                    {
                        evidence.cancelled = true;
                        break;
                    }

                    viewport.Rebuild(false);
                    int iteration = evidence.completedIterations;
                    ESEditorPreviewRenderContext context = viewport.PreviewContextForTest;
                    if (context == null || !context.IsReady)
                        throw new InvalidOperationException(
                            "第 " + (iteration + 1) + " 次重建没有得到可用预览上下文。");
                    evidence.previewSceneObserved |= context.SceneMode == ESEditorPreviewSceneMode.PreviewScene
                        && context.PreviewSceneIsValid;
                    evidence.cameraObserved |= context.Camera != null;
                    int width = iteration % 3 == 0 ? 640 : iteration % 3 == 1 ? 960 : 1280;
                    int height = Mathf.RoundToInt(width * 9f / 16f);
                    Texture2D frame = context.Snapshot(
                        width,
                        height,
                        new ESEditorPreviewCameraPose(
                            context.GroupOrigin, 24f, 35f, 24f, 1f),
                        ESEditorPreviewQuality.Balanced,
                        "ES World Acceptance Stress Frame");
                    if (frame == null)
                        throw new InvalidOperationException(
                            "第 " + (iteration + 1) + " 次预览截图失败。");
                    UnityEngine.Object.DestroyImmediate(frame);
                    evidence.completedIterations++;
                    ESEditorPreviewDiagnosticsSnapshot current =
                        ESEditorPreviewLifecycleHub.CaptureDiagnosticsSnapshot();
                    evidence.peakActiveScopeCount = Math.Max(
                        evidence.peakActiveScopeCount, current.ActiveScopeCount);
                    evidence.peakActiveRenderContextCount = Math.Max(
                        evidence.peakActiveRenderContextCount, current.ActiveRenderContextCount);
                    evidence.peakActiveModelGroupCount = Math.Max(
                        evidence.peakActiveModelGroupCount, current.ActiveModelGroupCount);
                    evidence.peakActiveTemporaryObjectCount = Math.Max(
                        evidence.peakActiveTemporaryObjectCount, current.ActiveTemporaryObjectCount);
                    evidence.peakActiveRenderTextureCount = Math.Max(
                        evidence.peakActiveRenderTextureCount, current.ActiveRenderTextureCount);
                    evidence.peakActiveRenderTexturePixels = Math.Max(
                        evidence.peakActiveRenderTexturePixels, current.ActiveRenderTexturePixels);
                    evidence.peakEstimatedRenderTextureBytes = Math.Max(
                        evidence.peakEstimatedRenderTextureBytes, current.EstimatedRenderTextureBytes);
                    double elapsed = EditorApplication.timeSinceStartup - started;
                    bool captureEveryIteration = evidence.minimumDurationSeconds <= 0d;
                    if (captureEveryIteration
                        || evidence.completedIterations == 1
                        || evidence.completedIterations == evidence.requestedIterations
                        || elapsed >= nextTimedSampleSeconds)
                    {
                        AddPreviewSample(
                            evidence,
                            evidence.completedIterations,
                            elapsed,
                            "Running",
                            current);
                        nextTimedSampleSeconds = elapsed + 0.5d;
                    }
                }

                AddPreviewSample(
                    evidence,
                    evidence.completedIterations,
                    EditorApplication.timeSinceStartup - started,
                    "Running",
                    ESEditorPreviewLifecycleHub.CaptureDiagnosticsSnapshot());
            }
            catch (Exception exception)
            {
                failure = exception.Message;
            }
            finally
            {
                viewport?.Dispose();
                if (showProgress) EditorUtility.ClearProgressBar();
            }

            ESEditorPreviewDiagnosticsSnapshot after =
                ESEditorPreviewLifecycleHub.CaptureDiagnosticsSnapshot();
            evidence.durationSeconds = EditorApplication.timeSinceStartup - started;
            AddPreviewSample(
                evidence,
                evidence.completedIterations,
                evidence.durationSeconds,
                "AfterDispose",
                after);
            evidence.afterSummary = after.ToSummary();
            evidence.totalAllocatedMemoryAfter = Profiler.GetTotalAllocatedMemoryLong();
            evidence.monoUsedMemoryAfter = Profiler.GetMonoUsedSizeLong();
            evidence.generation0CollectionsAfter = GC.CollectionCount(0);
            evidence.activeScopeDelta = after.ActiveScopeCount - before.ActiveScopeCount;
            evidence.activeRenderContextDelta =
                after.ActiveRenderContextCount - before.ActiveRenderContextCount;
            evidence.activeResourceScopeDelta =
                after.ActiveResourceScopeCount - before.ActiveResourceScopeCount;
            evidence.activeModelGroupDelta =
                after.ActiveModelGroupCount - before.ActiveModelGroupCount;
            evidence.activeTemporaryObjectDelta =
                after.ActiveTemporaryObjectCount - before.ActiveTemporaryObjectCount;
            evidence.activeRenderTextureDelta =
                after.ActiveRenderTextureCount - before.ActiveRenderTextureCount;
            evidence.activeRenderTexturePixelDelta =
                after.ActiveRenderTexturePixels - before.ActiveRenderTexturePixels;
            evidence.estimatedRenderTextureByteDelta =
                after.EstimatedRenderTextureBytes - before.EstimatedRenderTextureBytes;
            evidence.totalScopeRegistrationDelta =
                after.TotalScopeRegistrations - before.TotalScopeRegistrations;
            evidence.totalScopeReleaseDelta =
                after.TotalScopeReleases - before.TotalScopeReleases;
            evidence.cleanupFailureDelta =
                after.CleanupFailureCount - before.CleanupFailureCount;
            bool returnedToBaseline = evidence.activeScopeDelta == 0
                && evidence.activeRenderContextDelta == 0
                && evidence.activeResourceScopeDelta == 0
                && evidence.activeModelGroupDelta == 0
                && evidence.activeTemporaryObjectDelta == 0
                && evidence.activeRenderTextureDelta == 0
                && evidence.activeRenderTexturePixelDelta == 0
                && evidence.estimatedRenderTextureByteDelta == 0
                && evidence.totalScopeRegistrationDelta > 0
                && evidence.totalScopeRegistrationDelta == evidence.totalScopeReleaseDelta
                && evidence.cleanupFailureDelta == 0;
            MeetsPreviewStressRequirements(
                evidence.requestedIterations,
                evidence.completedIterations,
                evidence.minimumDurationSeconds,
                evidence.durationSeconds,
                out evidence.iterationRequirementPassed,
                out evidence.durationRequirementPassed);
            evidence.lifecycleTrendStable = EvaluateLifecycleTrend(
                evidence.samples,
                out int trendSampleCount);
            evidence.trendSampleCount = trendSampleCount;
            evidence.passed = string.IsNullOrEmpty(failure)
                && !evidence.cancelled
                && evidence.iterationRequirementPassed
                && evidence.durationRequirementPassed
                && evidence.lifecycleTrendStable
                && evidence.previewSceneObserved
                && evidence.cameraObserved
                && evidence.peakActiveRenderContextCount > before.ActiveRenderContextCount
                && evidence.peakActiveTemporaryObjectCount > before.ActiveTemporaryObjectCount
                && evidence.peakActiveRenderTextureCount > before.ActiveRenderTextureCount
                && returnedToBaseline;
            evidence.summary = !string.IsNullOrEmpty(failure)
                ? "预览压力检查失败：" + failure
                    : evidence.cancelled
                        ? "用户在 " + evidence.completedIterations + "/"
                        + evidence.requestedIterations + " 次时取消检查。"
                    : (evidence.passed
                        ? "预览生命周期基线与过程趋势通过"
                        : "预览压力次数、持续时间、过程趋势或最终基线未通过")
                        + " · " + evidence.completedIterations + " 次"
                        + "（要求至少 " + evidence.requestedIterations + " 次）"
                        + " · " + evidence.durationSeconds.ToString("0.00") + " 秒"
                        + "（要求至少 " + evidence.minimumDurationSeconds.ToString("0.00") + " 秒）"
                        + " · 趋势采样 " + evidence.trendSampleCount
                        + " · Scope 注册/释放 " + evidence.totalScopeRegistrationDelta
                        + "/" + evidence.totalScopeReleaseDelta
                        + " · 临时对象峰值 " + evidence.peakActiveTemporaryObjectCount
                        + " · Profiler 总分配差 "
                        + (evidence.totalAllocatedMemoryAfter
                            - evidence.totalAllocatedMemoryBefore).ToString("N0") + " B";
            return evidence;
        }

        internal static bool MeetsPreviewStressRequirementsForTest(
            int requestedIterations,
            int completedIterations,
            double minimumDurationSeconds,
            double durationSeconds)
        {
            return MeetsPreviewStressRequirements(
                requestedIterations,
                completedIterations,
                minimumDurationSeconds,
                durationSeconds,
                out _,
                out _);
        }

        private static bool MeetsPreviewStressRequirements(
            int requestedIterations,
            int completedIterations,
            double minimumDurationSeconds,
            double durationSeconds,
            out bool iterationPassed,
            out bool durationPassed)
        {
            iterationPassed = completedIterations >= Math.Max(1, requestedIterations);
            durationPassed = durationSeconds >= Math.Max(0d, minimumDurationSeconds);
            return iterationPassed && durationPassed;
        }

        private static void AddPreviewSample(
            ESWorldAcceptancePreviewEvidence evidence,
            int iteration,
            double elapsedSeconds,
            string phase,
            ESEditorPreviewDiagnosticsSnapshot snapshot)
        {
            if (evidence?.samples == null) return;
            evidence.samples.Add(new ESWorldAcceptancePreviewSample
            {
                iteration = iteration,
                elapsedSeconds = Math.Max(0d, elapsedSeconds),
                phase = phase ?? string.Empty,
                activeScopeCount = snapshot.ActiveScopeCount,
                activeRenderContextCount = snapshot.ActiveRenderContextCount,
                activeResourceScopeCount = snapshot.ActiveResourceScopeCount,
                activeModelGroupCount = snapshot.ActiveModelGroupCount,
                activeTemporaryObjectCount = snapshot.ActiveTemporaryObjectCount,
                activeRenderTextureCount = snapshot.ActiveRenderTextureCount,
                activeRenderTexturePixels = snapshot.ActiveRenderTexturePixels,
                estimatedRenderTextureBytes = snapshot.EstimatedRenderTextureBytes,
                totalScopeRegistrations = snapshot.TotalScopeRegistrations,
                totalScopeReleases = snapshot.TotalScopeReleases,
                outstandingScopeCount = snapshot.TotalScopeRegistrations
                    - snapshot.TotalScopeReleases,
                cleanupFailureCount = snapshot.CleanupFailureCount,
                totalAllocatedMemory = Profiler.GetTotalAllocatedMemoryLong(),
                monoUsedMemory = Profiler.GetMonoUsedSizeLong(),
                generation0Collections = GC.CollectionCount(0)
            });
        }

        private static bool EvaluateLifecycleTrend(
            IReadOnlyList<ESWorldAcceptancePreviewSample> samples,
            out int trendSampleCount)
        {
            List<ESWorldAcceptancePreviewSample> running = samples?
                .Where(sample => sample != null
                    && string.Equals(sample.phase, "Running", StringComparison.Ordinal))
                .ToList() ?? new List<ESWorldAcceptancePreviewSample>();
            trendSampleCount = running.Count;
            if (running.Count == 0) return false;
            if (running.Count == 1) return true;

            ESWorldAcceptancePreviewSample first = running[0];
            ESWorldAcceptancePreviewSample last = running[running.Count - 1];
            int split = Math.Max(1, running.Count / 2);
            List<ESWorldAcceptancePreviewSample> early = running.Take(split).ToList();
            List<ESWorldAcceptancePreviewSample> late = running.Skip(split).ToList();
            if (late.Count == 0) late.Add(last);
            return late.Max(sample => sample.activeScopeCount)
                    <= early.Max(sample => sample.activeScopeCount)
                && late.Max(sample => sample.activeRenderContextCount)
                    <= early.Max(sample => sample.activeRenderContextCount)
                && late.Max(sample => sample.activeResourceScopeCount)
                    <= early.Max(sample => sample.activeResourceScopeCount)
                && late.Max(sample => sample.activeModelGroupCount)
                    <= early.Max(sample => sample.activeModelGroupCount)
                && late.Max(sample => sample.activeTemporaryObjectCount)
                    <= early.Max(sample => sample.activeTemporaryObjectCount)
                && late.Max(sample => sample.activeRenderTextureCount)
                    <= early.Max(sample => sample.activeRenderTextureCount)
                && late.Max(sample => sample.outstandingScopeCount)
                    <= early.Max(sample => sample.outstandingScopeCount)
                && last.activeScopeCount <= first.activeScopeCount
                && last.activeRenderContextCount <= first.activeRenderContextCount
                && last.activeResourceScopeCount <= first.activeResourceScopeCount
                && last.activeModelGroupCount <= first.activeModelGroupCount
                && last.activeTemporaryObjectCount <= first.activeTemporaryObjectCount
                && last.activeRenderTextureCount <= first.activeRenderTextureCount
                && last.outstandingScopeCount <= first.outstandingScopeCount
                && last.cleanupFailureCount <= first.cleanupFailureCount;
        }

        internal static bool HasCurrentArtifactIdentity(
            ESWorldWorkbenchAcceptanceManifest manifest,
            string expectedSourceAssetGuid = null)
        {
            if (manifest == null
                || manifest.schemaVersion < 7
                || !string.Equals(manifest.workbenchId, "world", StringComparison.Ordinal)
                || !string.Equals(
                    manifest.unityVersion, Application.unityVersion, StringComparison.Ordinal)
                || !string.Equals(
                    manifest.assemblyModuleVersionId,
                    CurrentAssemblyModuleVersionId,
                    StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(manifest.sourceAssetPath)
                || string.IsNullOrWhiteSpace(manifest.sourceAssetGuid))
                return false;
            return string.IsNullOrWhiteSpace(expectedSourceAssetGuid)
                || string.Equals(
                    manifest.sourceAssetGuid, expectedSourceAssetGuid, StringComparison.Ordinal);
        }

        public static bool TryGetLatest(
            ESWorldMapAsset expectedSource,
            out ESWorldWorkbenchAcceptanceResult result)
        {
            result = default;
            string expectedSourcePath = expectedSource == null
                ? string.Empty : AssetDatabase.GetAssetPath(expectedSource);
            string expectedSourceGuid = string.IsNullOrWhiteSpace(expectedSourcePath)
                ? string.Empty : AssetDatabase.AssetPathToGUID(expectedSourcePath);
            if (string.IsNullOrWhiteSpace(expectedSourceGuid)) return false;
            try
            {
                string pointerPath = ResolveLatestPointerPath(expectedSourceGuid);
                if (string.IsNullOrWhiteSpace(pointerPath)) return false;
                if (!File.Exists(pointerPath)) return false;
                ESWorldWorkbenchAcceptanceLatestPointer latest =
                    JsonUtility.FromJson<ESWorldWorkbenchAcceptanceLatestPointer>(
                        File.ReadAllText(pointerPath, Encoding.UTF8));
                if (latest == null
                    || !IsWithinRoot(latest.runDirectory, EvidenceRoot)
                    || !IsWithinRoot(latest.manifestPath, EvidenceRoot)
                    || !File.Exists(latest.manifestPath)) return false;
                ESWorldWorkbenchAcceptanceManifest manifest =
                    JsonUtility.FromJson<ESWorldWorkbenchAcceptanceManifest>(
                        File.ReadAllText(latest.manifestPath, Encoding.UTF8));
                if (!HasCurrentArtifactIdentity(manifest, expectedSourceGuid)
                    || !string.Equals(
                        latest.sourceAssetGuid, expectedSourceGuid, StringComparison.Ordinal)
                    || !string.Equals(
                        latest.assemblyModuleVersionId,
                        CurrentAssemblyModuleVersionId,
                        StringComparison.OrdinalIgnoreCase)) return false;
                result = new ESWorldWorkbenchAcceptanceResult(
                    true,
                    manifest.automatedChecksPassed,
                    manifest.accepted,
                    manifest.previewStress?.cancelled == true,
                    manifest.verdict,
                    latest.runDirectory,
                    latest.manifestPath,
                    manifest.sourceAssetGuid,
                    manifest.assemblyModuleVersionId);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string ResolveLatestPointerPath(string sourceAssetGuid)
        {
            if (string.IsNullOrWhiteSpace(sourceAssetGuid)
                || sourceAssetGuid.Length != 32
                || sourceAssetGuid.Any(value => !Uri.IsHexDigit(value)))
                return string.Empty;
            return Path.Combine(
                EvidenceRoot,
                "latest-" + sourceAssetGuid.ToLowerInvariant() + ".json");
        }

        public static bool IsMemoryProfilerCaptureSupported(out string message)
        {
            Type profilerType = ResolveMemoryProfilerType();
            if (profilerType == null)
            {
                message = "当前项目未加载 Memory Profiler 快照 API；普通 Profiler 数值不能替代 .snap。";
                return false;
            }
            MethodInfo method = ResolveTakeSnapshotMethod(profilerType);
            if (method == null)
            {
                message = "已检测到 Memory Profiler，但没有找到兼容的 TakeSnapshot API。";
                return false;
            }
            message = "Memory Profiler 快照 API 可用。";
            return true;
        }

        public static bool TryCaptureMemoryProfilerSnapshot(
            ESWorldWorkbenchAcceptanceResult acceptance,
            Action<bool, string, ESWorldWorkbenchAcceptanceResult> completed)
        {
            if (!acceptance.Success
                || string.IsNullOrWhiteSpace(acceptance.SourceAssetGuid)
                || !string.Equals(
                    acceptance.AssemblyModuleVersionId,
                    CurrentAssemblyModuleVersionId,
                    StringComparison.OrdinalIgnoreCase)
                || !IsWithinRoot(acceptance.RunDirectory, EvidenceRoot)
                || !IsWithinRoot(acceptance.ManifestPath, EvidenceRoot)
                || !Directory.Exists(acceptance.RunDirectory)
                || !File.Exists(acceptance.ManifestPath))
            {
                completed?.Invoke(false, "最近一次验收证据不存在或未通过路径边界检查。", acceptance);
                return false;
            }
            try
            {
                ESWorldWorkbenchAcceptanceManifest manifest =
                    JsonUtility.FromJson<ESWorldWorkbenchAcceptanceManifest>(
                        File.ReadAllText(acceptance.ManifestPath, Encoding.UTF8));
                if (!HasCurrentArtifactIdentity(manifest, acceptance.SourceAssetGuid))
                {
                    completed?.Invoke(false, "最近一次验收证据不属于当前 Source 或当前程序集。", acceptance);
                    return false;
                }
            }
            catch (Exception exception)
            {
                completed?.Invoke(false, "最近一次验收清单无法重读：" + exception.Message, acceptance);
                return false;
            }

            Type profilerType = ResolveMemoryProfilerType();
            MethodInfo method = ResolveTakeSnapshotMethod(profilerType);
            if (method == null)
            {
                completed?.Invoke(false,
                    "Memory Profiler 快照 API 不可用；请安装与当前 Unity 版本匹配的 Memory Profiler 包。",
                    acceptance);
                return false;
            }

            string snapshotPath = Path.GetFullPath(Path.Combine(
                acceptance.RunDirectory,
                "world-memory.snap"));
            if (!IsWithinRoot(snapshotPath, acceptance.RunDirectory))
            {
                completed?.Invoke(false, "Memory Profiler 快照路径未通过验收目录边界检查。", acceptance);
                return false;
            }

            try
            {
                Action<string, bool> callback = (path, success) =>
                {
                    EditorApplication.delayCall += () =>
                    {
                        string actualPath = string.IsNullOrWhiteSpace(path)
                            ? snapshotPath : Path.GetFullPath(path);
                        bool captured = success
                            && IsWithinRoot(actualPath, acceptance.RunDirectory)
                            && File.Exists(actualPath);
                        ESWorldWorkbenchAcceptanceResult updated = acceptance;
                        string updateMessage = captured
                            ? "Memory Profiler 快照已生成并挂接到验收清单。"
                            : "Memory Profiler 快照未成功生成或输出路径不可信。";
                        if (TryAttachMemoryProfilerSnapshot(
                                acceptance.ManifestPath,
                                acceptance.SourceAssetGuid,
                                captured ? actualPath : string.Empty,
                                updateMessage,
                                out ESWorldWorkbenchAcceptanceResult attached))
                            updated = attached;
                        completed?.Invoke(captured, updateMessage, updated);
                    };
                };
                method.Invoke(null, BuildTakeSnapshotArguments(method, snapshotPath, callback));
                return true;
            }
            catch (Exception exception)
            {
                completed?.Invoke(false,
                    "Memory Profiler 快照启动失败：" + exception.GetBaseException().Message,
                    acceptance);
                return false;
            }
        }

        private static bool TryAttachMemoryProfilerSnapshot(
            string manifestPath,
            string expectedSourceAssetGuid,
            string snapshotPath,
            string message,
            out ESWorldWorkbenchAcceptanceResult result)
        {
            result = default;
            try
            {
                if (!IsWithinRoot(manifestPath, EvidenceRoot) || !File.Exists(manifestPath)) return false;
                ESWorldWorkbenchAcceptanceManifest manifest =
                    JsonUtility.FromJson<ESWorldWorkbenchAcceptanceManifest>(
                        File.ReadAllText(manifestPath, Encoding.UTF8));
                if (!HasCurrentArtifactIdentity(manifest, expectedSourceAssetGuid)
                    || manifest.previewStress == null) return false;
                string runDirectory = Path.GetDirectoryName(Path.GetFullPath(manifestPath));
                bool captured = !string.IsNullOrWhiteSpace(snapshotPath)
                    && !string.IsNullOrWhiteSpace(runDirectory)
                    && IsWithinRoot(snapshotPath, runDirectory)
                    && File.Exists(snapshotPath);
                manifest.previewStress.memoryProfilerCaptureAvailable = captured;
                manifest.previewStress.memoryProfilerCaptureSupported = true;
                manifest.previewStress.memoryProfilerSnapshotCaptured = captured;
                manifest.previewStress.memoryProfilerSnapshotPath = captured
                    ? Path.GetFullPath(snapshotPath) : string.Empty;
                manifest.previewStress.memoryProfilerSnapshotMessage = message ?? string.Empty;
                manifest.accepted = manifest.automatedChecksPassed
                    && manifest.liveWindowConflict?.passed == true
                    && manifest.visualMatrix?.complete == true
                    && captured;
                manifest.verdict = manifest.accepted
                    ? "World 专项自动检查、视觉交互矩阵和 Memory Profiler 快照均已闭环。"
                    : manifest.automatedChecksPassed
                        ? manifest.liveWindowConflict?.passed == true
                            ? manifest.visualMatrix?.complete == true
                                ? "专项自动检查与视觉交互矩阵通过；Memory Profiler 快照仍未闭环。"
                                : captured
                                    ? "专项自动检查与 Memory Profiler 快照通过；视觉交互矩阵仍未闭环。"
                                    : "专项自动检查通过；视觉交互矩阵与 Memory Profiler 快照仍未闭环。"
                            : "专项自动检查通过；尚未取得真实同源协作窗口冲突证据。"
                        : "World 专项自动检查未通过。";
                WriteUtf8Atomic(manifestPath, JsonUtility.ToJson(manifest, true));
                result = new ESWorldWorkbenchAcceptanceResult(
                    true,
                    manifest.automatedChecksPassed,
                    manifest.accepted,
                    manifest.previewStress.cancelled,
                    manifest.verdict,
                    runDirectory,
                    manifestPath,
                    manifest.sourceAssetGuid,
                    manifest.assemblyModuleVersionId);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static Type ResolveMemoryProfilerType()
        {
            string[] qualifiedCandidates =
            {
                "Unity.Profiling.Memory.MemoryProfiler, UnityEngine.CoreModule",
                "UnityEngine.Profiling.Memory.Experimental.MemoryProfiler, UnityEngine.CoreModule",
                "Unity.MemoryProfiler.MemoryProfiler, Unity.MemoryProfiler.Editor",
                "Unity.MemoryProfiler.Editor.MemoryProfiler, Unity.MemoryProfiler.Editor"
            };
            for (int i = 0; i < qualifiedCandidates.Length; i++)
            {
                Type qualifiedType = Type.GetType(qualifiedCandidates[i], false);
                if (qualifiedType != null) return qualifiedType;
            }

            string[] candidates =
            {
                "Unity.Profiling.Memory.MemoryProfiler",
                "UnityEngine.Profiling.Memory.Experimental.MemoryProfiler",
                "Unity.MemoryProfiler.MemoryProfiler",
                "Unity.MemoryProfiler.Editor.MemoryProfiler"
            };
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                for (int j = 0; j < candidates.Length; j++)
                {
                    Type type = assemblies[i].GetType(candidates[j], false);
                    if (type != null) return type;
                }
            }
            return null;
        }

        private static MethodInfo ResolveTakeSnapshotMethod(Type profilerType)
        {
            if (profilerType == null) return null;
            return profilerType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(value => string.Equals(value.Name, "TakeSnapshot", StringComparison.Ordinal))
                .Where(value =>
                {
                    ParameterInfo[] parameters = value.GetParameters();
                    return parameters.Length >= 2
                        && parameters[0].ParameterType == typeof(string)
                        && parameters.Any(parameter =>
                            parameter.ParameterType == typeof(Action<string, bool>))
                        && parameters.Skip(1).All(CanBuildTakeSnapshotArgument);
                })
                .OrderBy(value => value.GetParameters().Length)
                .FirstOrDefault();
        }

        private static bool CanBuildTakeSnapshotArgument(ParameterInfo parameter)
        {
            Type type = parameter.ParameterType;
            return type == typeof(Action<string, bool>)
                || parameter.HasDefaultValue
                || type.IsEnum
                || type == typeof(bool)
                || type == typeof(uint)
                || typeof(Delegate).IsAssignableFrom(type)
                || !type.IsValueType;
        }

        private static object[] BuildTakeSnapshotArguments(
            MethodInfo method,
            string snapshotPath,
            Action<string, bool> callback)
        {
            ParameterInfo[] parameters = method.GetParameters();
            var arguments = new object[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                ParameterInfo parameter = parameters[i];
                if (i == 0 && parameter.ParameterType == typeof(string)) arguments[i] = snapshotPath;
                else if (parameter.ParameterType == typeof(Action<string, bool>)) arguments[i] = callback;
                else if (parameter.HasDefaultValue) arguments[i] = parameter.DefaultValue;
                else if (parameter.ParameterType.IsEnum)
                {
                    long flags = 0L;
                    string[] names = Enum.GetNames(parameter.ParameterType);
                    for (int j = 0; j < names.Length; j++)
                    {
                        if (names[j].IndexOf("Managed", StringComparison.OrdinalIgnoreCase) < 0
                            && names[j].IndexOf("Native", StringComparison.OrdinalIgnoreCase) < 0) continue;
                        flags |= Convert.ToInt64(Enum.Parse(parameter.ParameterType, names[j]));
                    }
                    arguments[i] = Enum.ToObject(parameter.ParameterType, flags);
                }
                else if (parameter.ParameterType == typeof(bool)) arguments[i] = true;
                else if (parameter.ParameterType == typeof(uint)) arguments[i] = 0u;
                else arguments[i] = parameter.ParameterType.IsValueType
                    ? Activator.CreateInstance(parameter.ParameterType) : null;
            }
            return arguments;
        }

        internal static Type ResolveMemoryProfilerTypeForTest()
        {
            return ResolveMemoryProfilerType();
        }

        internal static MethodInfo ResolveTakeSnapshotMethodForTest(Type profilerType)
        {
            return ResolveTakeSnapshotMethod(profilerType);
        }

        internal static object[] BuildTakeSnapshotArgumentsForTest(
            MethodInfo method,
            string snapshotPath,
            Action<string, bool> callback)
        {
            return BuildTakeSnapshotArguments(method, snapshotPath, callback);
        }

        public static bool TryRevealDirectory(string path) => TryOpenGuarded(path, true);

        public static bool TryOpenManifest(string path) => TryOpenGuarded(path, false);

        private static ESWorldAcceptanceCheckEvidence CaptureCurrentSession(
            ESWorldEditSession currentSession)
        {
            if (currentSession == null)
                return new ESWorldAcceptanceCheckEvidence
                {
                    summary = "当前窗口尚未创建 World 编辑会话。"
                };
            try
            {
                currentSession.RefreshExternalConflict();
                ESWorldEditSessionConsistencySnapshot snapshot =
                    currentSession.CaptureConsistencySnapshot();
                var evidence = new ESWorldAcceptanceCheckEvidence
                {
                    executed = true,
                    passed = snapshot.Passed,
                    summary = snapshot.Summary + " · 同源活动会话 "
                        + ESWorldEditSession.GetActiveSessionCount(currentSession.Source),
                    diagnostic = snapshot.ToDiagnosticText(),
                    activeSameSourceSessionCount =
                        ESWorldEditSession.GetActiveSessionCount(currentSession.Source),
                    externalConflictObserved = snapshot.HasExternalConflict,
                    conflictOwnerSessionId = snapshot.ConflictOwnerSessionId,
                    initialState = CreateStateEvidence(
                        snapshot,
                        FindPrefabGuid(currentSession.Draft))
                };
                PopulateSessionIdentity(evidence, currentSession, snapshot);
                return evidence;
            }
            catch (Exception exception)
            {
                return FailedCheck("当前会话一致性检查失败", exception);
            }
        }

        private static ESWorldAcceptanceCheckEvidence CaptureLiveWindowUndoRedo(
            ESWorldEditSession currentSession)
        {
            if (currentSession?.Draft?.Definition == null)
                return new ESWorldAcceptanceCheckEvidence
                {
                    summary = "当前窗口尚未创建可操作的 World 草稿。"
                };

            ESWorldMapPrefabPlacement placement = currentSession.Draft.Definition
                .prefabPlacements?.FirstOrDefault(value => value != null
                    && !string.IsNullOrWhiteSpace(value.editorPrefabGuid));
            if (placement == null)
                return new ESWorldAcceptanceCheckEvidence
                {
                    executed = true,
                    summary = "当前草稿没有带真实 Prefab GUID 的放置点，不能验证资产引用 Undo/Redo。"
                };

            string originalMapId = currentSession.Draft.Definition.mapId;
            string originalGuid = placement.editorPrefabGuid;
            string alternateGuid = AssetDatabase.AssetPathToGUID(
                "Assets/ESNormalAssets/Prefabs/蓝色方块.prefab");
            if (string.IsNullOrWhiteSpace(alternateGuid)
                || string.Equals(alternateGuid, originalGuid, StringComparison.Ordinal))
                alternateGuid = AssetDatabase.AssetPathToGUID(
                    "Assets/ESNormalAssets/Prefabs/Cube.prefab");
            if (string.IsNullOrWhiteSpace(alternateGuid)
                || string.Equals(alternateGuid, originalGuid, StringComparison.Ordinal))
                return new ESWorldAcceptanceCheckEvidence
                {
                    executed = true,
                    summary = "没有可用于真实资产引用切换的第二个 Prefab GUID。"
                };

            ESWorldEditSessionConsistencySnapshot initial = default;
            ESWorldEditSessionConsistencySnapshot changed = default;
            ESWorldEditSessionConsistencySnapshot undone = default;
            ESWorldEditSessionConsistencySnapshot redone = default;
            ESWorldEditSessionConsistencySnapshot restored = default;
            try
            {
                initial = currentSession.CaptureConsistencySnapshot();
                Undo.IncrementCurrentGroup();
                int undoGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("ES World 真实窗口 Undo/Redo 验收");
                Undo.RecordObject(currentSession.Draft, "ES World 真实窗口 Undo/Redo 验收");
                currentSession.Draft.Definition.mapId = originalMapId + ".live-undo-redo";
                placement.editorPrefabGuid = alternateGuid;
                currentSession.NotifyDraftChanged("definition");
                Undo.FlushUndoRecordObjects();
                Undo.CollapseUndoOperations(undoGroup);
                changed = currentSession.CaptureConsistencySnapshot();

                Undo.PerformUndo();
                currentSession.SynchronizeDraftAfterUndoRedo();
                undone = currentSession.CaptureConsistencySnapshot();
                ESWorldMapPrefabPlacement undonePlacement = currentSession.Draft.Definition
                    .prefabPlacements?.FirstOrDefault(value => value != null
                        && value.placementId == placement.placementId);
                string undonePrefabGuid = undonePlacement?.editorPrefabGuid ?? string.Empty;
                bool undoValuesRestored = string.Equals(
                        currentSession.Draft.Definition.mapId, originalMapId, StringComparison.Ordinal)
                    && string.Equals(undonePrefabGuid, originalGuid, StringComparison.Ordinal);

                Undo.PerformRedo();
                currentSession.SynchronizeDraftAfterUndoRedo();
                redone = currentSession.CaptureConsistencySnapshot();
                ESWorldMapPrefabPlacement redonePlacement = currentSession.Draft.Definition
                    .prefabPlacements?.FirstOrDefault(value => value != null
                        && value.placementId == placement.placementId);
                string redonePrefabGuid = redonePlacement?.editorPrefabGuid ?? string.Empty;
                bool redoValuesRestored = string.Equals(
                        currentSession.Draft.Definition.mapId,
                        originalMapId + ".live-undo-redo",
                        StringComparison.Ordinal)
                    && string.Equals(redonePrefabGuid, alternateGuid, StringComparison.Ordinal);

                Undo.PerformUndo();
                currentSession.SynchronizeDraftAfterUndoRedo();
                restored = currentSession.CaptureConsistencySnapshot();
                ESWorldMapPrefabPlacement restoredPlacement = currentSession.Draft.Definition
                    .prefabPlacements?.FirstOrDefault(value => value != null
                        && value.placementId == placement.placementId);
                string restoredPrefabGuid = restoredPlacement?.editorPrefabGuid ?? string.Empty;
                bool finalValuesRestored = string.Equals(
                        currentSession.Draft.Definition.mapId, originalMapId, StringComparison.Ordinal)
                    && string.Equals(restoredPrefabGuid, originalGuid, StringComparison.Ordinal);
                bool passed = initial.Passed
                    && changed.Passed
                    && undone.Passed
                    && redone.Passed
                    && restored.Passed
                    && !string.Equals(initial.ActualDraftHash, changed.ActualDraftHash, StringComparison.Ordinal)
                    && string.Equals(initial.ActualDraftHash, undone.ActualDraftHash, StringComparison.Ordinal)
                    && string.Equals(changed.ActualDraftHash, redone.ActualDraftHash, StringComparison.Ordinal)
                    && string.Equals(initial.ActualDraftHash, restored.ActualDraftHash, StringComparison.Ordinal)
                    && undoValuesRestored
                    && redoValuesRestored
                    && finalValuesRestored;
                var evidence = new ESWorldAcceptanceCheckEvidence
                {
                    executed = true,
                    passed = passed,
                    summary = passed
                        ? "真实窗口 Undo/Redo 后 Draft Hash、ChangeSet、Dirty、SessionState 与 Prefab GUID 全部一致"
                        : "真实窗口 Undo/Redo 一致性检查未通过",
                    diagnostic = "Initial\n" + initial.ToDiagnosticText()
                        + "\n\nChanged\n" + changed.ToDiagnosticText()
                        + "\n\nUndone\n" + undone.ToDiagnosticText()
                        + "\n\nRedone\n" + redone.ToDiagnosticText()
                        + "\n\nRestored\n" + restored.ToDiagnosticText()
                        + "\n\nOriginalPrefabGuid=" + originalGuid
                        + "\nOriginalPrefabPath=" + AssetDatabase.GUIDToAssetPath(originalGuid)
                        + "\nAlternatePrefabGuid=" + alternateGuid
                        + "\nAlternatePrefabPath=" + AssetDatabase.GUIDToAssetPath(alternateGuid),
                    initialState = CreateStateEvidence(initial, originalGuid),
                    changedState = CreateStateEvidence(changed, alternateGuid),
                    undoState = CreateStateEvidence(undone, undonePrefabGuid),
                    redoState = CreateStateEvidence(redone, redonePrefabGuid),
                    restoredState = CreateStateEvidence(
                        restored,
                        restoredPrefabGuid)
                };
                PopulateSessionIdentity(evidence, currentSession, restored);
                return evidence;
            }
            catch (Exception exception)
            {
                currentSession.SynchronizeDraftAfterUndoRedo();
                return FailedCheck("真实窗口 Undo/Redo 一致性检查失败", exception);
            }
            finally
            {
                Undo.ClearUndo(currentSession.Draft);
            }
        }

        private static ESWorldAcceptanceCheckEvidence CaptureLiveWindowConflict(
            ESWorldEditSession currentSession)
        {
            if (currentSession == null)
                return new ESWorldAcceptanceCheckEvidence
                {
                    summary = "当前窗口尚未创建 World 编辑会话。"
                };
            try
            {
                currentSession.RefreshExternalConflict();
                ESWorldEditSessionConsistencySnapshot before =
                    currentSession.CaptureConsistencySnapshot();
                int activeCount = ESWorldEditSession.GetActiveSessionCount(currentSession.Source);
                bool readyForRejectProbe = before.Passed
                    && activeCount >= 2
                    && before.IsDirty
                    && before.HasExternalConflict
                    && !string.IsNullOrWhiteSpace(before.ConflictOwnerSessionId);
                string draftHashBefore = before.ActualDraftHash;
                string sourceHashBefore = before.CurrentSourceHash;
                string prefabGuidBefore = FindPrefabGuid(currentSession.Draft);
                ESWorldEditCommitResult write = readyForRejectProbe
                    ? currentSession.TryCommit()
                    : default;
                ESWorldEditSessionConsistencySnapshot after =
                    currentSession.CaptureConsistencySnapshot();
                string prefabGuidAfter = FindPrefabGuid(currentSession.Draft);
                bool writeRejected = readyForRejectProbe && !write.success && write.conflict;
                bool localDraftPreserved = readyForRejectProbe
                    && string.Equals(draftHashBefore, after.ActualDraftHash, StringComparison.Ordinal)
                    && after.IsDirty
                    && after.HasExternalConflict;
                bool sourcePreserved = readyForRejectProbe
                    && string.Equals(sourceHashBefore, after.CurrentSourceHash, StringComparison.Ordinal);
                bool passed = readyForRejectProbe
                    && writeRejected
                    && localDraftPreserved
                    && sourcePreserved
                    && after.Passed;
                var evidence = new ESWorldAcceptanceCheckEvidence
                {
                    executed = true,
                    passed = passed,
                    summary = passed
                        ? "真实同源协作窗口已观察到外部提交，当前本地草稿仍保留并被阻断"
                        : "尚未形成可签收的真实同源窗口冲突状态",
                    diagnostic = "BeforeReject\n" + before.ToDiagnosticText()
                        + "\n\nWriteAttempted=" + readyForRejectProbe
                        + "\nWriteRejected=" + writeRejected
                        + "\nWriteResult=" + (readyForRejectProbe ? write.message : "未满足拒写探针前置条件")
                        + "\nLocalDraftPreserved=" + localDraftPreserved
                        + "\nSourcePreserved=" + sourcePreserved
                        + "\n\nAfterReject\n" + after.ToDiagnosticText()
                        + "\nLiveWindowRequirement=Active>=2, Dirty=True, ExternalConflict=True, ConflictOwner!=Empty, TryCommit=Rejected",
                    activeSameSourceSessionCount = activeCount,
                    externalConflictObserved = after.HasExternalConflict,
                    conflictOwnerSessionId = after.ConflictOwnerSessionId,
                    writeAttempted = readyForRejectProbe,
                    writeRejected = writeRejected,
                    localDraftPreserved = localDraftPreserved,
                    sourcePreserved = sourcePreserved,
                    writeResult = readyForRejectProbe ? write.message : string.Empty,
                    initialState = CreateStateEvidence(
                        before,
                        prefabGuidBefore),
                    rejectedState = CreateStateEvidence(
                        after,
                        prefabGuidAfter),
                    draftHashBeforeReject = draftHashBefore,
                    draftHashAfterReject = after.ActualDraftHash,
                    sourceHashBeforeReject = sourceHashBefore,
                    sourceHashAfterReject = after.CurrentSourceHash
                };
                PopulateSessionIdentity(evidence, currentSession, after);
                return evidence;
            }
            catch (Exception exception)
            {
                return FailedCheck("真实同源窗口冲突检查失败", exception);
            }
        }

        private static ESWorldAcceptanceCheckEvidence RunUndoRedoRecovery(
            ESWorldMapAsset template)
        {
            ESWorldMapAsset specimen = CreateSpecimen(template, "undo-redo-source");
            ESWorldEditSession session = null;
            string diagnostic = string.Empty;
            try
            {
                session = ESWorldEditSession.Open(specimen, "acceptance-undo-redo");
                ESWorldEditSessionConsistencySnapshot initial =
                    session.CaptureConsistencySnapshot();
                string originalMapId = session.Draft.Definition.mapId;
                string originalReference =
                    session.Draft.Definition.prefabPlacements[0].editorPrefabGuid;
                string changedMapId = "acceptance-draft-map";
                string changedReference = "22222222222222222222222222222222";

                Undo.IncrementCurrentGroup();
                int undoGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("ES World 验收：草稿 Undo/Redo");
                Undo.RecordObject(session.Draft, "ES World 验收：修改草稿");
                session.Draft.Definition.mapId = changedMapId;
                session.Draft.Definition.seed = 77;
                session.Draft.Definition.prefabPlacements[0].editorPrefabGuid =
                    changedReference;
                session.NotifyDraftChanged("definition");
                Undo.FlushUndoRecordObjects();
                Undo.CollapseUndoOperations(undoGroup);
                ESWorldEditSessionConsistencySnapshot changed =
                    session.CaptureConsistencySnapshot();

                Undo.PerformUndo();
                session.SynchronizeDraftAfterUndoRedo();
                ESWorldEditSessionConsistencySnapshot undone =
                    session.CaptureConsistencySnapshot();
                string undoneReference =
                    session.Draft.Definition.prefabPlacements[0].editorPrefabGuid;
                bool undoReferenceRestored = string.Equals(
                    undoneReference,
                    originalReference,
                    StringComparison.Ordinal);

                Undo.PerformRedo();
                session.SynchronizeDraftAfterUndoRedo();
                ESWorldEditSessionConsistencySnapshot redone =
                    session.CaptureConsistencySnapshot();
                string redoneReference =
                    session.Draft.Definition.prefabPlacements[0].editorPrefabGuid;
                bool redoReferenceRestored = string.Equals(
                    redoneReference,
                    changedReference,
                    StringComparison.Ordinal);
                bool redoValueRestored =
                    session.Draft.Definition.mapId == changedMapId
                    && session.Draft.Definition.seed == 77;

                Undo.ClearUndo(session.Draft);
                session.Dispose();
                session = ESWorldEditSession.Open(specimen, "acceptance-undo-redo");
                ESWorldEditSessionConsistencySnapshot reopened =
                    session.CaptureConsistencySnapshot();
                string reopenedReference =
                    session.Draft.Definition.prefabPlacements[0].editorPrefabGuid;
                bool recoveryRestored =
                    session.Draft.Definition.mapId == changedMapId
                    && session.Draft.Definition.seed == 77
                    && session.Draft.Definition.prefabPlacements[0].editorPrefabGuid
                        == changedReference;
                bool passed = initial.Passed
                    && changed.Passed && changed.IsDirty
                    && undone.Passed && !undone.IsDirty
                    && session.Draft != null
                    && redone.Passed && redone.IsDirty
                    && reopened.Passed && reopened.IsDirty
                    && undoReferenceRestored
                    && redoReferenceRestored
                    && redoValueRestored
                    && recoveryRestored
                    && specimen.Definition.mapId == originalMapId;
                diagnostic = "Initial\n" + initial.ToDiagnosticText()
                    + "\n\nChanged\n" + changed.ToDiagnosticText()
                    + "\n\nUndone\n" + undone.ToDiagnosticText()
                    + "\n\nRedone\n" + redone.ToDiagnosticText()
                    + "\n\nReopened\n" + reopened.ToDiagnosticText()
                    + "\n\nAssetReferenceUndo=" + undoReferenceRestored
                    + "\nAssetReferenceRedo=" + redoReferenceRestored
                    + "\nRecoveryRestored=" + recoveryRestored;
                var evidence = new ESWorldAcceptanceCheckEvidence
                {
                    executed = true,
                    passed = passed,
                    summary = passed
                        ? "Undo/Redo、Hash、ChangeSet、Dirty、SessionState 与资源引用一致"
                        : "Undo/Redo 或恢复链存在不一致",
                    diagnostic = diagnostic,
                    initialState = CreateStateEvidence(initial, originalReference),
                    changedState = CreateStateEvidence(changed, changedReference),
                    undoState = CreateStateEvidence(undone, undoneReference),
                    redoState = CreateStateEvidence(redone, redoneReference),
                    reopenedState = CreateStateEvidence(reopened, reopenedReference)
                };
                PopulateSessionIdentity(evidence, session, reopened);
                return evidence;
            }
            catch (Exception exception)
            {
                return FailedCheck("Undo/Redo 与 SessionState 专项检查失败", exception, diagnostic);
            }
            finally
            {
                if (session != null)
                {
                    Undo.ClearUndo(session.Draft);
                    session.ClearRecoveryState();
                    session.Dispose();
                }
                Undo.ClearUndo(specimen);
                UnityEngine.Object.DestroyImmediate(specimen);
            }
        }

        private static ESWorldAcceptanceCheckEvidence RunMultiWindowConflict(
            ESWorldMapAsset template)
        {
            ESWorldMapAsset specimen = CreateSpecimen(template, "conflict-source");
            ESWorldEditSession first = null;
            ESWorldEditSession second = null;
            string diagnostic = string.Empty;
            try
            {
                first = ESWorldEditSession.Open(specimen, "acceptance-window-a");
                second = ESWorldEditSession.Open(specimen, "acceptance-window-b");
                ESWorldEditSessionConsistencySnapshot initial =
                    second.CaptureConsistencySnapshot();
                string initialReference = FindPrefabGuid(second.Draft);
                string preservedReference = "33333333333333333333333333333333";
                second.Draft.Definition.seed = 91;
                second.Draft.Definition.prefabPlacements[0].editorPrefabGuid =
                    preservedReference;
                second.NotifyDraftChanged("definition");
                ESWorldEditSessionConsistencySnapshot changed =
                    second.CaptureConsistencySnapshot();

                first.Draft.Definition.mapId = "committed-by-acceptance-window-a";
                first.NotifyDraftChanged("definition.mapId");
                ESWorldEditCommitResult committed = first.TryCommit();
                ESWorldEditSessionConsistencySnapshot conflicted =
                    second.CaptureConsistencySnapshot();
                string draftHashBeforeReject = conflicted.ActualDraftHash;
                string sourceHashBeforeReject = conflicted.CurrentSourceHash;
                ESWorldEditCommitResult rejected = second.TryCommit();
                ESWorldEditSessionConsistencySnapshot rejectedSnapshot =
                    second.CaptureConsistencySnapshot();
                bool rejectedDraftHashPreserved = string.Equals(
                    draftHashBeforeReject,
                    rejectedSnapshot.ActualDraftHash,
                    StringComparison.Ordinal);
                bool rejectedSourceHashPreserved = string.Equals(
                    sourceHashBeforeReject,
                    rejectedSnapshot.CurrentSourceHash,
                    StringComparison.Ordinal);
                bool localProgressPreserved = second.Draft.Definition.seed == 91
                    && second.Draft.Definition.prefabPlacements[0].editorPrefabGuid
                        == preservedReference;
                bool sourcePreserved =
                    specimen.Definition.mapId == "committed-by-acceptance-window-a";

                second.ReloadFromSource();
                ESWorldEditSessionConsistencySnapshot recovered =
                    second.CaptureConsistencySnapshot();
                bool recoveryAcceptedSource =
                    second.Draft.Definition.mapId == specimen.Definition.mapId
                    && !second.IsDirty
                    && !second.HasExternalConflict;
                bool passed = committed.success
                    && conflicted.Passed
                    && conflicted.HasExternalConflict
                    && conflicted.ConflictOwnerSessionId == "acceptance-window-a"
                    && !rejected.success
                    && rejected.conflict
                    && rejectedDraftHashPreserved
                    && rejectedSourceHashPreserved
                    && localProgressPreserved
                    && sourcePreserved
                    && recovered.Passed
                    && recoveryAcceptedSource;
                diagnostic = "Conflicted\n" + conflicted.ToDiagnosticText()
                    + "\n\nRejected=" + rejected.conflict
                    + "\nRejectedDraftHashPreserved=" + rejectedDraftHashPreserved
                    + "\nRejectedSourceHashPreserved=" + rejectedSourceHashPreserved
                    + "\nLocalProgressPreserved=" + localProgressPreserved
                    + "\nSourcePreserved=" + sourcePreserved
                    + "\n\nRecovered\n" + recovered.ToDiagnosticText();
                var evidence = new ESWorldAcceptanceCheckEvidence
                {
                    executed = true,
                    passed = passed,
                    summary = passed
                        ? "多窗口后写被拒绝，本地草稿未丢失，明确重载后恢复一致"
                        : "多窗口冲突拒写或恢复链存在不一致",
                    diagnostic = diagnostic,
                    initialState = CreateStateEvidence(initial, initialReference),
                    changedState = CreateStateEvidence(changed, preservedReference),
                    conflictState = CreateStateEvidence(conflicted, preservedReference),
                    rejectedState = CreateStateEvidence(
                        rejectedSnapshot,
                        preservedReference),
                    restoredState = CreateStateEvidence(
                        recovered,
                        FindPrefabGuid(second.Draft)),
                    draftHashBeforeReject = draftHashBeforeReject,
                    draftHashAfterReject = rejectedSnapshot.ActualDraftHash,
                    sourceHashBeforeReject = sourceHashBeforeReject,
                    sourceHashAfterReject = rejectedSnapshot.CurrentSourceHash,
                    activeSameSourceSessionCount = conflicted.ActiveOwnerSessionIds.Count,
                    externalConflictObserved = conflicted.HasExternalConflict,
                    conflictOwnerSessionId = conflicted.ConflictOwnerSessionId,
                    writeAttempted = true,
                    writeRejected = !rejected.success && rejected.conflict,
                    localDraftPreserved = localProgressPreserved,
                    sourcePreserved = sourcePreserved,
                    writeResult = rejected.message
                };
                PopulateSessionIdentity(evidence, second, conflicted);
                return evidence;
            }
            catch (Exception exception)
            {
                return FailedCheck("多窗口冲突专项检查失败", exception, diagnostic);
            }
            finally
            {
                if (first != null)
                {
                    Undo.ClearUndo(first.Draft);
                    first.ClearRecoveryState();
                    first.Dispose();
                }
                if (second != null)
                {
                    Undo.ClearUndo(second.Draft);
                    second.ClearRecoveryState();
                    second.Dispose();
                }
                Undo.ClearUndo(specimen);
                UnityEngine.Object.DestroyImmediate(specimen);
            }
        }

        private static ESWorldAcceptanceStateEvidence CreateStateEvidence(
            ESWorldEditSessionConsistencySnapshot snapshot,
            string prefabGuid)
        {
            return new ESWorldAcceptanceStateEvidence
            {
                draftHash = snapshot.ActualDraftHash,
                sourceHash = snapshot.CurrentSourceHash,
                changeCount = snapshot.ChangeCount,
                dirty = snapshot.IsDirty,
                externalConflict = snapshot.HasExternalConflict,
                consistencyPassed = snapshot.Passed,
                prefabGuid = prefabGuid ?? string.Empty
            };
        }

        private static void PopulateSessionIdentity(
            ESWorldAcceptanceCheckEvidence evidence,
            ESWorldEditSession session,
            ESWorldEditSessionConsistencySnapshot snapshot)
        {
            if (evidence == null || session == null) return;
            evidence.ownerSessionId = snapshot.OwnerSessionId;
            evidence.activeOwnerSessionIds.Clear();
            if (snapshot.ActiveOwnerSessionIds != null)
                for (int i = 0; i < snapshot.ActiveOwnerSessionIds.Count; i++)
                    evidence.activeOwnerSessionIds.Add(snapshot.ActiveOwnerSessionIds[i]);
            evidence.sourceAssetPath = session.Source == null
                ? string.Empty : AssetDatabase.GetAssetPath(session.Source);
            evidence.sourceAssetGuid = string.IsNullOrWhiteSpace(evidence.sourceAssetPath)
                ? string.Empty : AssetDatabase.AssetPathToGUID(evidence.sourceAssetPath);
        }

        private static string FindPrefabGuid(ESWorldMapAsset asset)
        {
            ESWorldMapPrefabPlacement placement = asset?.Definition?.prefabPlacements?
                .FirstOrDefault(value => value != null
                    && !string.IsNullOrWhiteSpace(value.editorPrefabGuid));
            return placement?.editorPrefabGuid ?? string.Empty;
        }

        private static ESWorldMapAsset CreateSpecimen(
            ESWorldMapAsset template,
            string mapId)
        {
            var specimen = ScriptableObject.CreateInstance<ESWorldMapAsset>();
            specimen.name = "ES World Acceptance Specimen";
            specimen.hideFlags = HideFlags.HideAndDontSave;
            ESWorldMapDefinition definition = specimen.Definition;
            definition.mapId = mapId;
            definition.contentVersion = 1;
            definition.contentHash = "acceptance-baseline";
            definition.sourceMode = ESWorldMapSourceMode.Procedural;
            definition.generatorKey = "es.acceptance.world";
            definition.generatorVersion = 1;
            definition.worldMin = Vector2.zero;
            definition.worldMax = new Vector2(128f, 128f);
            definition.heightfield.EnsureSamples();
            string prefabKey = "acceptance.prefab";
            string prefabGuid = "11111111111111111111111111111111";
            if (template?.Definition?.prefabPlacements != null)
            {
                for (int i = 0; i < template.Definition.prefabPlacements.Count; i++)
                {
                    ESWorldMapPrefabPlacement placement =
                        template.Definition.prefabPlacements[i];
                    if (placement == null) continue;
                    if (!string.IsNullOrWhiteSpace(placement.prefabKey))
                        prefabKey = placement.prefabKey;
                    if (!string.IsNullOrWhiteSpace(placement.editorPrefabGuid))
                        prefabGuid = placement.editorPrefabGuid;
                    break;
                }
            }
            definition.prefabPlacements.Add(new ESWorldMapPrefabPlacement
            {
                placementId = "acceptance-placement",
                prefabKey = prefabKey,
                editorPrefabGuid = prefabGuid,
                position = new Vector3(16f, 0f, 16f),
                scale = Vector3.one,
                enabled = true
            });
            return specimen;
        }

        private static ESWorldAcceptanceCheckEvidence FailedCheck(
            string title,
            Exception exception,
            string diagnostic = null)
        {
            return new ESWorldAcceptanceCheckEvidence
            {
                executed = true,
                passed = false,
                summary = title + "：" + exception.Message,
                diagnostic = (diagnostic ?? string.Empty)
                    + (string.IsNullOrEmpty(diagnostic) ? string.Empty : "\n\n")
                    + exception
            };
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

        private static bool IsWithinRoot(string path, string root)
        {
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(root))
                return false;
            string fullPath = Path.GetFullPath(path)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string fullRoot = Path.GetFullPath(root)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return string.Equals(fullPath, fullRoot, StringComparison.OrdinalIgnoreCase)
                || fullPath.StartsWith(
                    fullRoot + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase)
                || fullPath.StartsWith(
                    fullRoot + Path.AltDirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase);
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
            if (string.IsNullOrEmpty(directory))
                throw new InvalidOperationException("验收输出目录无效。");
            Directory.CreateDirectory(directory);
            string temporary = path + ".tmp";
            File.WriteAllText(
                temporary,
                content ?? string.Empty,
                new UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
        }
    }
}
#endif
