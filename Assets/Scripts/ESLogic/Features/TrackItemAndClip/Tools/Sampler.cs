using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;


namespace ES
{

    #region  采样器


#if UNITY_EDITOR
    public class AnimationSampler : IEditorTimeSampler
    {
        private readonly AnimationClip _clip;
        private readonly float _startTime;
        private readonly GameObject _target; // 新增

        public AnimationSampler(GameObject target, AnimationClip clip, float startTime)
        {
            _target = target;
            _clip = clip;
            _startTime = startTime;
        }

        public void SampleTime(float time)
        {
            if (_clip == null || _target == null) return;

            float localTime = time - _startTime;
            if (localTime < 0 || localTime > _clip.length)
                return;

            AnimationMode.SampleAnimationClip(_target, _clip, localTime);
        }
    }
#endif
#if UNITY_EDITOR
    public class GameObjectSampler : IEditorTimeSampler
    {
        private readonly GameObject _target;
        private readonly bool _activate;
        private readonly float _startTime;
        private bool _originalActiveState;
        private bool _hasCachedOriginal;

        public GameObjectSampler(GameObject target, bool activate, float startTime)
        {
            _target = target;
            _activate = activate;
            _startTime = startTime;
        }

        public void SampleTime(float time)
        {
            if (_target == null) return;

            // 首次采样时缓存原始状态
            if (!_hasCachedOriginal)
            {
                _originalActiveState = _target.activeSelf;
                _hasCachedOriginal = true;
            }

            if (time >= _startTime)
                _target.SetActive(_activate);
            else
                _target.SetActive(_originalActiveState); // 倒回时恢复
        }

        public void Stop()
        {
            // 停止预览时恢复原始状态
            if (_target != null && _hasCachedOriginal)
                _target.SetActive(_originalActiveState);
        }
    }
#endif
    public class ParticleSampler : IEditorTimeSampler
    {
        private ParticleSystem particleSystem;
        private float startTime; // 轨道起始时间偏移

        public ParticleSampler(ParticleSystem ps, float trackStartTime = 0f)
        {
            particleSystem = ps;
            startTime = trackStartTime;
        }

        public void SampleTime(float time)
        {
            if (particleSystem == null) return;

            float localTime = Mathf.Max(0, time - startTime);
            // 模拟粒子到指定时间，restart=true 表示从初始状态开始模拟
            particleSystem.Simulate(localTime, true, true);

            // 如果时间接近0，停止粒子并清除
            if (localTime < 0.01f)
                particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }


    public class AudioSampler : IEditorTimeSampler
{
    private readonly AudioClip _clip;
    private readonly float _startTime;
    private AudioSource _audioSource;
    private float _lastSampledTime = -1f;
    private const float SeekThreshold = 0.05f;

    public AudioSampler(AudioClip clip, float startTime)
    {
        _clip = clip;
        _startTime = startTime;
        CreateAudioSource();
    }

    private void CreateAudioSource()
    {
        if (_clip == null) return;
        var go = new GameObject($"AudioPreview_{_clip.name}_{GetHashCode()}")
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        _audioSource = go.AddComponent<AudioSource>();
        _audioSource.playOnAwake = false;
        _audioSource.clip = _clip;
        _audioSource.Stop(); // 确保初始状态为停止
    }

    public void SampleTime(float time)
    {
        if (_clip == null || _audioSource == null) return;

        float localTime = time - _startTime;
        bool isValid = localTime >= 0 && localTime < _clip.length;

        if (!isValid)
        {
            // 不在有效区间内：停止播放并清除最后采样记录
            if (_audioSource.isPlaying)
                _audioSource.Stop();
            _lastSampledTime = -1f;
            return;
        }

        // 有效区间内：根据时间变化决定是否重新定位
        bool shouldSeek = !_audioSource.isPlaying ||
                          Mathf.Abs(localTime - _lastSampledTime) > SeekThreshold;

        if (shouldSeek)
        {
            _audioSource.time = localTime;
            if (!_audioSource.isPlaying)
                _audioSource.Play();
        }

        _lastSampledTime = localTime;
    }

    public void Stop()
    {
        if (_audioSource != null)
        {
            _audioSource.Stop();
            UnityEngine.Object.DestroyImmediate(_audioSource.gameObject);
            _audioSource = null;
        }
    }
}
    #endregion


    #region  采样映射

    public class EditorSamplerRegistry
    {
        private readonly Dictionary<ITrackClip, IEditorTimeSampler>
            _map = new();

        public void Rebuild(ITrackSequence sequence)
        {
            StopAll();
            _map.Clear();

            foreach (var track in sequence.Tracks)
            {
                foreach (var clip in track.Clips)
                {
                    var sampler = clip.CreateSampler(sequence, track);
                    if (sampler == null)
                    {
                        // 没有专用采样器时，使用默认调试采样器
                        sampler = new DefaultDebugSampler(sequence.Name, track.DisplayName, clip);
                    }
                    _map[clip] = sampler;
                }
            }
        }
        public void Tick(float time)
        {
            foreach (var sampler in _map.Values)
            {
                sampler.SampleTime(time);
            }
        }

        public void StopAll()
        {
            // foreach (var sampler in _map.Values)
            // {
            //     sampler.Stop();
            // }
        }
    }

    #endregion
}