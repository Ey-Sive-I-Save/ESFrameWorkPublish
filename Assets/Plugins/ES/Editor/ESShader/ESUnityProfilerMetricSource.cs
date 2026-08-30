using System;
using Unity.Profiling;

namespace ES.EditorInternal
{
    /// <summary>
    /// ES 对 Unity ProfilerRecorder 的受限适配。指标名和单位由调用方显式提供；
    /// 任何不可用计数器都返回失败，不以零值代替未测量事实。
    /// </summary>
    public sealed class ESUnityProfilerMetricSource : IDisposable
    {
        private readonly ESRenderMetricCaptureSession session;
        private ProfilerRecorder drawCalls;
        private ProfilerRecorder setPassCalls;
        private ProfilerRecorder cpuTime;
        private ProfilerRecorder gpuTime;
        private ProfilerRecorder gcAlloc;
        private ProfilerRecorder residentMemory;
        private bool disposed;

        private ESUnityProfilerMetricSource(
            ESRenderMetricCaptureSession session,
            ProfilerRecorder drawCalls,
            ProfilerRecorder setPassCalls,
            ProfilerRecorder cpuTime,
            ProfilerRecorder gpuTime,
            ProfilerRecorder gcAlloc,
            ProfilerRecorder residentMemory)
        {
            this.session = session;
            this.drawCalls = drawCalls;
            this.setPassCalls = setPassCalls;
            this.cpuTime = cpuTime;
            this.gpuTime = gpuTime;
            this.gcAlloc = gcAlloc;
            this.residentMemory = residentMemory;
        }

        public bool IsDisposed => disposed;
        public bool IsCompleted => session.IsCompleted;

        public static bool TryCreate(
            ESRenderMetricSamplingRequest request,
            string drawCallsMarker,
            string setPassCallsMarker,
            string cpuTimeMarker,
            string gpuTimeMarker,
            string gcAllocMarker,
            string residentMemoryMarker,
            out ESUnityProfilerMetricSource source,
            out string reason)
        {
            source = null;
            if (!ESRenderMetricCaptureSession.TryCreate(request, out ESRenderMetricCaptureSession session, out reason))
                return false;
            if (string.IsNullOrWhiteSpace(drawCallsMarker)
                || string.IsNullOrWhiteSpace(setPassCallsMarker)
                || string.IsNullOrWhiteSpace(cpuTimeMarker)
                || string.IsNullOrWhiteSpace(gpuTimeMarker)
                || string.IsNullOrWhiteSpace(gcAllocMarker)
                || string.IsNullOrWhiteSpace(residentMemoryMarker))
            {
                reason = "profiler-marker-names-required";
                return false;
            }

            ProfilerRecorder draw = default(ProfilerRecorder);
            ProfilerRecorder setPass = default(ProfilerRecorder);
            ProfilerRecorder cpu = default(ProfilerRecorder);
            ProfilerRecorder gpu = default(ProfilerRecorder);
            ProfilerRecorder gc = default(ProfilerRecorder);
            ProfilerRecorder memory = default(ProfilerRecorder);
            try
            {
                draw = ProfilerRecorder.StartNew(ProfilerCategory.Render, drawCallsMarker, 1);
                setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, setPassCallsMarker, 1);
                cpu = ProfilerRecorder.StartNew(ProfilerCategory.Internal, cpuTimeMarker, 1);
                gpu = ProfilerRecorder.StartNew(ProfilerCategory.Render, gpuTimeMarker, 1);
                gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, gcAllocMarker, 1);
                memory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, residentMemoryMarker, 1);
                source = new ESUnityProfilerMetricSource(session, draw, setPass, cpu, gpu, gc, memory);
                reason = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                StopAndDispose(ref draw);
                StopAndDispose(ref setPass);
                StopAndDispose(ref cpu);
                StopAndDispose(ref gpu);
                StopAndDispose(ref gc);
                StopAndDispose(ref memory);
                reason = "profiler-recorder-start-failed-" + exception.GetType().Name;
                return false;
            }
        }

        /// <summary>读取当前帧的六类指标，并将其提交给 ES 会话。</summary>
        public bool TryCaptureFrame(out string reason)
        {
            if (disposed)
            {
                reason = "profiler-source-disposed";
                return false;
            }
            if (!AreAllValid())
            {
                reason = "profiler-marker-unavailable";
                return false;
            }

            return session.TryAddSample(
                ClampToInt(drawCalls.LastValue),
                ClampToInt(setPassCalls.LastValue),
                NanosecondsToMilliseconds(cpuTime.LastValue),
                NanosecondsToMilliseconds(gpuTime.LastValue),
                ClampToInt(gcAlloc.LastValue),
                Math.Max(0L, residentMemory.LastValue),
                out reason);
        }

        public bool TryComplete(out ESRenderMetricSnapshot snapshot, out string reason)
        {
            if (disposed)
            {
                snapshot = default(ESRenderMetricSnapshot);
                reason = "profiler-source-disposed";
                return false;
            }
            return session.TryComplete(out snapshot, out reason);
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            StopAndDispose(ref drawCalls);
            StopAndDispose(ref setPassCalls);
            StopAndDispose(ref cpuTime);
            StopAndDispose(ref gpuTime);
            StopAndDispose(ref gcAlloc);
            StopAndDispose(ref residentMemory);
        }

        private bool AreAllValid()
        {
            return drawCalls.Valid && setPassCalls.Valid && cpuTime.Valid
                && gpuTime.Valid && gcAlloc.Valid && residentMemory.Valid;
        }

        private static float NanosecondsToMilliseconds(long value)
        {
            return Math.Max(0f, value / 1000000f);
        }

        private static int ClampToInt(long value)
        {
            if (value <= 0L) return 0;
            return value >= int.MaxValue ? int.MaxValue : (int)value;
        }

        private static void StopAndDispose(ref ProfilerRecorder recorder)
        {
            if (!recorder.Valid)
                return;
            recorder.Stop();
            recorder.Dispose();
        }

    }
}
