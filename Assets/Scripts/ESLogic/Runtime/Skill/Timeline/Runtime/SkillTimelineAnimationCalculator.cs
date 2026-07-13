using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace ES
{
    [Serializable]
    public sealed class SkillTimelineAnimationCalculator : StateAnimationMixCalculator
    {
        [NonSerialized] private AnimationClip[] clips;
        [NonSerialized] private float[] startTimes;
        [NonSerialized] private float[] durations;
        [NonSerialized] private float totalDuration;
        [NonSerialized] private StateAnimationMixCalculator idleCalculator;
        [NonSerialized] private float idleBlendDuration;

        public override StateAnimationMixerKind CalculatorKind => StateAnimationMixerKind.SkillTimelineSequence;

        public override bool NeedUpdateWhenFadingOut => false;

        public static bool TryCreate(ITrackSequence sequence, out SkillTimelineAnimationCalculator calculator)
        {
            return TryCreate(sequence, null, out calculator);
        }

        public static bool TryCreate(ITrackSequence sequence, StateAnimationMixCalculator idleCalculator, out SkillTimelineAnimationCalculator calculator)
        {
            calculator = null;
            if (sequence == null || sequence.Tracks == null)
                return false;

            var entries = new List<TimelineClipEntry>(8);
            float maxEndTime = 0f;

            foreach (ITrackItem track in sequence.Tracks)
            {
                if (track is not SkillTrackItem_Animation animationTrack || !animationTrack.Enabled || animationTrack.clips == null)
                    continue;

                for (int i = 0; i < animationTrack.clips.Count; i++)
                {
                    SkillTrackClip_Animation clip = animationTrack.clips[i];
                    if (clip == null || !clip.Enabled || clip.AnimationClipName == null || clip.DurationTime <= 0.0001f)
                        continue;

                    entries.Add(new TimelineClipEntry(
                        clip.AnimationClipName,
                        Mathf.Max(0f, clip.StartTime),
                        Mathf.Max(0.0001f, clip.DurationTime),
                        entries.Count));
                    maxEndTime = Mathf.Max(maxEndTime, clip.StartTime + clip.DurationTime);
                }
            }

            if (entries.Count == 0)
                return false;

            entries.Sort(TimelineClipEntryComparer.Instance);

            int count = entries.Count;
            AnimationClip[] clipArray = new AnimationClip[count];
            float[] startArray = new float[count];
            float[] durationArray = new float[count];
            for (int i = 0; i < count; i++)
            {
                TimelineClipEntry entry = entries[i];
                clipArray[i] = entry.Clip;
                startArray[i] = entry.StartTime;
                durationArray[i] = entry.Duration;
            }

            calculator = new SkillTimelineAnimationCalculator
            {
                clips = clipArray,
                startTimes = startArray,
                durations = durationArray,
                totalDuration = maxEndTime,
                idleCalculator = idleCalculator,
                idleBlendDuration = 0.08f
            };
            return true;
        }

        public override void InitializeCalculator()
        {
            idleCalculator?.InitializeCalculator();
        }

        public override AnimationCalculatorRuntime CreateRuntimeData()
        {
            AnimationCalculatorRuntime runtime = base.CreateRuntimeData();
            if (idleCalculator != null)
                runtime.childRuntime = idleCalculator.CreateRuntimeData();

            return runtime;
        }

        protected override bool InitializeRuntimeInternal(AnimationCalculatorRuntime runtime, PlayableGraph graph, ref Playable output)
        {
            if (clips == null || clips.Length == 0)
                return false;

            int count = clips.Length;
            bool hasIdle = idleCalculator != null && runtime.childRuntime != null;
            int inputCount = count + (hasIdle ? 1 : 0);
            int clipInputOffset = hasIdle ? 1 : 0;

            runtime.mixer = AnimationMixerPlayable.Create(graph, inputCount);
            runtime.playables = new AnimationClipPlayable[count];
            runtime.weightCache = new float[count];
            runtime.sequencePhase = -1;

            if (hasIdle)
            {
                Playable idleOutput = Playable.Null;
                if (idleCalculator.InitializeRuntime(runtime.childRuntime, graph, ref idleOutput) && idleOutput.IsValid())
                {
                    graph.Connect(idleOutput, 0, runtime.mixer, 0);
                    runtime.mixer.SetInputWeight(0, 1f);
                }
                else
                {
                    hasIdle = false;
                }
            }

            for (int i = 0; i < count; i++)
            {
                AnimationClip clip = clips[i];
                if (clip == null)
                    continue;

                AnimationClipPlayable playable = AnimationClipPlayable.Create(graph, clip);
                playable.SetSpeed(0d);
                playable.SetTime(0d);
                runtime.playables[i] = playable;
                int mixerInputIndex = i + clipInputOffset;
                graph.Connect(playable, 0, runtime.mixer, mixerInputIndex);
                runtime.mixer.SetInputWeight(mixerInputIndex, 0f);
            }

            output = runtime.mixer;
            return output.IsValid();
        }

        public override void UpdateWeights(AnimationCalculatorRuntime runtime, in StateMachineContext context, float deltaTime)
        {
            if (runtime == null || !runtime.mixer.IsValid() || runtime.playables == null)
                return;

            float sequenceTime = runtime.ownerState != null ? runtime.ownerState.hasEnterTime : 0f;
            int activeIndex = ResolveActiveClipIndex(sequenceTime, runtime.sequencePhase);
            if (activeIndex != runtime.sequencePhase)
                ApplyActiveClipIndex(runtime, activeIndex);

            float skillWeight = ResolveSkillClipWeight(activeIndex, sequenceTime);
            ApplyIdleAndSkillWeights(runtime, activeIndex, skillWeight);

            if (idleCalculator != null && runtime.childRuntime != null)
                idleCalculator.UpdateWeights(runtime.childRuntime, context, deltaTime);

            if (activeIndex >= 0 && activeIndex < runtime.playables.Length && runtime.playables[activeIndex].IsValid())
                runtime.playables[activeIndex].SetTime(ResolveClipLocalTime(activeIndex, sequenceTime));
        }

        public override void ImmediateUpdate(AnimationCalculatorRuntime runtime, in StateMachineContext context)
        {
            UpdateWeights(runtime, context, 0f);
        }

        public override AnimationClip GetCurrentClip(AnimationCalculatorRuntime runtime)
        {
            float sequenceTime = runtime != null && runtime.ownerState != null ? runtime.ownerState.hasEnterTime : 0f;
            int index = ResolveActiveClipIndex(sequenceTime, runtime != null ? runtime.sequencePhase : -1);
            return index >= 0 && clips != null && index < clips.Length ? clips[index] : null;
        }

        public override bool OverrideClip(AnimationCalculatorRuntime runtime, int clipIndex, AnimationClip newClip)
        {
            if (clips == null || clipIndex < 0 || clipIndex >= clips.Length || newClip == null)
                return false;

            clips[clipIndex] = newClip;
            if (runtime == null || runtime.playables == null || clipIndex >= runtime.playables.Length || !runtime.mixer.IsValid())
                return true;

            PlayableGraph graph = runtime.mixer.GetGraph();
            if (!graph.IsValid())
                return true;

            if (runtime.playables[clipIndex].IsValid())
            {
                int mixerInputIndex = GetClipMixerInputIndex(runtime, clipIndex);
                graph.Disconnect(runtime.mixer, mixerInputIndex);
                runtime.playables[clipIndex].Destroy();
            }

            AnimationClipPlayable playable = AnimationClipPlayable.Create(graph, newClip);
            playable.SetSpeed(0d);
            playable.SetTime(0d);
            runtime.playables[clipIndex] = playable;
            graph.Connect(playable, 0, runtime.mixer, GetClipMixerInputIndex(runtime, clipIndex));

            float weight = runtime.sequencePhase == clipIndex ? 1f : 0f;
            runtime.mixer.SetInputWeight(GetClipMixerInputIndex(runtime, clipIndex), weight);
            if (runtime.weightCache != null && clipIndex < runtime.weightCache.Length)
                runtime.weightCache[clipIndex] = weight;

            if (runtime.sequencePhase == clipIndex && runtime.ownerState != null)
                playable.SetTime(ResolveClipLocalTime(clipIndex, runtime.ownerState.hasEnterTime));

            return true;
        }

        public override float GetStandardDuration(AnimationCalculatorRuntime runtime)
        {
            return totalDuration;
        }

        private int ResolveActiveClipIndex(float sequenceTime, int cachedIndex)
        {
            if (clips == null)
                return -1;

            if (IsTimeInsideClip(cachedIndex, sequenceTime))
            {
                int candidate = cachedIndex;
                for (int i = cachedIndex + 1; i < clips.Length; i++)
                {
                    if (startTimes[i] > sequenceTime)
                        break;

                    if (IsTimeInsideClip(i, sequenceTime))
                        candidate = i;
                }

                return candidate;
            }

            for (int i = clips.Length - 1; i >= 0; i--)
            {
                if (IsTimeInsideClip(i, sequenceTime))
                    return i;
            }

            return -1;
        }

        private void ApplyActiveClipIndex(AnimationCalculatorRuntime runtime, int activeIndex)
        {
            int previousIndex = runtime.sequencePhase;
            runtime.sequencePhase = activeIndex;

            SetClipWeight(runtime, previousIndex, 0f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetClipWeight(AnimationCalculatorRuntime runtime, int index, float weight)
        {
            if (runtime == null || !runtime.mixer.IsValid() || index < 0 || runtime.playables == null || index >= runtime.playables.Length)
                return;

            if (runtime.weightCache != null && index < runtime.weightCache.Length && Mathf.Approximately(runtime.weightCache[index], weight))
                return;

            runtime.mixer.SetInputWeight(GetClipMixerInputIndex(runtime, index), weight);
            if (runtime.weightCache != null && index < runtime.weightCache.Length)
                runtime.weightCache[index] = weight;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ApplyIdleAndSkillWeights(AnimationCalculatorRuntime runtime, int activeIndex, float skillWeight)
        {
            if (!HasIdleInput(runtime))
                skillWeight = activeIndex >= 0 ? 1f : 0f;

            skillWeight = Mathf.Clamp01(skillWeight);
            SetIdleWeight(runtime, 1f - skillWeight);
            SetClipWeight(runtime, activeIndex, skillWeight);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetIdleWeight(AnimationCalculatorRuntime runtime, float weight)
        {
            if (idleCalculator == null || runtime == null || runtime.childRuntime == null || !runtime.mixer.IsValid())
                return;

            runtime.mixer.SetInputWeight(0, weight);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetClipMixerInputIndex(AnimationCalculatorRuntime runtime, int clipIndex)
        {
            return idleCalculator != null && runtime != null && runtime.childRuntime != null ? clipIndex + 1 : clipIndex;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool HasIdleInput(AnimationCalculatorRuntime runtime)
        {
            return idleCalculator != null
                && runtime != null
                && runtime.childRuntime != null
                && runtime.mixer.IsValid()
                && runtime.mixer.GetInputCount() > 0
                && runtime.mixer.GetInput(0).IsValid();
        }

        private float ResolveSkillClipWeight(int index, float sequenceTime)
        {
            if (clips == null || index < 0 || index >= clips.Length)
                return 0f;

            float blend = Mathf.Max(0f, idleBlendDuration);
            if (blend <= 0.0001f)
                return 1f;

            float localTime = sequenceTime - startTimes[index];
            float remaining = durations[index] - localTime;
            float fadeIn = Mathf.Clamp01(localTime / blend);
            float fadeOut = Mathf.Clamp01(remaining / blend);
            return Mathf.Min(fadeIn, fadeOut);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsTimeInsideClip(int index, float sequenceTime)
        {
            if (clips == null || index < 0 || index >= clips.Length)
                return false;

            float start = startTimes[index];
            return sequenceTime >= start && sequenceTime < start + durations[index];
        }

        private double ResolveClipLocalTime(int index, float sequenceTime)
        {
            if (clips == null || index < 0 || index >= clips.Length || clips[index] == null)
                return 0d;

            float elapsed = Mathf.Clamp(sequenceTime - startTimes[index], 0f, durations[index]);
            float normalized = durations[index] > 0.0001f ? elapsed / durations[index] : 0f;
            return Mathf.Clamp01(normalized) * clips[index].length;
        }

        private readonly struct TimelineClipEntry
        {
            public readonly AnimationClip Clip;
            public readonly float StartTime;
            public readonly float Duration;
            public readonly int SourceIndex;

            public TimelineClipEntry(AnimationClip clip, float startTime, float duration, int sourceIndex)
            {
                Clip = clip;
                StartTime = startTime;
                Duration = duration;
                SourceIndex = sourceIndex;
            }
        }

        private sealed class TimelineClipEntryComparer : IComparer<TimelineClipEntry>
        {
            public static readonly TimelineClipEntryComparer Instance = new TimelineClipEntryComparer();

            public int Compare(TimelineClipEntry x, TimelineClipEntry y)
            {
                int startCompare = x.StartTime.CompareTo(y.StartTime);
                return startCompare != 0 ? startCompare : x.SourceIndex.CompareTo(y.SourceIndex);
            }
        }
    }
}
