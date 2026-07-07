using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ES
{
    /// <summary>
    /// 编辑器时间采样器接口�?
    /// 所有需要在编辑器时间线下随时间更新的对象（动画、粒子、音频等）都应实现此接口�?
    /// 并注册到 EditorSequencePlayer 中�?
    /// </summary>
    public interface IEditorTimeSampler
    {
        /// <summary>
        /// 采样指定时间（单位：秒）�?        /// 实现方应在此方法中根据给定时间更新自身状态�?        /// </summary>
        void SampleTime(float time);
    }

    /// <summary>
    /// 编辑器时间采样器生命周期接口�?    /// 需要恢复现场或释放编辑器临时对象的采样器实现此接口�?    /// </summary>
    public interface IEditorTimeSamplerLifecycle
    {
        void OnEditorPreviewStart();
        void OnEditorPreviewStop();
    }

    public abstract class EditorTimeSamplerBase : IEditorTimeSampler, IEditorTimeSamplerLifecycle
    {
        public virtual void OnEditorPreviewStart() { }
        public virtual void OnEditorPreviewStop() { }
        public abstract void SampleTime(float time);
    }

    public interface ITrackClipEditorSampler : IEditorTimeSampler
    {
        ITrackClip Clip { get; }
        IEditorTimeSampler InnerSampler { get; }
    }

    public class TrackEditorSampler : EditorTimeSamplerBase
    {
        public ITrackItem Track { get; }
        public object EditorTarget { get; }
        private readonly bool ownsEditorTarget;

        public TrackEditorSampler(ITrackItem track, object editorTarget, bool ownsEditorTarget)
        {
            Track = track;
            EditorTarget = editorTarget;
            this.ownsEditorTarget = ownsEditorTarget;
        }

        public override void SampleTime(float time) { }

        public override void OnEditorPreviewStop()
        {
            if (ownsEditorTarget && EditorTarget is IPoolableAuto poolable && !poolable.IsRecycled)
                poolable.TryAutoPushedToPool();
        }
    }

    public sealed class TrackClipEditorSampler : EditorTimeSamplerBase, ITrackClipEditorSampler
    {
        public ITrackClip Clip { get; }
        public IEditorTimeSampler InnerSampler { get; }

        public TrackClipEditorSampler(ITrackClip clip, IEditorTimeSampler innerSampler)
        {
            Clip = clip;
            InnerSampler = innerSampler;
        }

        public override void OnEditorPreviewStart()
        {
            if (InnerSampler is IEditorTimeSamplerLifecycle lifecycle)
                lifecycle.OnEditorPreviewStart();
        }

        public override void SampleTime(float time)
        {
            InnerSampler?.SampleTime(time);
        }

        public override void OnEditorPreviewStop()
        {
            if (InnerSampler is IEditorTimeSamplerLifecycle lifecycle)
                lifecycle.OnEditorPreviewStop();
        }
    }

    public class DefaultEditorDebugSampler : EditorTimeSamplerBase
    {
        private readonly string _sequenceName;
        private readonly string _trackName;
        private readonly string _clipName;
        private readonly float _startTime;
        private readonly float _endTime;
        private bool _wasInside;

        public DefaultEditorDebugSampler(string sequenceName, string trackName, ITrackClip clip)
        {
            _sequenceName = sequenceName;
            _trackName = trackName;
            _clipName = clip.DisplayName;
            _startTime = clip.StartTime;
            _endTime = clip.StartTime + clip.DurationTime;
            _wasInside = false;

            Debug.Log($"[DefaultEditorDebugSampler] 创建采样�? 序列={_sequenceName} 轨道={_trackName} 片段={_clipName} 时段=[{_startTime:F2}-{_endTime:F2}]");
        }

        public override void SampleTime(float time)
        {
            bool isInside = time >= _startTime && time < _endTime;
            if (isInside && !_wasInside)
            {
                Debug.Log($"[DefaultEditorDebugSampler] �?进入片段 | 序列:{_sequenceName} 轨道:{_trackName} 片段:{_clipName} 时间:{time:F2}");
            }
            else if (!isInside && _wasInside)
            {
                Debug.Log($"[DefaultEditorDebugSampler] �?离开片段 | 序列:{_sequenceName} 轨道:{_trackName} 片段:{_clipName} 时间:{time:F2}");
            }
            _wasInside = isInside;
        }
    }
   

}
