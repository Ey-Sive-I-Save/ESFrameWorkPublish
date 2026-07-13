using ES;
using System;
using System.Collections;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;


namespace ES
{
#if UNITY_EDITOR
    /// <summary>
    /// 编辑器时间线预览播放控制器。保留 EditorTimelinePlayer 旧名称用于现有窗口引用。
    /// </summary>
    public class EditorTimelinePlayer
    {
        public static EditorTimelinePlayer Instance { get; } = new();

        // 存放当前激活的序列（目前只放一个，未来可放多个）。
        private List<EditorSequencePlayer> activeSequences = new List<EditorSequencePlayer>();

        /// <summary> 获取或设置当前主序列（默认是第一个激活的序列）。</summary>
        public EditorSequencePlayer ActiveSequence
        {
            get => activeSequences.Count > 0 ? activeSequences[0] : null;
            set
            {
                try
                {
                    Stop();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }

                foreach (var sequence in activeSequences)
                {
                    try
                    {
                        sequence?.DisposeEditorPreviewTarget();
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }
                }

                activeSequences.Clear();
                if (value != null)
                    activeSequences.Add(value);
            }
        }

        /// <summary> 添加一个要同时播放的序列（未来扩展用）。</summary>
        public void AddSequence(EditorSequencePlayer sequence)
        {
            if (sequence != null && !activeSequences.Contains(sequence))
                activeSequences.Add(sequence);
        }

        /// <summary> 移除一个序列。</summary>
        public void RemoveSequence(EditorSequencePlayer sequence)
        {
            if (sequence != null)
            {
                sequence.DisposeEditorPreviewTarget();
                activeSequences.Remove(sequence);
            }
        }

        // ---------- 全局播放控制（作用于所有激活序列） ----------

        public void Play()
        {
            if (activeSequences.Count == 0) return;

            foreach (var seq in activeSequences)
                seq.Play();

            if (!IsUpdateRegistered)
            {
                lastTickTime = EditorApplication.timeSinceStartup; // 关键：初始化时间基准
                EditorApplication.update += TickAll;
                IsUpdateRegistered = true;
            }
        }

        private void UnregisterUpdate()
        {
            EditorApplication.update -= TickAll;
            IsUpdateRegistered = false;
        }
        public void Pause()
        {
            foreach (var seq in activeSequences)
                seq.Pause();

            // 如果所有序列都暂停，注销 update。
            if (AllSequencesPaused())
                UnregisterUpdate();
        }

        public void Stop()
        {
            foreach (var seq in activeSequences)
                seq.Stop();

            UnregisterUpdate();
        }

        public void SetTime(float time)
        {
            // 对当前所有激活序列设置时间（每个序列内部会 clamp 到自身 Duration）。
            foreach (var seq in activeSequences)
                seq.SetTime(time);
        }

        // ---------- 内部实现 ----------

        private bool IsUpdateRegistered = false;
        private double lastTickTime = 0;
        private void TickAll()
        {
            double now = EditorApplication.timeSinceStartup;
            float dt = (float)(now - lastTickTime);
            lastTickTime = now;

            // 防止切换窗口后首帧 Tick 产生一个巨大的 dt（例如数秒）。
            dt = Mathf.Min(dt, 0.1f);

            for (int i = activeSequences.Count - 1; i >= 0; i--)
            {
                var sequence = activeSequences[i];
                if (sequence == null)
                {
                    activeSequences.RemoveAt(i);
                    continue;
                }

                try
                {
                    sequence.Tick(dt);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            if (AllSequencesPaused())
                UnregisterUpdate();
        }
        private bool AllSequencesPaused()
        {
            foreach (var seq in activeSequences)
                if (seq.IsPlaying) return false;
            return true;
        }

   

        ~EditorTimelinePlayer()
        {
            EditorApplication.update -= TickAll;
        }
    }

    public class EditorSequencePlayer
    {
        public string Name { get; set; } = "Sequence";
        public ESRuntimeTargetPack PreviewTarget { get; } = ESRuntimeTargetPack.Pool.GetInPool();

        // 播放状态
        public bool IsPlaying { get; private set; }
        public float Speed { get; set; } = 1f;      // 支持倒放
        public float CurrentTime { get; private set; }
        public float Duration { get; set; } = 10f;

        // 采样器集合
        private HashSet<IEditorTimeSampler> samplers = new HashSet<IEditorTimeSampler>();
        private Dictionary<ITrackItem, TrackEditorSampler> trackEditorSamplers = new Dictionary<ITrackItem, TrackEditorSampler>();
        private Dictionary<ITrackClip, ITrackClipEditorSampler> clipEditorSamplers = new Dictionary<ITrackClip, ITrackClipEditorSampler>();

        // 事件
        public event System.Action<float> OnTimeUpdated;

        // ========== 采样器管理 ==========

        public void RegisterSampler(IEditorTimeSampler sampler)
        {
            if (sampler == null)
                return;

            try
            {
                samplers.Add(sampler);
                if (sampler is TrackEditorSampler trackSampler && trackSampler.Track != null)
                    trackEditorSamplers[trackSampler.Track] = trackSampler;

                if (sampler is ITrackClipEditorSampler clipSampler && clipSampler.Clip != null)
                    clipEditorSamplers[clipSampler.Clip] = clipSampler;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        public void UnregisterSampler(IEditorTimeSampler sampler)
        {
            if (sampler is IEditorTimeSamplerLifecycle lifecycle)
            {
                try
                {
                    lifecycle.OnEditorPreviewStop();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            if (sampler is TrackEditorSampler trackSampler && trackSampler.Track != null)
                trackEditorSamplers.Remove(trackSampler.Track);

            if (sampler is ITrackClipEditorSampler clipSampler && clipSampler.Clip != null)
                clipEditorSamplers.Remove(clipSampler.Clip);

            samplers.Remove(sampler);
        }

        public void ClearSamplers()
        {
            StopAllSamplers();
            samplers.Clear();
            trackEditorSamplers.Clear();
            clipEditorSamplers.Clear();
        }

        public bool TryGetTrackEditorSampler(ITrackItem track, out TrackEditorSampler sampler)
        {
            if (track != null && trackEditorSamplers.TryGetValue(track, out sampler))
                return true;

            sampler = null;
            return false;
        }

        public TrackEditorSampler GetTrackEditorSampler(ITrackItem track)
        {
            return TryGetTrackEditorSampler(track, out var sampler) ? sampler : null;
        }

        public bool TryGetClipEditorSampler(ITrackClip clip, out ITrackClipEditorSampler sampler)
        {
            if (clip != null && clipEditorSamplers.TryGetValue(clip, out sampler))
                return true;

            sampler = null;
            return false;
        }

        public ITrackClipEditorSampler GetClipEditorSampler(ITrackClip clip)
        {
            return TryGetClipEditorSampler(clip, out var sampler) ? sampler : null;
        }

        // ========== 播放控制 ==========

        public void Play()
        {
            IsPlaying = true;
        }

        public void StartAllSamplers()
        {
            foreach (var sampler in samplers)
            {
                if (sampler is not IEditorTimeSamplerLifecycle lifecycle)
                    continue;

                try
                {
                    lifecycle.OnEditorPreviewStart();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }

        public void SetPreviewIdleWeight(float weight)
        {
            foreach (var sampler in samplers)
            {
                if (sampler is not IEditorPreviewIdleWeightController controller)
                    continue;

                try
                {
                    controller.SetPreviewIdleWeight(weight, CurrentTime);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }

        public void UsePreviewIdleAutoBlend()
        {
            foreach (var sampler in samplers)
            {
                if (sampler is not IEditorPreviewIdleWeightController controller)
                    continue;

                try
                {
                    controller.UsePreviewIdleAutoBlend(CurrentTime);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }

        public void Pause()
        {
            IsPlaying = false;
        }

        public void Stop()
        {
            Pause();
            SetTime(0f);
            StopAllSamplers();
        }

        public void DisposeEditorPreviewTarget()
        {
            Pause();
            StopAllSamplers();
            if (PreviewTarget != null && !PreviewTarget.IsRecycled)
                PreviewTarget.TryAutoPushedToPool();
        }

        /// <summary>
        /// 设置当前时间并立刻采样所有注册对象。
        /// </summary>
        public void SetTime(float time)
        {
            CurrentTime = Mathf.Clamp(time, 0f, Duration);
            SampleAll();
            OnTimeUpdated?.Invoke(CurrentTime);
        }

        /// <summary>
        /// 由全局播放器每帧调用，根据 IsPlaying 和 Speed 推进时间。
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (!IsPlaying)
                return;

            float step = deltaTime * Speed;
            CurrentTime += step;

            // 处理到达边界的情况。
            if (Speed >= 0f && CurrentTime >= Duration)
            {
                CurrentTime = Duration;
                IsPlaying = false;
            }
            else if (Speed < 0f && CurrentTime <= 0f)
            {
                CurrentTime = 0f;
                IsPlaying = false;
            }

            SampleAll();
            OnTimeUpdated?.Invoke(CurrentTime);
        }

        // ========== 内部采样 ==========

        private void SampleAll()
        {
            foreach (var sampler in samplers)
            {
                if (sampler == null)
                    continue;

                try
                {
                    sampler.SampleTime(CurrentTime);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }

        public void StopAllSamplers()
        {
            foreach (var sampler in samplers)
            {
                if (sampler is not IEditorTimeSamplerLifecycle lifecycle)
                    continue;

                try
                {
                    lifecycle.OnEditorPreviewStop();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }
    }

    /// <summary>
    /// 更明确的编辑器预览命名入口；旧的 EditorTimelinePlayer 继续保留兼容现有代码。
    /// </summary>
    public sealed class EditorTimelinePreviewPlayer : EditorTimelinePlayer { }

    public sealed class EditorTrackSequencePreviewPlayer : EditorSequencePlayer { }
#endif

}
