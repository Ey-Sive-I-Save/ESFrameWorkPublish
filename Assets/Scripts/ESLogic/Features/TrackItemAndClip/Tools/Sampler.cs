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

    #region  閲囨牱鍣?


#if UNITY_EDITOR
    public class AnimationTrackEditorSampler : TrackEditorSampler
    {
        private readonly string _trackName;
        private readonly List<AnimationClipEditorSampler> _clips = new List<AnimationClipEditorSampler>();
        private bool _loggedEmpty;

        public AnimationTrackEditorSampler(ITrackItem track, string trackName)
            : base(track, null, false)
        {
            _trackName = string.IsNullOrEmpty(trackName) ? "AnimationTrackEditorSampler" : trackName;
        }

        public void AddClipSampler(AnimationClipEditorSampler sampler)
        {
            if (sampler != null)
                _clips.Add(sampler);
        }

        public override void OnEditorPreviewStart()
        {
            EnsureAnimationMode();
        }

        public override void OnEditorPreviewStop()
        {
            if (AnimationMode.InAnimationMode())
                AnimationMode.StopAnimationMode();
        }

        public override void SampleTime(float time)
        {
            if (_clips.Count == 0)
            {
                LogEmptyOnce();
                return;
            }

            bool useEndFrame = false;
            AnimationClipEditorSampler sampler = FindSamplerAtTime(time);
            if (sampler == null)
                sampler = FindGapSampler(time, out useEndFrame);

            if (sampler == null)
                return;

            EnsureAnimationMode();
            sampler.SampleAnimation(time, useEndFrame);
        }

        private AnimationClipEditorSampler FindSamplerAtTime(float time)
        {
            for (int i = 0; i < _clips.Count; i++)
            {
                AnimationClipEditorSampler sampler = _clips[i];
                if (sampler != null && sampler.ContainsTime(time))
                    return sampler;
            }

            return null;
        }

        private AnimationClipEditorSampler FindGapSampler(float time, out bool useEndFrame)
        {
            AnimationClipEditorSampler previous = null;
            AnimationClipEditorSampler next = null;
            float previousEndTime = float.NegativeInfinity;
            float nextStartTime = float.PositiveInfinity;
            useEndFrame = false;

            for (int i = 0; i < _clips.Count; i++)
            {
                AnimationClipEditorSampler sampler = _clips[i];
                if (sampler == null || !sampler.CanSample)
                    continue;

                if (sampler.EndTime <= time && sampler.EndTime > previousEndTime)
                {
                    previous = sampler;
                    previousEndTime = sampler.EndTime;
                }

                if (sampler.StartTime > time && sampler.StartTime < nextStartTime)
                {
                    next = sampler;
                    nextStartTime = sampler.StartTime;
                }
            }

            if (previous != null)
            {
                useEndFrame = true;
                return previous;
            }

            useEndFrame = false;
            return next;
        }

        private static void EnsureAnimationMode()
        {
            if (!AnimationMode.InAnimationMode())
                AnimationMode.StartAnimationMode();
        }

        private void LogEmptyOnce()
        {
            if (_loggedEmpty)
                return;

            _loggedEmpty = true;
            Debug.LogWarning($"[AnimationTrackEditorSampler] Track has no animation clips | Track={_trackName}");
        }
    }

    public class AnimationClipEditorSampler : EditorTimeSamplerBase
    {
        private readonly AnimationClip _clip;
        private readonly float _startTime;
        private readonly float _durationTime;
        private readonly GameObject _target;
        private bool _loggedInvalidSetup;

        public float StartTime => _startTime;
        public float EndTime => _startTime + _durationTime;
        public bool CanSample => _clip != null && _target != null;

        public AnimationClipEditorSampler(GameObject target, AnimationClip clip, float startTime)
            : this(target, clip, startTime, clip != null ? clip.length : 0f)
        {
        }

        public AnimationClipEditorSampler(GameObject target, AnimationClip clip, float startTime, float durationTime)
        {
            _target = target;
            _clip = clip;
            _startTime = startTime;
            _durationTime = Mathf.Max(0f, durationTime);
        }

        public override void OnEditorPreviewStart()
        {
            LogInvalidSetupOnce();
        }

        public override void OnEditorPreviewStop()
        {
        }

        public override void SampleTime(float time)
        {
        }

        public bool ContainsTime(float time)
        {
            return CanSample && time >= _startTime && time < EndTime;
        }

        public void SampleAnimation(float sequenceTime, bool useEndFrame)
        {
            if (_clip == null || _target == null)
            {
                LogInvalidSetupOnce();
                return;
            }

            float localTime = useEndFrame ? _durationTime : sequenceTime - _startTime;
            localTime = Mathf.Clamp(localTime, 0f, _durationTime);
            localTime = Mathf.Clamp(localTime, 0f, _clip.length);
            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(_target, _clip, localTime);
            AnimationMode.EndSampling();
            SceneView.RepaintAll();
        }

        private void LogInvalidSetupOnce()
        {
            if (_loggedInvalidSetup || (_clip != null && _target != null))
                return;

            _loggedInvalidSetup = true;
            Debug.LogWarning($"AnimationClipEditorSampler cannot sample. Target={(_target != null ? _target.name : "<None>")}, Clip={(_clip != null ? _clip.name : "<None>")}");
        }
    }
#endif
#if UNITY_EDITOR
    public class GameObjectTrackEditorSampler : TrackEditorSampler
    {
        private struct TargetState
        {
            public GameObject Target;
            public bool OriginalActive;
            public bool HasOriginal;
            public bool LastAppliedActive;
            public bool HasAppliedActive;
            public int LastRequestFrame;
            public bool RequestedActive;
        }

        private readonly string _trackName;
        private readonly bool _debug;
        private readonly List<TargetState> _targets = new List<TargetState>();
        private int _sampleFrame;
        private float _lastSubmitTime = float.NaN;

        public GameObjectTrackEditorSampler(ITrackItem track, object editorTarget, bool ownsEditorTarget, string trackName, bool debug)
            : base(track, editorTarget, ownsEditorTarget)
        {
            _trackName = string.IsNullOrEmpty(trackName) ? "GameObjectTrack" : trackName;
            _debug = debug;
            LogDebug($"Created | EditorTarget={editorTarget}");
        }

        public GameObject GetInheritedTarget()
        {
            return EditorTarget is ESRuntimeTarget runtimeTarget ? runtimeTarget.GetGameObject() : null;
        }

        public string GetInheritedTargetDebugInfo()
        {
            if (EditorTarget == null)
                return "InheritTrackTarget TrackEditorSampler.EditorTarget=null";

            if (EditorTarget is not ESRuntimeTarget runtimeTarget)
                return $"InheritTrackTarget EditorTargetType={EditorTarget.GetType().Name} is not ESRuntimeTarget";

            if (runtimeTarget.userEntity == null)
                return "InheritTrackTarget runtimeTarget.userEntity=null";

            GameObject gameObject = runtimeTarget.GetGameObject();
            return gameObject != null
                ? "InheritTrackTarget from TrackEditorSampler"
                : $"InheritTrackTarget userEntity={runtimeTarget.userEntity.name} gameObject=null";
        }

        public override void SampleTime(float time)
        {
            BeginFrame(time);

            for (int i = 0; i < _targets.Count; i++)
            {
                TargetState targetState = _targets[i];
                if (targetState.Target == null)
                    continue;

                if (!targetState.HasOriginal)
                {
                    targetState.OriginalActive = targetState.Target.activeSelf;
                    targetState.HasOriginal = true;
                    LogDebug($"CacheOriginal | Target={GetTargetName(targetState.Target)} | OriginalActive={targetState.OriginalActive}");
                }

                bool hasRequest = targetState.LastRequestFrame == _sampleFrame;
                bool finalActive = hasRequest ? targetState.RequestedActive : targetState.OriginalActive;
                ApplyActiveState(ref targetState, finalActive, hasRequest);
                _targets[i] = targetState;
            }
        }

        public void SubmitClipState(string clipName, GameObject target, bool activate, bool isInside, float time)
        {
            if (target == null)
                return;

            BeginFrame(time);

            int targetIndex = EnsureTarget(target);
            TargetState targetState = _targets[targetIndex];
            if (!targetState.HasOriginal)
            {
                targetState.OriginalActive = target.activeSelf;
                targetState.HasOriginal = true;
                LogDebug($"CacheOriginal | Target={GetTargetName(target)} | OriginalActive={targetState.OriginalActive}");
            }

            if (isInside)
            {
                targetState.RequestedActive = activate;
                targetState.LastRequestFrame = _sampleFrame;
                LogDebug($"Submit | Clip={clipName} | Time={time:F3} | Target={GetTargetName(target)} | Active={activate}");
            }

            bool hasRequest = targetState.LastRequestFrame == _sampleFrame;
            bool finalActive = hasRequest ? targetState.RequestedActive : targetState.OriginalActive;
            ApplyActiveState(ref targetState, finalActive, hasRequest);
            _targets[targetIndex] = targetState;
        }

        private void BeginFrame(float time)
        {
            if (!float.IsNaN(_lastSubmitTime) && Mathf.Approximately(_lastSubmitTime, time))
                return;

            _sampleFrame++;
            _lastSubmitTime = time;
        }

        public override void OnEditorPreviewStop()
        {
            for (int i = 0; i < _targets.Count; i++)
            {
                TargetState targetState = _targets[i];
                if (targetState.Target == null || !targetState.HasOriginal)
                    continue;

                LogDebug($"StopRestore | Target={GetTargetName(targetState.Target)} | Restore={targetState.OriginalActive}");
                ApplyActiveState(ref targetState, targetState.OriginalActive, false);
                _targets[i] = targetState;
            }

            base.OnEditorPreviewStop();
        }

        private int EnsureTarget(GameObject target)
        {
            for (int i = 0; i < _targets.Count; i++)
            {
                if (_targets[i].Target == target)
                    return i;
            }

            _targets.Add(new TargetState { Target = target });
            return _targets.Count - 1;
        }

        private void ApplyActiveState(ref TargetState targetState, bool activeState, bool hasActiveClip)
        {
            if (targetState.HasAppliedActive && targetState.LastAppliedActive == activeState && targetState.Target.activeSelf == activeState)
                return;

            LogDebug($"SetActive | Target={GetTargetName(targetState.Target)} | From={targetState.Target.activeSelf} | To={activeState} | HasActiveClip={hasActiveClip}");
            targetState.Target.SetActive(activeState);
            targetState.LastAppliedActive = activeState;
            targetState.HasAppliedActive = true;
        }

        private void LogDebug(string message)
        {
            if (!_debug)
                return;

            //Debug.Log($"[GameObjectTrackEditorSampler] {_trackName} | {message}");
        }

        private static string GetTargetName(GameObject target)
        {
            return target != null ? target.name : "<None>";
        }
    }

    public class GameObjectEditorSampler : EditorTimeSamplerBase
    {
        private readonly GameObjectTrackEditorSampler _trackSampler;
        private readonly GameObject _target;
        private readonly bool _activate;
        private readonly float _startTime;
        private readonly float _durationTime;
        private readonly string _debugName;
        private readonly string _targetSource;
        private readonly bool _debug;
        private bool _originalActiveState;
        private bool _hasCachedOriginal;
        private bool _lastAppliedActiveState;
        private bool _hasAppliedActiveState;
        private bool _wasInside;
        private bool _loggedInvalidTarget;

        public GameObjectEditorSampler(GameObject target, bool activate, float startTime)
            : this(null, target, activate, startTime, float.PositiveInfinity, null, null, false)
        {
        }

        public GameObjectEditorSampler(GameObject target, bool activate, float startTime, float durationTime)
            : this(null, target, activate, startTime, durationTime, null, null, false)
        {
        }

        public GameObjectEditorSampler(GameObject target, bool activate, float startTime, float durationTime, string debugName, bool debug)
            : this(null, target, activate, startTime, durationTime, debugName, null, debug)
        {
        }

        public GameObjectEditorSampler(GameObjectTrackEditorSampler trackSampler, GameObject target, bool activate, float startTime, float durationTime, string debugName, string targetSource, bool debug)
        {
            _trackSampler = trackSampler;
            _target = target;
            _activate = activate;
            _startTime = startTime;
            _durationTime = durationTime > 0f ? durationTime : 0f;
            _debugName = string.IsNullOrEmpty(debugName) ? "GameObjectEditorSampler" : debugName;
            _targetSource = string.IsNullOrEmpty(targetSource) ? "<None>" : targetSource;
            _debug = debug;
            LogDebug($"Created | Target={GetTargetName()} | Source={_targetSource} | Activate={_activate} | Start={_startTime:F3} | Duration={_durationTime:F3}");
        }

        public override void SampleTime(float time)
        {
            if (_target == null)
            {
                LogInvalidTargetOnce(time);
                return;
            }

            // 棣栨閲囨牱鏃剁紦瀛樺師濮嬬姸鎬?
            if (!_hasCachedOriginal)
            {
                _originalActiveState = _target.activeSelf;
                _hasCachedOriginal = true;
                LogDebug($"CacheOriginal | Target={GetTargetName()} | OriginalActive={_originalActiveState}");
            }

            bool isInside = time >= _startTime && time < _startTime + _durationTime;
            if (isInside != _wasInside)
            {
                LogDebug(isInside
                    ? $"EnterClip | Time={time:F3} | Target={GetTargetName()}"
                    : $"ExitClip | Time={time:F3} | Target={GetTargetName()}");
                _wasInside = isInside;
            }

            if (_trackSampler != null)
            {
                _trackSampler.SubmitClipState(_debugName, _target, _activate, isInside, time);
                return;
            }

            bool targetActiveState = isInside ? _activate : _originalActiveState;
            ApplyActiveState(targetActiveState);
        }

        public override void OnEditorPreviewStop()
        {
            if (_trackSampler == null && _target != null && _hasCachedOriginal)
            {
                LogDebug($"StopRestore | Target={GetTargetName()} | Restore={_originalActiveState}");
                ApplyActiveState(_originalActiveState);
            }

            _wasInside = false;
        }

        private void ApplyActiveState(bool activeState)
        {
            if (_hasAppliedActiveState && _lastAppliedActiveState == activeState && _target.activeSelf == activeState)
                return;

            LogDebug($"SetActive | Target={GetTargetName()} | From={_target.activeSelf} | To={activeState}");
            _target.SetActive(activeState);
            _lastAppliedActiveState = activeState;
            _hasAppliedActiveState = true;
        }

        private void LogInvalidTargetOnce(float time)
        {
            if (_loggedInvalidTarget)
                return;

            _loggedInvalidTarget = true;
            Debug.LogWarning($"[GameObjectEditorSampler] Target is null | Sampler={_debugName} | Source={_targetSource} | Time={time:F3} | Activate={_activate} | Start={_startTime:F3} | Duration={_durationTime:F3}");
        }

        private void LogDebug(string message)
        {
            if (!_debug)
                return;

            Debug.Log($"[GameObjectEditorSampler] {_debugName} | {message}");
        }

        private string GetTargetName()
        {
            return _target != null ? _target.name : "<None>";
        }
    }
#endif
    public class ParticleEditorSampler : EditorTimeSamplerBase
    {
        private ParticleSystem particleSystem;
        private float startTime; // 杞ㄩ亾璧峰鏃堕棿鍋忕Щ

        public ParticleEditorSampler(ParticleSystem ps, float trackStartTime = 0f)
        {
            particleSystem = ps;
            startTime = trackStartTime;
        }

        public override void SampleTime(float time)
        {
            if (particleSystem == null) return;

            float localTime = Mathf.Max(0, time - startTime);
            // 妯℃嫙绮掑瓙鍒版寚瀹氭椂闂达紝restart=true 琛ㄧず浠庡垵濮嬬姸鎬佸紑濮嬫ā鎷?
            particleSystem.Simulate(localTime, true, true);

            // 濡傛灉鏃堕棿鎺ヨ繎0锛屽仠姝㈢矑瀛愬苟娓呴櫎
            if (localTime < 0.01f)
                particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        public override void OnEditorPreviewStop()
        {
            if (particleSystem != null)
                particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }


#if UNITY_EDITOR
    public class AudioEditorSampler : EditorTimeSamplerBase
{
    private readonly AudioClip _clip;
    private readonly float _startTime;
    private AudioSource _audioSource;
    private float _lastSampledTime = -1f;
    private const float SeekThreshold = 0.05f;

    public AudioEditorSampler(AudioClip clip, float startTime)
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
        _audioSource.Stop(); // 纭繚鍒濆鐘舵€佷负鍋滄
    }

    public override void SampleTime(float time)
    {
        if (_clip == null || _audioSource == null) return;

        float localTime = time - _startTime;
        bool isValid = localTime >= 0 && localTime < _clip.length;

        if (!isValid)
        {
            // 涓嶅湪鏈夋晥鍖洪棿鍐咃細鍋滄鎾斁骞舵竻闄ゆ渶鍚庨噰鏍疯褰?
            if (_audioSource.isPlaying)
                _audioSource.Stop();
            _lastSampledTime = -1f;
            return;
        }

        // 鏈夋晥鍖洪棿鍐咃細鏍规嵁鏃堕棿鍙樺寲鍐冲畾鏄惁閲嶆柊瀹氫綅
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

    public override void OnEditorPreviewStop()
    {
        if (_audioSource != null)
        {
            _audioSource.Stop();
            UnityEngine.Object.DestroyImmediate(_audioSource.gameObject);
            _audioSource = null;
        }
    }
}
#endif
    #endregion


    #region  閲囨牱鏄犲皠

#if UNITY_EDITOR
    public class EditorSamplerRegistry
    {
        private readonly Dictionary<ITrackClip, IEditorTimeSampler>
            _map = new();

        public void Rebuild(ITrackSequence sequence)
        {
            StopAll();
            _map.Clear();

            if (sequence == null || sequence.Tracks == null)
                return;

            foreach (var track in sequence.Tracks)
            {
                if (track == null || track.Clips == null)
                    continue;

                foreach (var clip in track.Clips)
                {
                    if (clip == null)
                        continue;

                    var sampler = clip.CreateSampler(sequence, track);
                    if (sampler == null)
                    {
                        // 娌℃湁涓撶敤閲囨牱鍣ㄦ椂锛屼娇鐢ㄩ粯璁よ皟璇曢噰鏍峰櫒
                        sampler = new DefaultEditorDebugSampler(sequence.Name, track.DisplayName, clip);
                    }
                    _map[clip] = sampler;
                }
            }
        }
        public void Tick(float time)
        {
            foreach (var sampler in _map.Values)
            {
                if (sampler == null)
                    continue;

                try
                {
                    sampler.SampleTime(time);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }

        public void StopAll()
        {
            foreach (var sampler in _map.Values)
            {
                if (sampler is IEditorTimeSamplerLifecycle lifecycle)
                    lifecycle.OnEditorPreviewStop();
            }
        }
    }
#endif

    #endregion
}
