using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ES
{
    /// <summary>
    /// 编辑器时间采样器接口。
    /// 所有需要在编辑器时间线下随时间更新的对象（动画、粒子、音频等）都应实现此接口，
    /// 并注册到 EditorSequencePlayer 中。
    /// </summary>
    public interface IEditorTimeSampler
    {
        /// <summary>
        /// 采样指定时间（单位：秒）。
        /// 实现方应在此方法中根据给定时间更新自身状态。
        /// </summary>
        void SampleTime(float time);
    }

     public class DefaultDebugSampler : IEditorTimeSampler
    {
        private readonly string _sequenceName;
        private readonly string _trackName;
        private readonly string _clipName;
        private readonly float _startTime;
        private readonly float _endTime;
        private bool _wasInside;

        public DefaultDebugSampler(string sequenceName, string trackName, ITrackClip clip)
        {
            _sequenceName = sequenceName;
            _trackName = trackName;
            _clipName = clip.DisplayName;
            _startTime = clip.StartTime;
            _endTime = clip.StartTime + clip.DurationTime;
            _wasInside = false;

            Debug.Log($"[DefaultSampler] 创建采样器: 序列={_sequenceName} 轨道={_trackName} 片段={_clipName} 时段=[{_startTime:F2}-{_endTime:F2}]");
        }

        public void SampleTime(float time)
        {
            bool isInside = time >= _startTime && time < _endTime;
            if (isInside && !_wasInside)
            {
                Debug.Log($"[DefaultSampler] ▶ 进入片段 | 序列:{_sequenceName} 轨道:{_trackName} 片段:{_clipName} 时间:{time:F2}");
            }
            else if (!isInside && _wasInside)
            {
                Debug.Log($"[DefaultSampler] ◼ 离开片段 | 序列:{_sequenceName} 轨道:{_trackName} 片段:{_clipName} 时间:{time:F2}");
            }
            _wasInside = isInside;
        }
    }
   

}
