using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace ES
{
    /// <summary>
    /// Unity/URP 当前渲染后端的只读事实快照。
    /// 不包含写入方法；快照本身不证明 Frame Debugger、Profiler 或 Player 结果。
    /// </summary>
    public readonly struct ESRenderBackendSnapshot
    {
        public ESRenderBackendSnapshot(
            int qualityIndex,
            string qualityName,
            string pipelineName,
            bool srpBatcherEnabled)
            : this(qualityIndex, qualityName, pipelineName, srpBatcherEnabled, 0)
        {
        }

        public ESRenderBackendSnapshot(
            int qualityIndex,
            string qualityName,
            string pipelineName,
            bool srpBatcherEnabled,
            int qualityCount)
            : this(qualityIndex, qualityName, pipelineName, srpBatcherEnabled, qualityCount, string.Empty, null)
        {
        }

        public ESRenderBackendSnapshot(
            int qualityIndex,
            string qualityName,
            string pipelineName,
            bool srpBatcherEnabled,
            int qualityCount,
            string qualityNamesFingerprint)
        {
            QualityIndex = qualityIndex;
            QualityName = qualityName ?? string.Empty;
            PipelineName = pipelineName ?? string.Empty;
            SrpBatcherEnabled = srpBatcherEnabled;
            QualityCount = Math.Max(0, qualityCount);
            QualityNamesFingerprint = qualityNamesFingerprint ?? string.Empty;
            LightingRecipe = null;
        }

        public ESRenderBackendSnapshot(
            int qualityIndex,
            string qualityName,
            string pipelineName,
            bool srpBatcherEnabled,
            int qualityCount,
            string qualityNamesFingerprint,
            ESRenderLightingRecipe? lightingRecipe)
        {
            QualityIndex = qualityIndex;
            QualityName = qualityName ?? string.Empty;
            PipelineName = pipelineName ?? string.Empty;
            SrpBatcherEnabled = srpBatcherEnabled;
            QualityCount = Math.Max(0, qualityCount);
            QualityNamesFingerprint = qualityNamesFingerprint ?? string.Empty;
            LightingRecipe = lightingRecipe;
        }

        public int QualityIndex { get; }
        public string QualityName { get; }
        public string PipelineName { get; }
        public bool SrpBatcherEnabled { get; }
        public int QualityCount { get; }
        public string QualityNamesFingerprint { get; }
        /// <summary>
        /// 可选的实际打光状态。为空表示当前捕获器尚未提供灯光/阴影事实，不能推断已应用。
        /// </summary>
        public ESRenderLightingRecipe? LightingRecipe { get; }

        public bool IsUrpLikePipeline
        {
            get { return PipelineName.IndexOf("Universal", StringComparison.OrdinalIgnoreCase) >= 0; }
        }

        public static bool TryCapture(out ESRenderBackendSnapshot snapshot, out string reason)
        {
            try
            {
                int qualityIndex = QualitySettings.GetQualityLevel();
                string[] names = QualitySettings.names;
                string qualityName = names != null
                    && qualityIndex >= 0
                    && qualityIndex < names.Length
                    ? names[qualityIndex]
                    : string.Empty;
                RenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline;
                string pipelineName = pipeline != null ? pipeline.name : string.Empty;
                snapshot = new ESRenderBackendSnapshot(
                    qualityIndex,
                    qualityName,
                    pipelineName,
                    GraphicsSettings.useScriptableRenderPipelineBatching,
                    names != null ? names.Length : 0,
                    names == null ? string.Empty : string.Join("\u001F", names));
                reason = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                snapshot = default(ESRenderBackendSnapshot);
                reason = exception.GetType().Name + ": " + exception.Message;
                return false;
            }
        }

        public static bool TryCapture(
            IESRenderLightingTarget lightingTarget,
            out ESRenderBackendSnapshot snapshot,
            out string reason)
        {
            if (lightingTarget == null)
            {
                snapshot = default(ESRenderBackendSnapshot);
                reason = "lighting-target-required";
                return false;
            }
            if (!TryCapture(out ESRenderBackendSnapshot backend, out reason))
            {
                snapshot = default(ESRenderBackendSnapshot);
                return false;
            }
            if (!lightingTarget.TryCapture(out ESRenderLightingRecipe lighting, out reason))
            {
                snapshot = default(ESRenderBackendSnapshot);
                return false;
            }
            snapshot = new ESRenderBackendSnapshot(
                backend.QualityIndex,
                backend.QualityName,
                backend.PipelineName,
                backend.SrpBatcherEnabled,
                backend.QualityCount,
                backend.QualityNamesFingerprint,
                lighting);
            reason = string.Empty;
            return true;
        }
    }
}
