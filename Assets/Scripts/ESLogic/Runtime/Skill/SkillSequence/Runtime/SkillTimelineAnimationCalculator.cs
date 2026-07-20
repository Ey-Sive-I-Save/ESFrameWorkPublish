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
        [NonSerialized] private SkillSequenceRuntimeCache.AnimationTimelineRuntimeData timelineData;
        [NonSerialized] private AnimationClip[] clipOverrides;
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
            var cache = SkillSequenceRuntimeCache.GetOrBuild(sequence);
            var timeline = cache != null ? cache.AnimationTimeline : null;
            if (timeline == null || !timeline.HasClips)
                return false;

            calculator = new SkillTimelineAnimationCalculator
            {
                timelineData = timeline,
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
            var clips = GetClips();
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
            runtime.EnsureClipOverrideSlots(count);

            if (hasIdle)
            {
                Playable idleOutput = Playable.Null;
                if (idleCalculator.InitializeRuntime(runtime.childRuntime, graph, ref idleOutput) && idleOutput.IsValid())
                {
                    graph.Connect(idleOutput, 0, runtime.mixer, 0);
                    runtime.mixer.SetInputWeight(0, 1f);
                    runtime.skillTimelineIdleWeightCache = 1f;
                    runtime.skillTimelineIdleWeightCacheValid = true;
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
                runtime.RegisterClipOverrideSlot(i, clip, GetClipMarker(i, clip));
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
            var clips = GetClips();
            return index >= 0 && clips != null && index < clips.Length ? clips[index] : null;
        }

        public override bool OverrideClip(AnimationCalculatorRuntime runtime, int clipIndex, AnimationClip newClip)
        {
            var clips = GetClips();
            if (clips == null || clipIndex < 0 || clipIndex >= clips.Length || newClip == null)
                return false;

            EnsureClipOverrideArray()[clipIndex] = newClip;
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

            runtime.UpdateClipOverrideSlot(clipIndex, newClip);
            return true;
        }

        public override bool OverrideClipBySource(AnimationCalculatorRuntime runtime, AnimationClip sourceClip, AnimationClip newClip)
        {
            if (runtime == null || sourceClip == null || newClip == null)
                return false;

            int slotIndex = runtime.FindClipSlotByOriginalOrCurrent(sourceClip);
            return slotIndex >= 0 && OverrideClip(runtime, slotIndex, newClip);
        }

        public override bool OverrideClipByMarker(AnimationCalculatorRuntime runtime, string marker, AnimationClip newClip)
        {
            if (runtime == null || string.IsNullOrWhiteSpace(marker) || newClip == null)
                return false;

            int slotIndex = runtime.FindClipSlotByMarker(marker);
            if (slotIndex >= 0)
                return OverrideClip(runtime, slotIndex, newClip);

            if (int.TryParse(marker, out int numericIndex))
                return OverrideClip(runtime, numericIndex, newClip);

            if (marker.StartsWith("Clip", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(marker.Substring(4), out int clipIndex))
            {
                return OverrideClip(runtime, clipIndex, newClip);
            }

            return false;
        }

        public override float GetStandardDuration(AnimationCalculatorRuntime runtime)
        {
            return timelineData != null ? timelineData.TotalDuration : 0f;
        }

        private int ResolveActiveClipIndex(float sequenceTime, int cachedIndex)
        {
            var clips = GetClips();
            var startTimes = timelineData != null ? timelineData.StartTimes : null;
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

            if (runtime.skillTimelineIdleWeightCacheValid && Mathf.Approximately(runtime.skillTimelineIdleWeightCache, weight))
                return;

            runtime.mixer.SetInputWeight(0, weight);
            runtime.skillTimelineIdleWeightCache = weight;
            runtime.skillTimelineIdleWeightCacheValid = true;
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
            var clips = GetClips();
            if (clips == null || index < 0 || index >= clips.Length)
                return 0f;

            float blend = Mathf.Max(0f, idleBlendDuration);
            if (blend <= 0.0001f)
                return 1f;

            float localTime = sequenceTime - timelineData.StartTimes[index];
            float remaining = timelineData.Durations[index] - localTime;
            float fadeIn = Mathf.Clamp01(localTime / blend);
            float fadeOut = Mathf.Clamp01(remaining / blend);
            return Mathf.Min(fadeIn, fadeOut);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsTimeInsideClip(int index, float sequenceTime)
        {
            var clips = GetClips();
            if (clips == null || index < 0 || index >= clips.Length)
                return false;

            float start = timelineData.StartTimes[index];
            return sequenceTime >= start && sequenceTime < start + timelineData.Durations[index];
        }

        private double ResolveClipLocalTime(int index, float sequenceTime)
        {
            var clips = GetClips();
            if (clips == null || index < 0 || index >= clips.Length || clips[index] == null)
                return 0d;

            float elapsed = Mathf.Clamp(sequenceTime - timelineData.StartTimes[index], 0f, timelineData.Durations[index]);
            float clipLength = clips[index].length;
            float offset = timelineData.ClipStartOffsets != null && index < timelineData.ClipStartOffsets.Length ? timelineData.ClipStartOffsets[index] : 0f;
            float speed = timelineData.PlaybackSpeeds != null && index < timelineData.PlaybackSpeeds.Length ? Mathf.Max(0.01f, timelineData.PlaybackSpeeds[index]) : 1f;
            bool loop = timelineData.LoopClips != null && index < timelineData.LoopClips.Length && timelineData.LoopClips[index];
            float localTime = offset + elapsed * speed;
            return loop ? Mathf.Repeat(localTime, clipLength) : Mathf.Clamp(localTime, 0f, clipLength);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private AnimationClip[] GetClips()
        {
            return clipOverrides ?? timelineData?.Clips;
        }

        private AnimationClip[] EnsureClipOverrideArray()
        {
            if (clipOverrides != null)
                return clipOverrides;

            var source = timelineData != null ? timelineData.Clips : null;
            if (source == null)
                return null;

            clipOverrides = new AnimationClip[source.Length];
            Array.Copy(source, clipOverrides, source.Length);
            return clipOverrides;
        }

        private string GetClipMarker(int index, AnimationClip clip)
        {
            var markers = timelineData != null ? timelineData.ClipMarkers : null;
            if (markers != null && index >= 0 && index < markers.Length && !string.IsNullOrWhiteSpace(markers[index]))
                return markers[index];

            return clip != null && !string.IsNullOrWhiteSpace(clip.name)
                ? clip.name
                : "Clip" + index;
        }
    }
}
