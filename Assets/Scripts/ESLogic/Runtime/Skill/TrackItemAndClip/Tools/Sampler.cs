using ES;
using System;
using System.Collections;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
#if UNITY_EDITOR
using UnityEngine.Animations;
using UnityEngine.Playables;
#endif


namespace ES
{

    #region 采样器


#if UNITY_EDITOR
    public interface IEditorPreviewIdleWeightController
    {
        void SetPreviewIdleWeight(float weight, float time);
        void UsePreviewIdleAutoBlend(float time);
    }

    public class AnimationTrackEditorSampler : TrackEditorSampler
    {
        private readonly string _trackName;
        private readonly List<AnimationClipEditorSampler> _clips = new List<AnimationClipEditorSampler>();
        private bool _loggedEmpty;
        private bool _animationModeAcquired;
        private static int activeAnimationModeUsers;
        private static bool animationModeOwned;

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
            if (_animationModeAcquired)
                return;

            try
            {
                if (activeAnimationModeUsers == 0 && !AnimationMode.InAnimationMode())
                {
                    AnimationMode.StartAnimationMode();
                    animationModeOwned = true;
                }

                activeAnimationModeUsers++;
                _animationModeAcquired = true;
            }
            catch
            {
                if (animationModeOwned && activeAnimationModeUsers == 0)
                {
                    try { AnimationMode.StopAnimationMode(); }
                    catch (Exception cleanupException) { Debug.LogException(cleanupException); }
                    animationModeOwned = false;
                }
                throw;
            }
        }

        public override void OnEditorPreviewStop()
        {
            if (!_animationModeAcquired)
                return;

            _animationModeAcquired = false;
            activeAnimationModeUsers = Mathf.Max(0, activeAnimationModeUsers - 1);
            if (activeAnimationModeUsers == 0 && animationModeOwned)
            {
                try
                {
                    // Unity or another editor tool may have already ended the
                    // global mode while this sampler was still registered.
                    // Avoid a second Stop call in that case.
                    if (AnimationMode.InAnimationMode())
                        AnimationMode.StopAnimationMode();
                }
                finally { animationModeOwned = false; }
            }
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
                return;

            OnEditorPreviewStart();
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
        private readonly float _clipStartOffset;
        private readonly float _playbackSpeed;
        private readonly bool _loopClip;
        private readonly GameObject _target;
        private bool _loggedInvalidSetup;

        public AnimationClip Clip => _clip;
        public GameObject Target => _target;
        public float StartTime => _startTime;
        public float DurationTime => _durationTime;
        public float EndTime => _startTime + _durationTime;
        public bool CanSample => _clip != null && _target != null && _durationTime > 0.0001f && _clip.length > 0.0001f;

        public AnimationClipEditorSampler(GameObject target, AnimationClip clip, float startTime)
            : this(target, clip, startTime, clip != null ? clip.length : 0f)
        {
        }

        public AnimationClipEditorSampler(GameObject target, AnimationClip clip, float startTime, float durationTime)
            : this(target, clip, startTime, durationTime, 0f, 1f, false)
        {
        }

        public AnimationClipEditorSampler(GameObject target, AnimationClip clip, float startTime, float durationTime, float clipStartOffset, float playbackSpeed, bool loopClip)
        {
            _target = target;
            _clip = clip;
            _startTime = startTime;
            _durationTime = Mathf.Max(0f, durationTime);
            _clipStartOffset = Mathf.Max(0f, clipStartOffset);
            _playbackSpeed = Mathf.Max(0.01f, playbackSpeed);
            _loopClip = loopClip;
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

            float elapsed = useEndFrame ? _durationTime : sequenceTime - _startTime;
            elapsed = Mathf.Clamp(elapsed, 0f, _durationTime);
            float localTime = _clipStartOffset + elapsed * _playbackSpeed;
            localTime = _loopClip ? Mathf.Repeat(localTime, _clip.length) : Mathf.Clamp(localTime, 0f, _clip.length);
            AnimationMode.BeginSampling();
            try
            {
                AnimationMode.SampleAnimationClip(_target, _clip, localTime);
            }
            finally
            {
                AnimationMode.EndSampling();
            }
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

    public sealed class AdvancedAnimationTrackEditorSampler : TrackEditorSampler, IEditorPreviewIdleWeightController
    {
        private sealed class TargetMixer : IDisposable
        {
            private struct TransformPose
            {
                public Transform Transform;
                public Vector3 LocalPosition;
                public Quaternion LocalRotation;
                public Vector3 LocalScale;
            }

            public GameObject Target;
            public Animator Animator;
            public PlayableGraph Graph;
            public AnimationMixerPlayable Mixer;
            public AnimationClipPlayable BasePosePlayable;
            public AnimationClip BasePoseClip;
            public int BasePoseIndex = -1;
            public readonly List<AnimationClipPlayable> Playables = new List<AnimationClipPlayable>();
            public readonly List<AdvancedAnimationClipEditorSampler> Clips = new List<AdvancedAnimationClipEditorSampler>();
            public readonly List<float> Weights = new List<float>();
            public readonly List<float> LocalTimes = new List<float>();
            private TransformPose[] _originalPose;

            public bool IsValid => Animator != null && Graph.IsValid() && Mixer.IsValid();

            public void CaptureOriginalPose()
            {
                Transform root = Animator != null ? Animator.transform : Target != null ? Target.transform : null;
                if (root == null)
                {
                    _originalPose = null;
                    return;
                }

                Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
                _originalPose = new TransformPose[transforms.Length];
                for (int i = 0; i < transforms.Length; i++)
                {
                    Transform t = transforms[i];
                    _originalPose[i] = new TransformPose
                    {
                        Transform = t,
                        LocalPosition = t.localPosition,
                        LocalRotation = t.localRotation,
                        LocalScale = t.localScale
                    };
                }
            }

            public void RestoreOriginalPose()
            {
                if (_originalPose == null)
                    return;

                for (int i = 0; i < _originalPose.Length; i++)
                {
                    TransformPose pose = _originalPose[i];
                    if (pose.Transform == null)
                        continue;

                    pose.Transform.localPosition = pose.LocalPosition;
                    pose.Transform.localRotation = pose.LocalRotation;
                    pose.Transform.localScale = pose.LocalScale;
                }
            }

            public void Dispose()
            {
                if (Graph.IsValid())
                    Graph.Destroy();

                BasePoseIndex = -1;
                BasePoseClip = null;
                Playables.Clear();
                Clips.Clear();
                Weights.Clear();
                LocalTimes.Clear();
            }
        }

        private readonly string _trackName;
        private readonly float _transitionDuration;
        private readonly GameObject _defaultTarget;
        private readonly bool _allowIdleOnlyFallback;
        private readonly List<AdvancedAnimationClipEditorSampler> _clips = new List<AdvancedAnimationClipEditorSampler>();
        private readonly List<TargetMixer> _mixers = new List<TargetMixer>();
        private float _forcedIdleWeight;
        private bool _graphReady;
        private bool _loggedPreviewDiagnostics;

        public AdvancedAnimationTrackEditorSampler(ITrackItem track, string trackName, float transitionDuration)
            : this(track, trackName, transitionDuration, null, false)
        {
        }

        public AdvancedAnimationTrackEditorSampler(ITrackItem track, string trackName, float transitionDuration, GameObject defaultTarget)
            : this(track, trackName, transitionDuration, defaultTarget, false)
        {
        }

        public AdvancedAnimationTrackEditorSampler(ITrackItem track, string trackName, float transitionDuration, GameObject defaultTarget, bool allowIdleOnlyFallback)
            : base(track, null, false)
        {
            _trackName = string.IsNullOrEmpty(trackName) ? "AdvancedAnimationTrackEditorSampler" : trackName;
            _transitionDuration = Mathf.Max(0.35f, transitionDuration);
            _defaultTarget = defaultTarget;
            _allowIdleOnlyFallback = allowIdleOnlyFallback;
        }

        public void AddClipSampler(AdvancedAnimationClipEditorSampler sampler)
        {
            if (sampler != null)
                _clips.Add(sampler);
        }

        public override void OnEditorPreviewStart()
        {
            EnsureGraph();
        }

        public override void OnEditorPreviewStop()
        {
            DestroyGraphs();
        }

        public void SetPreviewIdleWeight(float weight, float time)
        {
            _forcedIdleWeight = Mathf.Clamp01(weight);
            SampleCurrentState(time);
        }

        public void UsePreviewIdleAutoBlend(float time)
        {
            _forcedIdleWeight = 0f;
            SampleCurrentState(time);
        }

        private void SampleCurrentState(float time)
        {
            EnsureGraph();
            if (!_graphReady)
                return;

            if (_mixers.Count == 0 && _allowIdleOnlyFallback)
                CreateIdleOnlyMixer();

            for (int i = 0; i < _mixers.Count; i++)
                SampleMixer(_mixers[i], time);

            SceneView.RepaintAll();
        }

        public override void SampleTime(float time)
        {
            EnsureGraph();
            if (!_graphReady)
                return;

            for (int i = 0; i < _mixers.Count; i++)
                SampleMixer(_mixers[i], time);

            SceneView.RepaintAll();
        }

        private void EnsureGraph()
        {
            if (_graphReady)
                return;

            DestroyGraphs();
            _clips.Sort((a, b) => a.StartTime.CompareTo(b.StartTime));

            for (int i = 0; i < _clips.Count; i++)
            {
                var clip = _clips[i];
                if (clip == null || !clip.CanSample)
                    continue;

                Animator animator = ResolveAnimator(clip.Target);
                if (animator == null)
                {
                    clip.LogInvalidSetupOnce();
                    continue;
                }

                TargetMixer mixer = FindOrCreateMixer(clip.Target, animator);
                if (mixer == null)
                    continue;

                var playable = AnimationClipPlayable.Create(mixer.Graph, clip.Clip);
                playable.SetApplyFootIK(false);
                playable.SetApplyPlayableIK(false);
                playable.SetSpeed(0d);

                int inputIndex = mixer.BasePoseIndex >= 0 ? mixer.Playables.Count + 1 : mixer.Playables.Count;
                mixer.Mixer.SetInputCount(inputIndex + 1);
                mixer.Graph.Connect(playable, 0, mixer.Mixer, inputIndex);
                mixer.Mixer.SetInputWeight(inputIndex, 0f);

                mixer.Playables.Add(playable);
                mixer.Clips.Add(clip);
                mixer.Weights.Add(0f);
            }

            for (int i = 0; i < _mixers.Count; i++)
            {
                var mixer = _mixers[i];
                if (mixer.Graph.IsValid())
                    mixer.Graph.Play();
            }

            _graphReady = _mixers.Count > 0;
        }

        private void CreateIdleOnlyMixer()
        {
            Animator animator = ResolveAnimator(_defaultTarget);
            if (animator == null)
                return;

            FindOrCreateMixer(_defaultTarget != null ? _defaultTarget : animator.gameObject, animator);
        }

        private TargetMixer FindOrCreateMixer(GameObject target, Animator animator)
        {
            for (int i = 0; i < _mixers.Count; i++)
            {
                if (_mixers[i].Animator == animator)
                    return _mixers[i];
            }

            var mixer = new TargetMixer
            {
                Target = target,
                Animator = animator,
                Graph = PlayableGraph.Create($"ES Preview Animation Track - {_trackName} - {animator.name}")
            };

            try
            {
                mixer.Graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                mixer.Mixer = AnimationMixerPlayable.Create(mixer.Graph, 0);
                var output = AnimationPlayableOutput.Create(mixer.Graph, "AnimationPreview", animator);
                output.SetSourcePlayable(mixer.Mixer);
                mixer.CaptureOriginalPose();
                AddBasePoseInput(mixer);
                _mixers.Add(mixer);
                return mixer;
            }
            catch (Exception exception)
            {
                try
                {
                    mixer.Dispose();
                    mixer.RestoreOriginalPose();
                }
                catch (Exception cleanupException)
                {
                    Debug.LogException(new InvalidOperationException(
                        "高级动画预览 Mixer 构建失败，回滚也失败。", cleanupException));
                }

                Debug.LogException(new InvalidOperationException(
                    "高级动画预览 Mixer 构建失败，已回滚候选 Graph。", exception));
                return null;
            }
        }

        private static void AddBasePoseInput(TargetMixer mixer)
        {
            AnimationClip baseClip = StateMachineConfig.Instance != null ? StateMachineConfig.Instance.previewIdleClip : null;
            if (baseClip == null || mixer == null || !mixer.Graph.IsValid() || !mixer.Mixer.IsValid())
                return;

            var playable = AnimationClipPlayable.Create(mixer.Graph, baseClip);
            playable.SetApplyFootIK(false);
            playable.SetApplyPlayableIK(false);
            playable.SetSpeed(0d);
            playable.SetTime(0d);

            mixer.BasePoseIndex = mixer.Playables.Count;
            mixer.Mixer.SetInputCount(mixer.BasePoseIndex + 1);
            mixer.Graph.Connect(playable, 0, mixer.Mixer, mixer.BasePoseIndex);
            mixer.Mixer.SetInputWeight(mixer.BasePoseIndex, 1f);
            mixer.BasePosePlayable = playable;
            mixer.BasePoseClip = baseClip;
        }

        private static Animator ResolveAnimator(GameObject target)
        {
            if (target == null)
                return null;

            var animator = target.GetComponent<Animator>();
            if (animator != null)
                return animator;

            animator = target.GetComponentInChildren<Animator>(true);
            if (animator != null)
                return animator;

            return target.GetComponentInParent<Animator>();
        }

        private void SampleMixer(TargetMixer mixer, float time)
        {
            if (mixer == null || !mixer.IsValid)
                return;

            int count = mixer.Clips.Count;
            EnsureWeightBuffer(mixer.Weights, count);
            EnsureWeightBuffer(mixer.LocalTimes, count);

            int currentIndex = FindClipContaining(mixer.Clips, time);
            if (currentIndex >= 0)
                mixer.Weights[currentIndex] = 1f;

            for (int i = 0; i < count; i++)
                mixer.LocalTimes[i] = mixer.Clips[i].GetLocalTime(time);

            bool hasBasePose = mixer.BasePoseIndex >= 0 && mixer.BasePosePlayable.IsValid();
            if (count == 0)
            {
                if (hasBasePose)
                {
                    mixer.BasePosePlayable.SetTime(GetBasePoseTime(mixer, time));
                    mixer.Mixer.SetInputWeight(mixer.BasePoseIndex, 1f);
                    mixer.Graph.Evaluate(0f);
                }

                LogPreviewDiagnosticsOnce(mixer, time, 0f, hasBasePose ? 1f : 0f);
                return;
            }

            ApplyTransitionWeights(mixer.Clips, mixer.Weights, mixer.LocalTimes, time);
            float clipWeightSum = hasBasePose ? ClampWeightsForBaseBlend(mixer.Weights) : NormalizeWeights(mixer.Weights);
            float idleWeight = hasBasePose ? Mathf.Clamp01(_forcedIdleWeight) : 0f;
            float clipWeightScale = 1f - idleWeight;

            for (int i = 0; i < count; i++)
            {
                var clip = mixer.Clips[i];
                var playable = mixer.Playables[i];
                float localTime = mixer.LocalTimes[i];
                float weight = mixer.Weights[i] * clipWeightScale;

                playable.SetTime(localTime);
                playable.SetDuration(Mathf.Max(0.0001f, clip.Clip.length));
                mixer.Mixer.SetInputWeight(GetClipInputIndex(mixer, i), weight);
            }

            if (hasBasePose)
            {
                mixer.BasePosePlayable.SetTime(GetBasePoseTime(mixer, time));
                float baseWeight = idleWeight + (1f - idleWeight) * Mathf.Clamp01(1f - clipWeightSum);
                mixer.Mixer.SetInputWeight(mixer.BasePoseIndex, Mathf.Clamp01(baseWeight));
            }

            mixer.Graph.Evaluate(0f);
            LogPreviewDiagnosticsOnce(mixer, time, clipWeightSum, hasBasePose ? mixer.Mixer.GetInputWeight(mixer.BasePoseIndex) : 0f);
        }

        private void LogPreviewDiagnosticsOnce(TargetMixer mixer, float time, float clipWeightSum, float idleWeight)
        {
            if (_loggedPreviewDiagnostics)
                return;

            _loggedPreviewDiagnostics = true;

            string firstClip = mixer.Clips.Count > 0 && mixer.Clips[0] != null && mixer.Clips[0].Clip != null
                ? $"{mixer.Clips[0].Clip.name} [{mixer.Clips[0].StartTime:F2}-{mixer.Clips[0].EndTime:F2}]"
                : "<无>";
            string animatorName = mixer.Animator != null ? mixer.Animator.name : "<无>";
            string targetName = mixer.Target != null ? mixer.Target.name : "<无>";
            string idleName = mixer.BasePoseClip != null ? mixer.BasePoseClip.name : "<未配置>";

            Debug.Log($"[实体预览动画诊断] 轨道={_trackName} 时间={time:F2}s 有效Clip数={mixer.Clips.Count} 第一个Clip={firstClip} 目标={targetName} Animator={animatorName} Idle={idleName} Clip权重和={clipWeightSum:F2} Idle权重={idleWeight:F2} 强制Idle={_forcedIdleWeight:F2}");
        }

        private static int GetClipInputIndex(TargetMixer mixer, int clipIndex)
        {
            return mixer.BasePoseIndex >= 0 ? clipIndex + 1 : clipIndex;
        }

        private static double GetBasePoseTime(TargetMixer mixer, float sequenceTime)
        {
            AnimationClip clip = mixer.BasePoseClip;
            if (clip == null || clip.length <= 0.0001f)
                return 0d;

            return Mathf.Repeat(sequenceTime, clip.length);
        }

        private static void EnsureWeightBuffer(List<float> weights, int count)
        {
            weights.Clear();
            for (int i = 0; i < count; i++)
                weights.Add(0f);
        }

        private static int FindClipContaining(List<AdvancedAnimationClipEditorSampler> clips, float time)
        {
            for (int i = 0; i < clips.Count; i++)
            {
                if (clips[i].ContainsTime(time))
                    return i;
            }

            return -1;
        }

        private void ApplyTransitionWeights(List<AdvancedAnimationClipEditorSampler> clips, List<float> weights, List<float> localTimes, float time)
        {
            if (_transitionDuration <= 0.0001f)
                return;

            if (clips.Count > 1)
            {
                for (int i = 0; i < clips.Count - 1; i++)
                {
                    var from = clips[i];
                    var to = clips[i + 1];

                    if (TryGetOverlapTransition(from, to, time, out float overlapT))
                    {
                        overlapT = SmoothTransition(overlapT);
                        weights[i] = 1f - overlapT;
                        weights[i + 1] = overlapT;
                        return;
                    }

                    if (TryGetPreRollTransition(from, to, time, out float prerollT))
                    {
                        prerollT = SmoothTransition(prerollT);
                        weights[i] = 1f - prerollT;
                        weights[i + 1] = prerollT;
                        localTimes[i + 1] = 0f;
                        return;
                    }
                }
            }

            for (int i = 0; i < clips.Count; i++)
            {
                var clip = clips[i];
                if (TryGetBaseToClipTransition(clip, time, out float enterT))
                {
                    weights[i] = SmoothTransition(enterT);
                    return;
                }

                if (TryGetClipToBaseTransition(clip, time, out float exitT))
                {
                    weights[i] = 1f - SmoothTransition(exitT);
                    return;
                }
            }
        }

        private static float SmoothTransition(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        private bool TryGetOverlapTransition(AdvancedAnimationClipEditorSampler from, AdvancedAnimationClipEditorSampler to, float time, out float t)
        {
            t = 0f;
            if (to.StartTime >= from.EndTime)
                return false;

            float start = to.StartTime;
            float end = Mathf.Min(from.EndTime, to.StartTime + _transitionDuration);
            if (end <= start || time < start || time > end)
                return false;

            t = Mathf.InverseLerp(start, end, time);
            return true;
        }

        private bool TryGetPreRollTransition(AdvancedAnimationClipEditorSampler from, AdvancedAnimationClipEditorSampler to, float time, out float t)
        {
            t = 0f;
            if (to.StartTime < from.EndTime)
                return false;

            float start = Mathf.Max(from.StartTime, to.StartTime - _transitionDuration);
            float end = to.StartTime;
            if (end <= start || time < start || time > end)
                return false;

            t = Mathf.InverseLerp(start, end, time);
            return true;
        }

        private bool TryGetBaseToClipTransition(AdvancedAnimationClipEditorSampler clip, float time, out float t)
        {
            t = 0f;
            float start = clip.StartTime;
            float end = Mathf.Min(clip.EndTime, clip.StartTime + _transitionDuration);
            if (end <= start || time < start || time > end)
                return false;

            t = Mathf.InverseLerp(start, end, time);
            return true;
        }

        private bool TryGetClipToBaseTransition(AdvancedAnimationClipEditorSampler clip, float time, out float t)
        {
            t = 0f;
            float start = Mathf.Max(clip.StartTime, clip.EndTime - _transitionDuration);
            float end = clip.EndTime;
            if (end <= start || time < start || time > end)
                return false;

            t = Mathf.InverseLerp(start, end, time);
            return true;
        }

        private static float ClampWeightsForBaseBlend(List<float> weights)
        {
            float sum = 0f;
            for (int i = 0; i < weights.Count; i++)
                sum += weights[i];

            if (sum <= 0.0001f)
            {
                return 0f;
            }

            if (sum > 1f)
            {
                for (int i = 0; i < weights.Count; i++)
                    weights[i] /= sum;
            }

            return Mathf.Clamp01(sum);
        }

        private static float NormalizeWeights(List<float> weights)
        {
            float sum = 0f;
            for (int i = 0; i < weights.Count; i++)
                sum += weights[i];

            if (sum <= 0.0001f)
                return 0f;

            for (int i = 0; i < weights.Count; i++)
                weights[i] /= sum;

            return 1f;
        }

        private void DestroyGraphs()
        {
            for (int i = 0; i < _mixers.Count; i++)
            {
                TargetMixer mixer = _mixers[i];
                try
                {
                    mixer?.Dispose();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }

                try
                {
                    mixer?.RestoreOriginalPose();
                }
                catch (Exception e)
                {
                    Debug.LogException(new InvalidOperationException(
                        "高级动画预览原始姿态恢复失败。", e));
                }
            }

            _mixers.Clear();
            _graphReady = false;
        }
    }

    public sealed class AdvancedAnimationClipEditorSampler : EditorTimeSamplerBase
    {
        private readonly AnimationClip _clip;
        private readonly float _startTime;
        private readonly float _durationTime;
        private readonly float _clipStartOffset;
        private readonly float _playbackSpeed;
        private readonly bool _loopClip;
        private readonly GameObject _target;
        private bool _loggedInvalidSetup;

        public AnimationClip Clip => _clip;
        public GameObject Target => _target;
        public float StartTime => _startTime;
        public float DurationTime => _durationTime;
        public float EndTime => _startTime + _durationTime;
        public bool CanSample => _clip != null && _target != null && _durationTime > 0.0001f && _clip.length > 0.0001f;

        public AdvancedAnimationClipEditorSampler(GameObject target, AnimationClip clip, float startTime, float durationTime)
            : this(target, clip, startTime, durationTime, 0f, 1f, false)
        {
        }

        public AdvancedAnimationClipEditorSampler(GameObject target, AnimationClip clip, float startTime, float durationTime, float clipStartOffset, float playbackSpeed, bool loopClip)
        {
            _target = target;
            _clip = clip;
            _startTime = startTime;
            _durationTime = Mathf.Max(0f, durationTime);
            _clipStartOffset = Mathf.Max(0f, clipStartOffset);
            _playbackSpeed = Mathf.Max(0.01f, playbackSpeed);
            _loopClip = loopClip;
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

        public float GetLocalTime(float sequenceTime)
        {
            if (_clip == null)
                return 0f;

            float elapsed = Mathf.Clamp(sequenceTime - _startTime, 0f, _durationTime);
            float localTime = _clipStartOffset + elapsed * _playbackSpeed;
            return _loopClip ? Mathf.Repeat(localTime, _clip.length) : Mathf.Clamp(localTime, 0f, _clip.length);
        }

        public void LogInvalidSetupOnce()
        {
            if (_loggedInvalidSetup || (_clip != null && _target != null))
                return;

            _loggedInvalidSetup = true;
            Debug.LogWarning($"AdvancedAnimationClipEditorSampler cannot sample. Target={(_target != null ? _target.name : "<None>")}, Clip={(_clip != null ? _clip.name : "<None>")}");
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
            public string LastRequestClip;
            public bool HasConflictWarning;
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
            return EditorTarget is ESRuntimeTargetPack runtimeTarget ? runtimeTarget.GetGameObject() : null;
        }

        public string GetInheritedTargetDebugInfo()
        {
            if (EditorTarget == null)
                return "InheritTrackTarget TrackEditorSampler.EditorTarget=null";

            if (EditorTarget is not ESRuntimeTargetPack runtimeTarget)
                return $"InheritTrackTarget EditorTargetType={EditorTarget.GetType().Name} is not ESRuntimeTargetPack";

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

        public void UpdateClipPreviewState(string clipName, GameObject target, bool activate, bool isInside, float time)
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
                if (targetState.LastRequestFrame == _sampleFrame
                    && targetState.RequestedActive != activate
                    && !targetState.HasConflictWarning)
                {
                    Debug.LogWarning(
                        "[GameObjectTrackEditorSampler] 同一采样帧内多个 Clip 对对象“"
                        + GetTargetName(target)
                        + "”提出了相反激活请求；先请求="
                        + (targetState.LastRequestClip ?? "<未知>")
                        + "，后请求="
                        + (clipName ?? "<未知>")
                        + "，当前保持既有顺序语义。");
                    targetState.HasConflictWarning = true;
                }
                targetState.RequestedActive = activate;
                targetState.LastRequestFrame = _sampleFrame;
                targetState.LastRequestClip = clipName;
                LogDebug($"Update | Clip={clipName} | Time={time:F3} | Target={GetTargetName(target)} | Active={activate}");
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
            try
            {
                for (int i = 0; i < _targets.Count; i++)
                {
                    TargetState targetState = _targets[i];
                    if (targetState.Target == null || !targetState.HasOriginal)
                        continue;

                    try
                    {
                        LogDebug($"StopRestore | Target={GetTargetName(targetState.Target)} | Restore={targetState.OriginalActive}");
                        ApplyActiveState(ref targetState, targetState.OriginalActive, false);
                        _targets[i] = targetState;
                    }
                    catch (Exception exception)
                    {
                        Debug.LogWarning("[GameObjectTrackEditorSampler] 停止预览时恢复对象状态失败：" + exception.Message);
                    }
                }
            }
            finally
            {
                // A sampler can be reused after Stop/Play or a domain-reload-like
                // rebuild. Do not carry old active-state baselines or destroyed
                // GameObject references into the next preview session.
                _targets.Clear();
                _sampleFrame = 0;
                _lastSubmitTime = float.NaN;
                base.OnEditorPreviewStop();
            }
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

            // 首次采样时缓存原始激活状态。
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
                _trackSampler.UpdateClipPreviewState(_debugName, _target, _activate, isInside, time);
                return;
            }

            bool targetActiveState = isInside ? _activate : _originalActiveState;
            ApplyActiveState(targetActiveState);
        }

        public override void OnEditorPreviewStop()
        {
            try
            {
                if (_trackSampler == null && _target != null && _hasCachedOriginal)
                {
                    LogDebug($"StopRestore | Target={GetTargetName()} | Restore={_originalActiveState}");
                    ApplyActiveState(_originalActiveState);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[GameObjectEditorSampler] 停止预览时恢复对象状态失败：" + exception.Message);
            }
            finally
            {
                _hasCachedOriginal = false;
                _hasAppliedActiveState = false;
                _lastAppliedActiveState = false;
                _wasInside = false;
            }
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
        private float startTime; // 轨道起始时间偏移。
        private bool originalStateCaptured;
        private bool originalPlaying;
        private bool originalPaused;
        private float originalTime;
        private bool originalEmissionEnabled;

        public ParticleEditorSampler(ParticleSystem ps, float trackStartTime = 0f)
        {
            particleSystem = ps;
            startTime = trackStartTime;
        }

        public override void SampleTime(float time)
        {
            if (particleSystem == null) return;

            CaptureOriginalState();

            float localTime = Mathf.Max(0, time - startTime);
            // 模拟粒子到指定时间，restart=true 表示从初始状态开始模拟。
            particleSystem.Simulate(localTime, true, true);

            // 如果时间接近 0，停止粒子并清除。
            if (localTime < 0.01f)
                particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        public override void OnEditorPreviewStop()
        {
            if (particleSystem == null || !originalStateCaptured)
                return;

            try
            {
                try
                {
                    particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("[ParticleEditorSampler] 停止预览时清理粒子失败：" + exception.Message);
                }

                try
                {
                    particleSystem.Simulate(Mathf.Max(0f, originalTime), true, true);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("[ParticleEditorSampler] 恢复粒子时间失败：" + exception.Message);
                }

                try
                {
                    var emission = particleSystem.emission;
                    emission.enabled = originalEmissionEnabled;
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("[ParticleEditorSampler] 恢复粒子发射状态失败：" + exception.Message);
                }

                try
                {
                    if (originalPlaying)
                        particleSystem.Play(true);
                    else if (originalPaused)
                        particleSystem.Pause(true);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("[ParticleEditorSampler] 恢复粒子播放状态失败：" + exception.Message);
                }
            }
            finally
            {
                originalStateCaptured = false;
            }
        }

        private void CaptureOriginalState()
        {
            if (originalStateCaptured || particleSystem == null)
                return;

            originalStateCaptured = true;
            originalPlaying = particleSystem.isPlaying;
            originalPaused = particleSystem.isPaused;
            originalTime = particleSystem.time;
            originalEmissionEnabled = particleSystem.emission.enabled;
        }
    }


#if UNITY_EDITOR
    public class AudioEditorSampler : EditorTimeSamplerBase
{
    private readonly AudioClip _clip;
    private readonly float _startTime;
    private AudioSource _audioSource;
    private float _lastSampledTime = -1f;
    private bool _loggedPlaybackFailure;
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
        GameObject go = null;
        try
        {
            go = ESEditorPreviewUtility.CreatePreviewGameObject(
                $"AudioPreview_{_clip.name}_{GetHashCode()}");
            if (!ESEditorPreviewUtility.TryMarkPreviewObject(
                    go, nameof(AudioEditorSampler), "Track audio preview source.", out string markerStatus))
                throw new InvalidOperationException("音频预览对象未能登记公共预览所有权：" + markerStatus);
            AudioSource source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.clip = _clip;
            source.Stop(); // 确保初始状态为停止。
            _audioSource = source;
        }
        catch
        {
            if (go != null)
            {
                try { ESEditorPreviewUtility.DestroyObject(go); }
                catch (Exception cleanupException) { Debug.LogException(cleanupException); }
            }
            throw;
        }
    }

    public override void SampleTime(float time)
    {
        if (_clip == null || _audioSource == null) return;

        try
        {
            float localTime = time - _startTime;
            bool isValid = localTime >= 0 && localTime < _clip.length;

            if (!isValid)
            {
                // 不在有效区间内：停止播放并清除最后采样记录。
                if (_audioSource.isPlaying)
                    _audioSource.Stop();
                _lastSampledTime = -1f;
                return;
            }

            // 有效区间内：根据时间变化决定是否重新定位。
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
        catch (Exception exception)
        {
            _lastSampledTime = -1f;
            try { _audioSource.Stop(); }
            catch (Exception stopException) { Debug.LogException(stopException); }
            if (!_loggedPlaybackFailure)
            {
                _loggedPlaybackFailure = true;
                Debug.LogWarning("[AudioEditorSampler] 音频预览采样失败，本次采样已停止：" + exception.Message);
            }
        }
    }

    public override void OnEditorPreviewStop()
    {
        AudioSource source = _audioSource;
        _audioSource = null;
        _lastSampledTime = -1f;
        _loggedPlaybackFailure = false;
        if (source != null)
        {
            try { source.Stop(); }
            catch (Exception exception) { Debug.LogException(exception); }
            try
            {
                if (source.gameObject != null)
                    ESEditorPreviewUtility.DestroyObject(source.gameObject);
            }
            catch (Exception exception) { Debug.LogException(exception); }
        }
    }
}
#endif
    #endregion


    #region 采样映射

#if UNITY_EDITOR
    public class EditorSamplerRegistry
    {
        private readonly Dictionary<ITrackClip, IEditorTimeSampler>
            _map = new();
        private readonly List<IEditorTimeSampler> _tickBuffer = new(16);
        private bool _isTicking;
        private int _rebuildGeneration;

        public void Rebuild(ITrackSequence sequence)
        {
            _rebuildGeneration++;
            StopAll();
            _map.Clear();

            if (sequence == null || sequence.Tracks == null)
                return;

            Dictionary<ITrackClip, IEditorTimeSampler> rebuilt = new();
            try
            {
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
                            // 没有专用采样器时，使用默认调试采样器。
                            sampler = new DefaultEditorDebugSampler(sequence.Name, track.DisplayName, clip);
                        }

                        bool duplicateClip = rebuilt.TryGetValue(
                            clip,
                            out IEditorTimeSampler previousSampler)
                            && !ReferenceEquals(previousSampler, sampler);
                        // Register the candidate before diagnostics so any
                        // exception while formatting a warning is still covered
                        // by the rebuild rollback path.
                        rebuilt[clip] = sampler;
                        if (duplicateClip)
                        {
                            StopSamplers(new[] { previousSampler });
                            Debug.LogWarning(
                                "[EditorSamplerRegistry] 检测到同一 Clip 被多个轨道引用；"
                                + "已释放旧采样器并保持后一个轨道的覆盖语义。Clip="
                                + (clip.DisplayName ?? "<未命名>"));
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                StopSamplers(rebuilt.Values);
                rebuilt.Clear();
                Debug.LogWarning("[EditorSamplerRegistry] 重建采样器失败，已清理本次已创建的采样器：" + exception.Message);
                throw;
            }

            foreach (var pair in rebuilt)
                _map[pair.Key] = pair.Value;
        }
        public void Tick(float time)
        {
            if (_isTicking)
                return;

            _isTicking = true;
            int generation = _rebuildGeneration;
            _tickBuffer.Clear();
            foreach (var sampler in _map.Values)
                if (sampler != null)
                    _tickBuffer.Add(sampler);

            try
            {
                for (int i = 0; i < _tickBuffer.Count; i++)
                {
                    // A sampler may synchronously trigger Rebuild. Do not sample
                    // the old snapshot after that generation has been replaced.
                    if (generation != _rebuildGeneration)
                        break;

                    IEditorTimeSampler sampler = _tickBuffer[i];
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
            finally
            {
                _tickBuffer.Clear();
                _isTicking = false;
            }
        }

        public void StopAll()
        {
            StopSamplers(_map.Values);
        }

        private static void StopSamplers(IEnumerable<IEditorTimeSampler> samplers)
        {
            foreach (var sampler in samplers)
            {
                if (sampler is not IEditorTimeSamplerLifecycle lifecycle)
                    continue;

                try
                {
                    lifecycle.OnEditorPreviewStop();
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("[EditorSamplerRegistry] 采样器停止失败，继续清理其他采样器：" + exception.Message);
                }
            }
        }
    }
#endif

    #endregion
}
