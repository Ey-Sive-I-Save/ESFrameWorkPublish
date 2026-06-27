using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ES
{
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;

    public class EditorTimelinePlayer
    {
        public static EditorTimelinePlayer Instance { get; } = new();

        // 存放当前激活的序列（目前只放一个，未来可放多个）
        private List<EditorSequencePlayer> activeSequences = new List<EditorSequencePlayer>();

        /// <summary> 获取或设置当前主序列（默认是第一个激活的序列） </summary>
        public EditorSequencePlayer ActiveSequence
        {
            get => activeSequences.Count > 0 ? activeSequences[0] : null;
            set
            {
                Stop();                  // 先停止所有播放
                activeSequences.Clear();
                if (value != null)
                    activeSequences.Add(value);
            }
        }

        /// <summary> 添加一个要同时播放的序列（未来扩展用） </summary>
        public void AddSequence(EditorSequencePlayer sequence)
        {
            if (sequence != null && !activeSequences.Contains(sequence))
                activeSequences.Add(sequence);
        }

        /// <summary> 移除一个序列 </summary>
        public void RemoveSequence(EditorSequencePlayer sequence)
        {
            if (sequence != null)
            {
                sequence.Stop();
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
                lastTickTime = EditorApplication.timeSinceStartup; // ★ 关键：初始化时间基准
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

            // 如果所有序列都暂停，注销 update
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
            // 对当前所有激活序列设置时间（每个序列内部会 clamp 到自身 Duration）
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

            // 防止切换窗口后首次 Tick 产生一个巨大的 dt（例如数秒）
            dt = Mathf.Min(dt, 0.1f);

            // 倒序遍历以支持序列自行移除
            for (int i = activeSequences.Count - 1; i >= 0; i--)
                activeSequences[i].Tick(dt);

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

        // —— 播放状态 ——
        public bool IsPlaying { get; private set; }
        public float Speed { get; set; } = 1f;      // 支持倒放
        public float CurrentTime { get; private set; }
        public float Duration { get; set; } = 10f;

        // —— 采样器集合 ——
        private HashSet<IEditorTimeSampler> samplers = new HashSet<IEditorTimeSampler>();

        // —— 事件 ——
        public event System.Action<float> OnTimeUpdated;

        // ========== 采样器管理 ==========

        public void RegisterSampler(IEditorTimeSampler sampler)
        {
            if (sampler != null)
                samplers.Add(sampler);
        }

        public void UnregisterSampler(IEditorTimeSampler sampler)
        {
            samplers.Remove(sampler);
        }

        public void ClearSamplers()
        {
            samplers.Clear();
        }

        // ========== 播放控制 ==========

        public void Play()
        {
            IsPlaying = true;
        }

        public void Pause()
        {
            IsPlaying = false;
        }

        public void Stop()
        {
            Pause();
            SetTime(0f);
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

            // 处理到达边界的情况
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
                sampler.SampleTime(CurrentTime);
        }
    }


}
