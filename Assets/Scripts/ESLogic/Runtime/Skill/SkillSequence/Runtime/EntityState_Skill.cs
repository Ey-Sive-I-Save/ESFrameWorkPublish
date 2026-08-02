using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [Serializable, TypeRegistryItem("技能/轨道序列状态")]
    public class EntityState_Skill : StateBase
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static readonly Unity.Profiling.ProfilerMarker TickRuntimeMarker =
            new Unity.Profiling.ProfilerMarker("【ES】技能轨道运行时更新");
#endif

        public static new readonly ESSimplePool<EntityState_Skill> Pool = new ESSimplePool<EntityState_Skill>(
            factoryMethod: () => new EntityState_Skill(),
            initCount: 0,
            maxCount: 256,
            poolDisplayName: "EntityState_Skill Pool"
        );

        [LabelText("技能轨道序列")]
        [SerializeReference]
        public ITrackSequence skillSequence;

        [NonSerialized] private SkillSequenceRuntimeCache runtimeCache;
        [NonSerialized] private SkillRuntimeTrackState[] trackStates;
        [NonSerialized] private SkillRuntimeClipState[][] clipStates;
        [NonSerialized] private ESRuntimeTargetPack runtimeTarget;
        [NonSerialized] private long runtimeTargetVersion;
        [NonSerialized] private ESOpSupport opSupport;
        [NonSerialized] private SkillDefinitionDataInfo skillDefinition;
        [NonSerialized] private SkillRuntimePreparedValues preparedValues;
        [NonSerialized] private float skillTime;
        [NonSerialized] private bool runtimePrepared;
        [NonSerialized] private bool releaseSequenceCacheOnReset;

        public ESRuntimeTargetPack RuntimeTarget => runtimeTarget;
        public ESRuntimeTargetPack SkillRuntimeTarget => runtimeTarget;
        public ESOpSupport OpSupport => opSupport;
        public ESOpSupport HostOpSupport => HostEntity != null ? HostEntity.OpSupport : null;
        public Entity HostEntity => host != null ? host.HostEntity : null;
        public float SkillTime => skillTime;
        public SkillSequenceRuntimeCache RuntimeCache => runtimeCache;
        public SkillDefinitionDataInfo SkillDefinition => skillDefinition;
        public SkillRuntimePreparedValues PreparedValues => preparedValues;

        public void SetSkillSequence(ITrackSequence sequence)
        {
            ReleaseOwnedSequenceCacheIfChanged(sequence);
            skillSequence = sequence;
            releaseSequenceCacheOnReset = false;
            runtimePrepared = false;
        }

        public void SetTemporarySkillSequence(ITrackSequence sequence)
        {
            ReleaseOwnedSequenceCacheIfChanged(sequence);
            skillSequence = sequence;
            releaseSequenceCacheOnReset = sequence != null;
            runtimePrepared = false;
        }

        public void SetSkillDefinition(SkillDefinitionDataInfo definition, SkillRuntimePreparedValues prepared = null)
        {
            skillDefinition = definition;
            preparedValues = prepared;
            SetSkillSequence(definition != null && definition.trackProcess != null ? definition.trackProcess.sequence : null);
        }

        public void PrewarmRuntimeForSequence(ITrackSequence sequence)
        {
            SetSkillSequence(sequence);
            PrepareRuntimeIfNeeded();
            ResetRuntimeStates();
            skillSequence = null;
            runtimeCache = null;
            skillDefinition = null;
            preparedValues = null;
            skillTime = 0f;
            runtimePrepared = false;
        }

        protected override void OnStateEnterLogic()
        {
            PrepareRuntimeIfNeeded();
            ResetRuntimeStates();

            skillTime = 0f;
            if (runtimeTarget == null || runtimeTarget.IsRecycled)
                runtimeTarget = ESRuntimeTargetPack.Pool.GetInPool();
            runtimeTargetVersion = runtimeTarget.Version;

            if (opSupport == null || opSupport.IsRecycled)
                opSupport = ESOpSupport.Rent();
            opSupport.InitializeSkillOwner(this, HostOpSupport, GetHashCode());
            opSupport.SetCurrentSkillState(this);

            FillInitialRuntimeTarget(runtimeTarget);

            EnterAllTracks();
        }

        protected override void OnStateUpdateLogic()
        {
            if (!runtimePrepared)
                PrepareRuntimeIfNeeded();

#if UNITY_EDITOR
            if (runtimeCache != null && SkillSequenceRuntimeCache.GetOrBuild(skillSequence) != runtimeCache)
            {
                RebuildRuntimeDuringPlay();
            }
#endif

            TickRuntime(skillTime, Time.deltaTime);
            skillTime += Time.deltaTime;

            if (runtimeCache != null && runtimeCache.Duration > 0f && skillTime >= runtimeCache.Duration)
                EndSelf();
        }

        protected override void OnStateExitLogic()
        {
            try
            {
                try
                {
                    ExitAllClips();
                }
                finally
                {
                    ExitAllTracks();
                }
            }
            finally
            {
                try
                {
                    ReleaseTrackRuntimeTargets();
                }
                finally
                {
                    try
                    {
                        ESRuntimeTargetPack.TryReturnOwned(runtimeTarget, runtimeTargetVersion);
                    }
                    catch (Exception exception)
                    {
                        LogLifecycleException("Skill Target Exit", -1, -1, exception);
                    }
                    finally
                    {
                        runtimeTarget = null;
                        runtimeTargetVersion = 0L;
                    }

                    if (opSupport != null)
                    {
                        try
                        {
                            opSupport.SetCurrentSkillState(null);
                        }
                        catch (Exception exception)
                        {
                            LogLifecycleException("Skill Support Unbind", -1, -1, exception);
                        }

                        try
                        {
                            opSupport.ClearActivationRuntime();
                        }
                        catch (Exception exception)
                        {
                            LogLifecycleException("Skill Support Clear", -1, -1, exception);
                        }
                    }

                    skillTime = 0f;
                }
            }
        }

        protected override void OnStateResetAsPoolableLogic()
        {
            ResetRuntimeStates();
            if (releaseSequenceCacheOnReset && skillSequence != null)
                SkillSequenceRuntimeCache.Release(skillSequence);
            skillSequence = null;
            releaseSequenceCacheOnReset = false;
            runtimeCache = null;
            runtimeTarget = null;
            runtimeTargetVersion = 0L;
            if (opSupport != null && !opSupport.IsRecycled)
                opSupport.TryAutoPushedToPool();
            opSupport = null;
            skillDefinition = null;
            preparedValues = null;
            skillTime = 0f;
            runtimePrepared = false;
        }

        private void ReleaseOwnedSequenceCacheIfChanged(ITrackSequence nextSequence)
        {
            if (releaseSequenceCacheOnReset && skillSequence != null && !ReferenceEquals(skillSequence, nextSequence))
                SkillSequenceRuntimeCache.Release(skillSequence);
        }

        public ESRuntimeTargetPack GetTrackRuntimeTarget(int trackIndex)
        {
            if (trackStates == null || trackIndex < 0 || trackIndex >= trackStates.Length)
                return null;

            object userData = trackStates[trackIndex].UserData;
            if (userData is ESRuntimeTargetPack target)
                return target;

            if (userData is SkillOperationTrackRuntimeState operationTrackState)
                return operationTrackState.target;

            return null;
        }

        public void SetTrackRuntimeTarget(int trackIndex, ESRuntimeTargetPack target)
        {
            if (trackStates == null || trackIndex < 0 || trackIndex >= trackStates.Length)
                return;

            if (trackStates[trackIndex].UserData is SkillOperationTrackRuntimeState operationTrackState)
            {
                operationTrackState.target = target;
                return;
            }

            trackStates[trackIndex].UserData = target;
        }

        public ESRuntimeTargetPack GetSkillOrTrackRuntimeTarget(int trackIndex)
        {
            ESRuntimeTargetPack trackTarget = GetTrackRuntimeTarget(trackIndex);
            return trackTarget != null ? trackTarget : runtimeTarget;
        }

        public void FillInitialRuntimeTarget(ESRuntimeTargetPack target)
        {
            if (target == null)
                return;

            Entity entityUser = HostEntity;
            if (skillDefinition == null || skillDefinition.prefillUserFromStateHost)
            {
                target.SetEntity(entityUser);
                target.SetUser(entityUser);
            }

            target.SetEntityMainTarget(null);
            target.ClearTargets();

            if (skillDefinition != null && skillDefinition.initialTargetExpression != null)
            {
                GameObject targetObject = skillDefinition.initialTargetExpression.Evaluate(target, opSupport);
                Entity targetEntity = FindEntityInSelfOrParents(targetObject);
                target.SetEntityMainTarget(targetEntity);

                if (skillDefinition.addInitialTargetToList && targetEntity != null)
                    target.AddTarget(targetEntity);
            }

            if (preparedValues != null)
            {
                target.runtimeFloat = preparedValues.multiplier;
                target.runtimeBool = preparedValues.canCast;
            }
        }

        public override void TryAutoPushedToPool()
        {
            if (!IsRecycled)
                Pool.PushToPool(this);
        }

        private void PrepareRuntimeIfNeeded()
        {
            runtimeCache = SkillSequenceRuntimeCache.GetOrBuild(skillSequence);
            runtimePrepared = true;

            int trackCount = runtimeCache != null && runtimeCache.Tracks != null ? runtimeCache.Tracks.Length : 0;
            EnsureTrackStateCapacity(trackCount);
            EnsureTrackStateRuntimeContainers(runtimeCache);
            EnsureClipStateCapacity(runtimeCache);
        }

        private void EnsureTrackStateCapacity(int count)
        {
            if (trackStates == null || trackStates.Length < count)
                trackStates = new SkillRuntimeTrackState[count];
        }

        private void EnsureTrackStateRuntimeContainers(SkillSequenceRuntimeCache cache)
        {
            if (trackStates == null)
                return;

            for (int i = 0; i < trackStates.Length; i++)
            {
                int clipCount = cache != null && cache.Tracks != null && i < cache.Tracks.Length && cache.Tracks[i].Clips != null
                    ? cache.Tracks[i].Clips.Length
                    : 0;

                trackStates[i].EnsureActiveClipCapacity(clipCount);
            }
        }

        private void EnsureClipStateCapacity(SkillSequenceRuntimeCache cache)
        {
            int trackCount = cache != null && cache.Tracks != null ? cache.Tracks.Length : 0;
            if (clipStates == null || clipStates.Length < trackCount)
                clipStates = new SkillRuntimeClipState[trackCount][];

            for (int i = 0; i < trackCount; i++)
            {
                var clips = cache.Tracks[i].Clips;
                int clipCount = clips != null ? clips.Length : 0;
                if (clipStates[i] == null || clipStates[i].Length < clipCount)
                    clipStates[i] = new SkillRuntimeClipState[clipCount];
            }
        }

        private void ResetRuntimeStates()
        {
            if (trackStates != null)
            {
                for (int i = 0; i < trackStates.Length; i++)
                    trackStates[i].Reset();
            }

            if (clipStates == null)
                return;

            for (int i = 0; i < clipStates.Length; i++)
            {
                var clips = clipStates[i];
                if (clips == null)
                    continue;

                for (int c = 0; c < clips.Length; c++)
                    clips[c].Reset();
            }
        }

        private void TickRuntime(float time, float deltaTime)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            using (TickRuntimeMarker.Auto())
            {
                TickRuntimeCore(time, deltaTime);
            }
#else
            TickRuntimeCore(time, deltaTime);
#endif
        }

        private void TickRuntimeCore(float time, float deltaTime)
        {
            var tracks = runtimeCache != null ? runtimeCache.Tracks : null;
            if (tracks == null)
                return;

            for (int i = 0; i < tracks.Length; i++)
            {
                var track = tracks[i];
                var trackPlayer = track.Player;
                if (trackPlayer != null)
                {
                    if (!trackStates[i].IsRunning)
                        continue;

                    trackPlayer.Tick(this, ref trackStates[i], time, deltaTime);
                }

                TickClips(track, ref trackStates[i], clipStates[i], time, deltaTime);
            }
        }

#if UNITY_EDITOR
        private void RebuildRuntimeDuringPlay()
        {
            ExitAllClips();
            ExitAllTracks();
            ReleaseTrackRuntimeTargets();
            PrepareRuntimeIfNeeded();
            ResetRuntimeStates();
            EnterAllTracks();
        }
#endif

        private void EnterAllTracks()
        {
            var tracks = runtimeCache != null ? runtimeCache.Tracks : null;
            if (tracks == null || trackStates == null)
                return;

            for (int i = 0; i < tracks.Length; i++)
            {
                var player = tracks[i].Player;
                if (player == null)
                    continue;

                try
                {
                    player.OnSkillEnter(this, ref trackStates[i]);
                    trackStates[i].IsRunning = true;
                }
                catch (Exception exception)
                {
                    LogLifecycleException("Track Enter", i, -1, exception);
                    try
                    {
                        player.OnSkillExit(this, ref trackStates[i]);
                    }
                    catch (Exception compensationException)
                    {
                        LogLifecycleException("Track Enter Compensation", i, -1, compensationException);
                    }
                    finally
                    {
                        ReleaseLifecycleUserData(ref trackStates[i].UserData, "Track Enter Compensation", i, -1);
                        trackStates[i].Reset();
                    }
                }
            }
        }

        private void TickClips(SkillSequenceRuntimeCache.TrackRuntimeData track, ref SkillRuntimeTrackState trackState, SkillRuntimeClipState[] states, float time, float deltaTime)
        {
            var clips = track.Clips;
            if (clips == null || states == null)
                return;

            var activeClipIndices = trackState.ActiveClipIndices;
            if (activeClipIndices == null || trackState.ActiveClipCount <= 0 && track.EnterEvents.Length == 0)
                return;

            ProcessClipEnterEvents(track, ref trackState, states, time);
            ProcessClipExitEvents(track, ref trackState, states, time);

            int activeCount = trackState.ActiveClipCount;
            for (int i = 0; i < activeCount; i++)
            {
                int clipIndex = activeClipIndices[i];
                if (clipIndex < 0 || clipIndex >= clips.Length)
                    continue;

                var clip = clips[clipIndex];
                var player = clip.Player;
                if (player == null)
                    continue;

                player.Tick(this, ref states[clipIndex], time, deltaTime);
            }
        }

        private void ProcessClipEnterEvents(SkillSequenceRuntimeCache.TrackRuntimeData track, ref SkillRuntimeTrackState trackState, SkillRuntimeClipState[] states, float time)
        {
            var enterEvents = track.EnterEvents;
            if (enterEvents == null)
                return;

            while (trackState.NextEnterEventIndex < enterEvents.Length && enterEvents[trackState.NextEnterEventIndex].Time <= time)
            {
                int clipIndex = enterEvents[trackState.NextEnterEventIndex].ClipIndex;
                trackState.NextEnterEventIndex++;

                if (clipIndex < 0 || clipIndex >= track.Clips.Length || clipIndex >= states.Length)
                    continue;

                ref SkillRuntimeClipState state = ref states[clipIndex];
                if (state.IsInside)
                    continue;

                state.IsInside = true;
                state.HasEntered = true;
                trackState.AddActiveClipIndex(clipIndex);
                var player = track.Clips[clipIndex].Player;
                if (player == null)
                    continue;

                try
                {
                    player.OnClipEnter(this, ref state);
                }
                catch (Exception exception)
                {
                    LogLifecycleException("Clip Enter", -1, clipIndex, exception);
                    try
                    {
                        player.OnClipExit(this, ref state);
                    }
                    catch (Exception compensationException)
                    {
                        LogLifecycleException("Clip Enter Compensation", -1, clipIndex, compensationException);
                    }
                    finally
                    {
                        state.IsInside = false;
                        trackState.RemoveActiveClipIndex(clipIndex);
                        ReleaseLifecycleUserData(ref state.UserData, "Clip Enter Compensation", -1, clipIndex);
                    }
                }
            }
        }

        private void ProcessClipExitEvents(SkillSequenceRuntimeCache.TrackRuntimeData track, ref SkillRuntimeTrackState trackState, SkillRuntimeClipState[] states, float time)
        {
            var exitEvents = track.ExitEvents;
            if (exitEvents == null)
                return;

            while (trackState.NextExitEventIndex < exitEvents.Length && exitEvents[trackState.NextExitEventIndex].Time <= time)
            {
                int clipIndex = exitEvents[trackState.NextExitEventIndex].ClipIndex;
                trackState.NextExitEventIndex++;

                if (clipIndex < 0 || clipIndex >= track.Clips.Length || clipIndex >= states.Length)
                    continue;

                ref SkillRuntimeClipState state = ref states[clipIndex];
                if (!state.IsInside)
                    continue;

                state.IsInside = false;
                trackState.RemoveActiveClipIndex(clipIndex);
                var player = track.Clips[clipIndex].Player;
                if (player == null)
                    continue;

                try
                {
                    player.OnClipExit(this, ref state);
                }
                catch (Exception exception)
                {
                    LogLifecycleException("Clip Exit", -1, clipIndex, exception);
                }
                finally
                {
                    ReleaseLifecycleUserData(ref state.UserData, "Clip Exit", -1, clipIndex);
                }
            }
        }

        private void ExitAllClips()
        {
            var tracks = runtimeCache != null ? runtimeCache.Tracks : null;
            if (tracks == null || clipStates == null)
                return;

            for (int i = 0; i < tracks.Length; i++)
            {
                var clips = tracks[i].Clips;
                var states = clipStates[i];
                if (clips == null || states == null)
                    continue;

                for (int c = 0; c < clips.Length; c++)
                {
                    if (!states[c].IsInside)
                        continue;

                    states[c].IsInside = false;
                    var player = clips[c].Player;
                    if (player == null)
                        continue;

                    try
                    {
                        player.OnClipExit(this, ref states[c]);
                    }
                    catch (Exception exception)
                    {
                        LogLifecycleException("Clip Exit", i, c, exception);
                    }
                    finally
                    {
                        ReleaseLifecycleUserData(ref states[c].UserData, "Clip Exit", i, c);
                    }
                }
            }
        }

        private void ExitAllTracks()
        {
            var tracks = runtimeCache != null ? runtimeCache.Tracks : null;
            if (tracks == null || trackStates == null)
                return;

            for (int i = 0; i < tracks.Length; i++)
            {
                var player = tracks[i].Player;
                if (player == null || !trackStates[i].IsRunning)
                    continue;

                try
                {
                    player.OnSkillExit(this, ref trackStates[i]);
                }
                catch (Exception exception)
                {
                    LogLifecycleException("Track Exit", i, -1, exception);
                }
                finally
                {
                    ReleaseLifecycleUserData(ref trackStates[i].UserData, "Track Exit", i, -1);
                    trackStates[i].Reset();
                }
            }
        }

        private void ReleaseLifecycleUserData(ref object userData, string phase, int trackIndex, int clipIndex)
        {
            object value = userData;
            userData = null;
            if (value == null)
                return;

            try
            {
                if (value is ISkillRuntimeOwnedUserData poolable && !poolable.IsRecycled)
                    poolable.TryAutoPushedToPool();
            }
            catch (Exception exception)
            {
                LogLifecycleException(phase + " UserData Cleanup", trackIndex, clipIndex, exception);
            }
        }

        private static void LogLifecycleException(string phase, int trackIndex, int clipIndex, Exception exception)
        {
            StateMachineDebugSettings.Instance.LogError(
                $"[SkillTrack] {phase} failed. Track={trackIndex} Clip={clipIndex}\n{exception}");
        }

        private void ReleaseTrackRuntimeTargets()
        {
            if (trackStates == null)
                return;

            for (int i = 0; i < trackStates.Length; i++)
            {
                try
                {
                    if (trackStates[i].UserData is SkillOperationTrackRuntimeState operationState)
                    {
                        if (operationState.createdTarget)
                            ESRuntimeTargetPack.TryReturnOwned(operationState.target, operationState.targetVersion);
                        operationState.TryAutoPushedToPool();
                    }
                }
                catch (Exception exception)
                {
                    LogLifecycleException("Track Target Exit", i, -1, exception);
                }
                finally
                {
                    trackStates[i].UserData = null;
                }
            }
        }

        private static Entity FindEntityInSelfOrParents(GameObject gameObject)
        {
            Transform current = gameObject != null ? gameObject.transform : null;
            while (current != null)
            {
                Entity entity = current.GetComponent<Entity>();
                if (entity != null)
                    return entity;

                current = current.parent;
            }

            return null;
        }
    }
}
