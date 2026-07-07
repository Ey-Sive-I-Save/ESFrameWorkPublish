using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [Serializable, TypeRegistryItem("Skill Sequence State")]
    public class EntityState_Skill : StateBase
    {
        public static new readonly ESSimplePool<EntityState_Skill> Pool = new ESSimplePool<EntityState_Skill>(
            factoryMethod: () => new EntityState_Skill(),
            initCount: 0,
            maxCount: 256,
            poolDisplayName: "EntityState_Skill Pool"
        );

        [LabelText("Skill Sequence")]
        [SerializeReference]
        public ITrackSequence skillSequence;

        [NonSerialized] private SkillSequenceRuntimeCache runtimeCache;
        [NonSerialized] private SkillRuntimeTrackState[] trackStates;
        [NonSerialized] private SkillRuntimeClipState[][] clipStates;
        [NonSerialized] private ESRuntimeTarget runtimeTarget;
        [NonSerialized] private OpSupportProvider opSupportProvider;
        [NonSerialized] private float skillTime;
        [NonSerialized] private bool runtimePrepared;

        public ESRuntimeTarget RuntimeTarget => runtimeTarget;
        public IOpSupporter OpSupporter => opSupportProvider;
        public float SkillTime => skillTime;
        public SkillSequenceRuntimeCache RuntimeCache => runtimeCache;

        public void SetSkillSequence(ITrackSequence sequence)
        {
            skillSequence = sequence;
            runtimePrepared = false;
        }

        protected override void OnStateEnterLogic()
        {
            PrepareRuntimeIfNeeded();
            ResetRuntimeStates();

            skillTime = 0f;
            if (runtimeTarget == null || runtimeTarget.IsRecycled)
                runtimeTarget = ESRuntimeTarget.Pool.GetInPool();

            runtimeTarget.SetEntity(host != null ? host.HostEntity : null);

            if (opSupportProvider == null)
                opSupportProvider = new OpSupportProvider();

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
            if (runtimeTarget != null && !runtimeTarget.IsRecycled)
                runtimeTarget.HoldRecycle(null);

            ExitAllClips();
            ExitAllTracks();

            if (runtimeTarget != null && !runtimeTarget.IsRecycled)
                runtimeTarget.CompleteRecycle(null);

            runtimeTarget = null;
            skillTime = 0f;
        }

        protected override void OnStateResetAsPoolableLogic()
        {
            skillSequence = null;
            runtimeCache = null;
            trackStates = null;
            clipStates = null;
            runtimeTarget = null;
            opSupportProvider = null;
            skillTime = 0f;
            runtimePrepared = false;
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
            EnsureClipStateCapacity(runtimeCache);
        }

        private void EnsureTrackStateCapacity(int count)
        {
            if (trackStates == null || trackStates.Length != count)
                trackStates = new SkillRuntimeTrackState[count];
        }

        private void EnsureClipStateCapacity(SkillSequenceRuntimeCache cache)
        {
            int trackCount = cache != null && cache.Tracks != null ? cache.Tracks.Length : 0;
            if (clipStates == null || clipStates.Length != trackCount)
                clipStates = new SkillRuntimeClipState[trackCount][];

            for (int i = 0; i < trackCount; i++)
            {
                var clips = cache.Tracks[i].Clips;
                int clipCount = clips != null ? clips.Length : 0;
                if (clipStates[i] == null || clipStates[i].Length != clipCount)
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
            var tracks = runtimeCache != null ? runtimeCache.Tracks : null;
            if (tracks == null)
                return;

            for (int i = 0; i < tracks.Length; i++)
            {
                var track = tracks[i];
                var trackPlayer = track.Player;
                if (trackPlayer != null)
                    trackPlayer.Tick(this, ref trackStates[i], time, deltaTime);

                TickClips(track, clipStates[i], time, deltaTime);
            }
        }

#if UNITY_EDITOR
        private void RebuildRuntimeDuringPlay()
        {
            ExitAllClips();
            ExitAllTracks();
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
                if (player != null)
                    player.OnSkillEnter(this, ref trackStates[i]);
            }
        }

        private void TickClips(SkillSequenceRuntimeCache.TrackRuntimeData track, SkillRuntimeClipState[] states, float time, float deltaTime)
        {
            var clips = track.Clips;
            if (clips == null || states == null)
                return;

            for (int i = 0; i < clips.Length; i++)
            {
                var clip = clips[i];
                var player = clip.Player;
                if (player == null)
                    continue;

                bool inside = time >= clip.StartTime && time < clip.EndTime;
                ref SkillRuntimeClipState state = ref states[i];
                if (inside)
                {
                    if (!state.IsInside)
                    {
                        state.IsInside = true;
                        state.HasEntered = true;
                        player.OnClipEnter(this, ref state);
                    }

                    player.Tick(this, ref state, time, deltaTime);
                }
                else if (state.IsInside)
                {
                    state.IsInside = false;
                    player.OnClipExit(this, ref state);
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
                    if (player != null)
                        player.OnClipExit(this, ref states[c]);
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
                if (player != null)
                    player.OnSkillExit(this, ref trackStates[i]);
            }
        }
    }
}

