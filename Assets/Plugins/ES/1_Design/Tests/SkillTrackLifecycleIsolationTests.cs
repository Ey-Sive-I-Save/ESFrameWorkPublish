using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.TestTools;

namespace ES.Tests
{
    public sealed class SkillTrackLifecycleIsolationTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private bool previousIgnoreFailingMessages;

        [SetUp]
        public void SetUp()
        {
            previousIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;
        }

        [TearDown]
        public void TearDown()
        {
            LogAssert.ignoreFailingMessages = previousIgnoreFailingMessages;
        }

        [Test]
        public void TrackEnterFailure_CompensatesAndContinuesEnteringOtherTracks()
        {
            var failed = new ProbeTrackPlayer { ThrowOnEnter = true, ThrowOnExit = true };
            var healthy = new ProbeTrackPlayer();
            var failedTrackClip = new ProbeClipPlayer();
            SkillProcessTrackSequence sequence = CreateSequence(
                new ProbeTrack(failed, new ProbeClip(failedTrackClip, 0f, 1f)),
                new ProbeTrack(healthy));

            EntityState_Skill state = PrepareState(sequence);
            try
            {
                Invoke(state, "EnterAllTracks");

                Assert.That(failed.EnterCount, Is.EqualTo(1));
                Assert.That(failed.ExitCount, Is.EqualTo(1), "Failed Track Enter must run compensation Exit exactly once.");
                Assert.That(healthy.EnterCount, Is.EqualTo(1), "A failed Track must not block later Track Enter calls.");

                Invoke(state, "TickRuntimeCore", 0f, 0f);
                Assert.That(failed.TickCount, Is.EqualTo(0), "A failed Track must not be driven after its Enter compensation.");
                Assert.That(failedTrackClip.EnterCount, Is.EqualTo(0), "Clips under a failed Track must remain inactive.");
                Assert.That(healthy.TickCount, Is.EqualTo(1));

                Invoke(state, "ExitAllTracks");
                Assert.That(failed.ExitCount, Is.EqualTo(1), "Failed Track must not be exited twice.");
                Assert.That(healthy.ExitCount, Is.EqualTo(1));
            }
            finally
            {
                ReleaseSequence(sequence);
            }
        }

        [Test]
        public void ClipEnterFailure_CompensatesAndContinuesEnteringOtherClips()
        {
            var failed = new ProbeClipPlayer { ThrowOnEnter = true, ThrowOnExit = true };
            var healthy = new ProbeClipPlayer();
            var trackPlayer = new ProbeTrackPlayer();
            SkillProcessTrackSequence sequence = CreateSequence(
                new ProbeTrack(trackPlayer,
                    new ProbeClip(failed, 0f, 1f),
                    new ProbeClip(healthy, 0f, 1f)));

            EntityState_Skill state = PrepareState(sequence);
            try
            {
                Invoke(state, "EnterAllTracks");
                Invoke(state, "TickRuntimeCore", 0f, 0f);

                Assert.That(failed.EnterCount, Is.EqualTo(1));
                Assert.That(failed.ExitCount, Is.EqualTo(1), "Failed Clip Enter must run compensation Exit exactly once.");
                Assert.That(healthy.EnterCount, Is.EqualTo(1), "A failed Clip must not block later Clip Enter calls.");

                Invoke(state, "ExitAllClips");
                Assert.That(failed.ExitCount, Is.EqualTo(1), "Failed Clip must be removed from the active set after compensation.");
                Assert.That(healthy.ExitCount, Is.EqualTo(1));
                Invoke(state, "ExitAllTracks");
            }
            finally
            {
                ReleaseSequence(sequence);
            }
        }

        [Test]
        public void ClipExitFailure_DoesNotBlockOtherClipExits()
        {
            var orphanedUserData = new ProbePoolable();
            var failed = new ProbeClipPlayer { ThrowOnExit = true, UserDataOnEnter = orphanedUserData };
            var healthy = new ProbeClipPlayer();
            SkillProcessTrackSequence sequence = CreateSequence(
                new ProbeTrack(new ProbeTrackPlayer(),
                    new ProbeClip(failed, 0f, 1f),
                    new ProbeClip(healthy, 0f, 1f)));

            EntityState_Skill state = PrepareState(sequence);
            try
            {
                Invoke(state, "EnterAllTracks");
                Invoke(state, "TickRuntimeCore", 0f, 0f);
                Invoke(state, "ExitAllClips");

                Assert.That(failed.ExitCount, Is.EqualTo(1));
                Assert.That(healthy.ExitCount, Is.EqualTo(1), "A failed Clip Exit must not block later Clip Exit calls.");
                Assert.That(orphanedUserData.IsRecycled, Is.True, "Poolable Clip UserData must be reclaimed when its Exit throws.");
                Invoke(state, "ExitAllTracks");
            }
            finally
            {
                ReleaseSequence(sequence);
            }
        }

        [Test]
        public void ScheduledClipExitFailure_DoesNotBlockOtherClipExits()
        {
            var failed = new ProbeClipPlayer { ThrowOnExit = true };
            var healthy = new ProbeClipPlayer();
            SkillProcessTrackSequence sequence = CreateSequence(
                new ProbeTrack(new ProbeTrackPlayer(),
                    new ProbeClip(failed, 0f, 1f),
                    new ProbeClip(healthy, 0f, 1f)));

            EntityState_Skill state = PrepareState(sequence);
            try
            {
                Invoke(state, "EnterAllTracks");
                Invoke(state, "TickRuntimeCore", 0f, 0f);
                Invoke(state, "TickRuntimeCore", 1f, 1f);

                Assert.That(failed.ExitCount, Is.EqualTo(1));
                Assert.That(healthy.ExitCount, Is.EqualTo(1), "A scheduled Clip Exit failure must not block later exit events.");
                Invoke(state, "ExitAllTracks");
            }
            finally
            {
                ReleaseSequence(sequence);
            }
        }

        [Test]
        public void TrackExitFailure_DoesNotBlockOtherTrackExits()
        {
            var failed = new ProbeTrackPlayer { ThrowOnExit = true };
            var healthy = new ProbeTrackPlayer();
            SkillProcessTrackSequence sequence = CreateSequence(
                new ProbeTrack(failed),
                new ProbeTrack(healthy));

            EntityState_Skill state = PrepareState(sequence);
            try
            {
                Invoke(state, "EnterAllTracks");
                Invoke(state, "ExitAllTracks");

                Assert.That(failed.ExitCount, Is.EqualTo(1));
                Assert.That(healthy.ExitCount, Is.EqualTo(1), "A failed Track Exit must not block later Track Exit calls.");
            }
            finally
            {
                ReleaseSequence(sequence);
            }
        }

        [Test]
        public void OutputOperation_StopRunsOnlyWhenNeedsStop()
        {
            var instant = new StopProbeOperation(false);
            var scoped = new StopProbeOperation(true);

            instant._TryStopOp(null, null, null);
            scoped._TryStopOp(null, null, null);

            Assert.That(instant.StopCount, Is.Zero);
            Assert.That(scoped.StopCount, Is.EqualTo(1));
        }

        [Test]
        public void OperationClipStopFailure_StillRecyclesOwnedTargetAndRuntimeState()
        {
            var clip = new SkillTrackClip_Operation
            {
                op = new ThrowingStopOperation(),
                clipTargetSourceMode = ClipRuntimeTargetSourceMode.NewEmpty
            };
            var player = new SkillOperationClipRuntimePlayer(clip, 0);
            var clipState = new SkillRuntimeClipState();

            player.OnClipEnter(null, ref clipState);
            object runtimeState = clipState.UserData;
            Assert.That(runtimeState, Is.Not.Null);

            FieldInfo targetField = runtimeState.GetType().GetField("target", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(targetField, Is.Not.Null);
            var target = targetField.GetValue(runtimeState) as ESRuntimeTargetPack;
            Assert.That(target, Is.Not.Null);
            Assert.That(target.IsRecycled, Is.False);

            bool stopFailed = false;
            try
            {
                player.OnClipExit(null, ref clipState);
            }
            catch (InvalidOperationException)
            {
                stopFailed = true;
            }

            Assert.That(stopFailed, Is.True);
            Assert.That(clipState.UserData, Is.Null);
            Assert.That(target.IsRecycled, Is.True, "Owned TargetPack must be recycled even when StopOperation throws.");
            Assert.That(((IPoolableAuto)runtimeState).IsRecycled, Is.True, "Operation runtime state must also return to its pool.");
        }

        [Test]
        public void TrackExitFallback_DoesNotRecycleBorrowedSkillTarget()
        {
            ESRuntimeTargetPack skillTarget = ESRuntimeTargetPack.Pool.GetInPool();
            var player = new ProbeTrackPlayer { ThrowOnExit = true, UseSkillTargetAsUserData = true };
            SkillProcessTrackSequence sequence = CreateSequence(new ProbeTrack(player));
            EntityState_Skill state = PrepareState(sequence);
            SetPrivateField(state, "runtimeTarget", skillTarget);

            try
            {
                Invoke(state, "EnterAllTracks");
                Invoke(state, "ExitAllTracks");

                Assert.That(skillTarget.IsRecycled, Is.False, "Borrowed skill TargetPack must remain owned by the skill state.");
            }
            finally
            {
                if (!skillTarget.IsRecycled)
                    skillTarget.ForcePushToPool();
                ReleaseSequence(sequence);
            }
        }

        [Test]
        public void SupportCleanup_DoesNotRecyclePackFromANewerRental()
        {
            ESOpSupport support = ESOpSupport.Rent();
            ESRuntimeTargetPack firstRental = support.RentTargetPack();
            long firstVersion = firstRental.Version;
            ESRuntimeTargetPack reused = null;

            try
            {
                Assert.That(ESRuntimeTargetPack.TryReturnOwned(firstRental, firstVersion), Is.True);
                reused = ESRuntimeTargetPack.Pool.GetInPool();
                Assert.That(reused, Is.SameAs(firstRental));

                support.ClearActivationRuntime();

                Assert.That(reused.IsRecycled, Is.False,
                    "A stale Support ownership record must not recycle a newer rental of the same Pack instance.");
            }
            finally
            {
                if (reused != null)
                    ESRuntimeTargetPack.TryReturnOwned(reused, reused.Version);
                support.TryAutoPushedToPool();
            }
        }

        [Test]
        public void OperationClipExit_DoesNotRecyclePackFromANewerRental()
        {
            var clip = new SkillTrackClip_Operation
            {
                op = new StopProbeOperation(false),
                clipTargetSourceMode = ClipRuntimeTargetSourceMode.NewEmpty
            };
            var player = new SkillOperationClipRuntimePlayer(clip, 0);
            var clipState = new SkillRuntimeClipState();
            ESRuntimeTargetPack reused = null;

            player.OnClipEnter(null, ref clipState);
            object runtimeState = clipState.UserData;
            FieldInfo targetField = runtimeState.GetType().GetField("target", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            FieldInfo versionField = runtimeState.GetType().GetField("targetVersion", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var firstRental = targetField?.GetValue(runtimeState) as ESRuntimeTargetPack;
            long firstVersion = versionField != null ? (long)versionField.GetValue(runtimeState) : -1L;

            try
            {
                Assert.That(firstRental, Is.Not.Null);
                Assert.That(ESRuntimeTargetPack.TryReturnOwned(firstRental, firstVersion), Is.True);
                reused = ESRuntimeTargetPack.Pool.GetInPool();
                Assert.That(reused, Is.SameAs(firstRental));

                player.OnClipExit(null, ref clipState);

                Assert.That(reused.IsRecycled, Is.False,
                    "A stale Clip runtime state must not recycle a newer rental of the same Pack instance.");
            }
            finally
            {
                if (reused != null)
                    ESRuntimeTargetPack.TryReturnOwned(reused, reused.Version);
            }
        }

        [Test]
        public void InspectorDeclarations_AllTrackAndClipFieldsFollowStandardLayout()
        {
            var declarationTypes = new HashSet<Type>
            {
                typeof(TrackItemBase<>),
                typeof(TrackClipBase),
                typeof(SkillTrackItem<>),
            };

            AddTrackDeclarationTypes(typeof(SkillTrackItem_Audio).Assembly, declarationTypes);
            AddTrackDeclarationTypes(typeof(TrackClipBase).Assembly, declarationTypes);

            foreach (Type type in declarationTypes)
            {
                FieldInfo[] fields = type.GetFields(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                for (int i = 0; i < fields.Length; i++)
                {
                    FieldInfo field = fields[i];
                    if (!IsUnitySerializedField(field)
                        || field.IsDefined(typeof(HideInInspector), true))
                    {
                        continue;
                    }

                    string fieldPath = type.FullName + "." + field.Name;
                    LabelTextAttribute label = field.GetCustomAttribute<LabelTextAttribute>(true);
                    bool hideLabel = field.IsDefined(typeof(HideLabelAttribute), true);
                    bool hasVisibleLabel = hideLabel || label != null && ContainsChinese(label.Text);
                    Assert.That(
                        hasVisibleLabel,
                        Is.True,
                        fieldPath + " 必须声明中文 LabelText；仅嵌入式对象允许使用 HideLabel。");
                    Assert.That(
                        field.IsDefined(typeof(PropertyOrderAttribute), true),
                        Is.True,
                        fieldPath + " 必须声明 PropertyOrder，保证继承层级下的排版顺序稳定。");
                    Assert.That(
                        UsesStandardInspectorGroup(field),
                        Is.True,
                        fieldPath + " 必须使用 ESTrackInspectorFieldStandard 对应的标准分组。");
                }
            }
        }

        private static void AddTrackDeclarationTypes(Assembly assembly, HashSet<Type> output)
        {
            Type[] types = assembly.GetTypes();
            for (int i = 0; i < types.Length; i++)
            {
                Type type = types[i];
                if (!type.IsClass || type.IsNestedPrivate)
                    continue;
                if (typeof(ITrackItem).IsAssignableFrom(type)
                    || typeof(ITrackClip).IsAssignableFrom(type))
                {
                    output.Add(type);
                }
            }
        }

        private static bool IsUnitySerializedField(FieldInfo field)
        {
            if (field.IsStatic || field.IsNotSerialized)
                return false;
            return field.IsPublic
                   || field.IsDefined(typeof(SerializeField), true)
                   || field.IsDefined(typeof(SerializeReference), true);
        }

        private static bool UsesStandardInspectorGroup(FieldInfo field)
        {
            object[] attributes = field.GetCustomAttributes(typeof(PropertyGroupAttribute), true);
            for (int i = 0; i < attributes.Length; i++)
            {
                string groupId = ((PropertyGroupAttribute)attributes[i]).GroupID;
                if (IsStandardGroupRoot(groupId, ESTrackInspectorFieldStandard.TrackOverview)
                    || IsStandardGroupRoot(groupId, ESTrackInspectorFieldStandard.TrackArrangement)
                    || IsStandardGroupRoot(groupId, ESTrackInspectorFieldStandard.ClipOverview)
                    || IsStandardGroupRoot(groupId, ESTrackInspectorFieldStandard.Timeline)
                    || IsStandardGroupRoot(groupId, ESTrackInspectorFieldStandard.Content)
                    || IsStandardGroupRoot(groupId, ESTrackInspectorFieldStandard.Target)
                    || IsStandardGroupRoot(groupId, ESTrackInspectorFieldStandard.Behavior)
                    || IsStandardGroupRoot(groupId, ESTrackInspectorFieldStandard.Advanced)
                    || IsStandardGroupRoot(groupId, ESTrackInspectorFieldStandard.Preview))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsStandardGroupRoot(string groupId, string standardRoot)
        {
            return string.Equals(groupId, standardRoot, StringComparison.Ordinal)
                   || groupId != null && groupId.StartsWith(standardRoot + "/", StringComparison.Ordinal);
        }

        private static bool ContainsChinese(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;
            for (int i = 0; i < text.Length; i++)
                if (text[i] >= '\u4e00' && text[i] <= '\u9fff')
                    return true;
            return false;
        }

        [Test]
        public void StableIdentity_AssignsOnceForTrackAndClips()
        {
            var clip = new ProbeClip(new ProbeClipPlayer(), 0f, 1f);
            var track = new ProbeTrack(new ProbeTrackPlayer(), clip);

            Assert.That(track.TrackId, Is.Empty);
            Assert.That(clip.ClipId, Is.Empty);

            Assert.That(track.EnsureStableTrackIdentity(), Is.True);

            string trackId = track.TrackId;
            string clipId = clip.ClipId;
            Assert.That(trackId, Is.Not.Empty);
            Assert.That(clipId, Is.Not.Empty);
            Assert.That(track.TrackSchema, Is.EqualTo(ESTrackIdentity.CurrentTrackSchema));
            Assert.That(clip.ClipSchema, Is.EqualTo(ESTrackIdentity.CurrentClipSchema));

            Assert.That(track.EnsureStableTrackIdentity(), Is.False);
            Assert.That(track.TrackId, Is.EqualTo(trackId));
            Assert.That(clip.ClipId, Is.EqualTo(clipId));
        }

        [Test]
        public void StableIdentity_LegacyZeroSchemaUpgradesToCurrent()
        {
            var clip = new ProbeClip(new ProbeClipPlayer(), 0f, 1f)
            {
                ClipSchema = 0
            };
            var track = new ProbeTrack(new ProbeTrackPlayer(), clip)
            {
                TrackSchema = 0
            };

            Assert.That(track.EnsureStableTrackIdentity(), Is.True);
            Assert.That(track.TrackSchema, Is.EqualTo(ESTrackIdentity.CurrentTrackSchema));
            Assert.That(clip.ClipSchema, Is.EqualTo(ESTrackIdentity.CurrentClipSchema));
        }

        [Test]
        public void StableIdentity_RepairsDuplicateTrackAndClipIds()
        {
            var clipA = new ProbeClip(new ProbeClipPlayer(), 0f, 1f)
            {
                ClipId = "dup-clip"
            };
            var clipB = new ProbeClip(new ProbeClipPlayer(), 0f, 1f)
            {
                ClipId = "dup-clip"
            };
            var trackA = new ProbeTrack(new ProbeTrackPlayer(), clipA)
            {
                TrackId = "dup-track"
            };
            var trackB = new ProbeTrack(new ProbeTrackPlayer(), clipB)
            {
                TrackId = "dup-track"
            };
            SkillProcessTrackSequence sequence = CreateSequence(trackA, trackB);

            Assert.That(ESTrackIdentity.ValidateSequenceIdentity(sequence, out _, out _, out _, out _), Is.True);

            Assert.That(
                ESTrackIdentity.RepairSequenceIdentity(
                    sequence,
                    out int trackRepairs,
                    out int clipRepairs,
                    out _,
                    out _),
                Is.True);
            Assert.That(trackRepairs, Is.EqualTo(1));
            Assert.That(clipRepairs, Is.EqualTo(1));
            Assert.That(trackA.TrackId, Is.EqualTo("dup-track"));
            Assert.That(trackB.TrackId, Is.Not.EqualTo("dup-track"));
            Assert.That(clipA.ClipId, Is.EqualTo("dup-clip"));
            Assert.That(clipB.ClipId, Is.Not.EqualTo("dup-clip"));
            Assert.That(ESTrackIdentity.ValidateSequenceIdentity(sequence, out _, out _, out _, out _), Is.False);
        }

        [Test]
        public void StableIdentity_RepairFillsMissingIdsAndUpgradesSchema()
        {
            var clip = new ProbeClip(new ProbeClipPlayer(), 0f, 1f)
            {
                ClipId = null,
                ClipSchema = 0
            };
            var track = new ProbeTrack(new ProbeTrackPlayer(), clip)
            {
                TrackId = "   ",
                TrackSchema = 0
            };
            SkillProcessTrackSequence sequence = CreateSequence(track);

            Assert.That(
                ESTrackIdentity.RepairSequenceIdentity(
                    sequence,
                    out int trackRepairs,
                    out int clipRepairs,
                    out _,
                    out _),
                Is.True);
            Assert.That(trackRepairs, Is.GreaterThanOrEqualTo(1));
            Assert.That(clipRepairs, Is.GreaterThanOrEqualTo(1));
            Assert.That(ESTrackIdentity.IsValidStableId(track.TrackId), Is.True);
            Assert.That(ESTrackIdentity.IsValidStableId(clip.ClipId), Is.True);
            Assert.That(track.TrackSchema, Is.EqualTo(ESTrackIdentity.CurrentTrackSchema));
            Assert.That(clip.ClipSchema, Is.EqualTo(ESTrackIdentity.CurrentClipSchema));
            Assert.That(ESTrackIdentity.ValidateSequenceIdentity(sequence, out _, out _, out _, out _), Is.False);
        }

        [Test]
        public void StableIdentity_RepairIsIdempotentAfterValidState()
        {
            var track = new ProbeTrack(new ProbeTrackPlayer(), new ProbeClip(new ProbeClipPlayer(), 0f, 1f));
            SkillProcessTrackSequence sequence = CreateSequence(track);
            track.EnsureStableTrackIdentity();

            Assert.That(
                ESTrackIdentity.RepairSequenceIdentity(
                    sequence,
                    out _,
                    out _,
                    out _,
                    out _),
                Is.False);
        }

        [Test]
        public void StableIdentity_FutureSchemaIsNotAutoDowngraded()
        {
            var clip = new ProbeClip(new ProbeClipPlayer(), 0f, 1f)
            {
                ClipId = "valid-clip",
                ClipSchema = ESTrackIdentity.CurrentClipSchema + 1
            };
            var track = new ProbeTrack(new ProbeTrackPlayer(), clip)
            {
                TrackId = "valid-track",
                TrackSchema = ESTrackIdentity.CurrentTrackSchema + 1
            };
            SkillProcessTrackSequence sequence = CreateSequence(track);

            Assert.That(ESTrackIdentity.ValidateSequenceIdentity(sequence, out _, out _, out _, out _), Is.True);
            Assert.That(
                ESTrackIdentity.HasFutureSchema(
                    sequence,
                    out int futureTrackCount,
                    out int futureClipCount),
                Is.True);
            Assert.That(futureTrackCount, Is.EqualTo(1));
            Assert.That(futureClipCount, Is.EqualTo(1));
            Assert.That(ESTrackIdentity.RepairSequenceIdentity(sequence, out _, out _, out _, out _), Is.False);
            Assert.That(track.TrackSchema, Is.EqualTo(ESTrackIdentity.CurrentTrackSchema + 1));
            Assert.That(clip.ClipSchema, Is.EqualTo(ESTrackIdentity.CurrentClipSchema + 1));
        }

        [Test]
        public void StableIdentity_ReportsUnsupportedTrackAndClipContracts()
        {
            var sequence = new SkillProcessTrackSequence();
            sequence.tracks_.Add(new RawTrack());

            Assert.That(
                ESTrackIdentity.ValidateSequenceIdentity(
                    sequence,
                    out _,
                    out _,
                    out int unsupportedTrackCount,
                    out int unsupportedClipCount),
                Is.False);
            Assert.That(unsupportedTrackCount, Is.EqualTo(1));
            Assert.That(unsupportedClipCount, Is.EqualTo(1));
        }

        [Test]
        public void StableIdentity_RepairDoesNotClaimUnsupportedContractsChanged()
        {
            var sequence = new SkillProcessTrackSequence();
            sequence.tracks_.Add(new RawTrack());

            Assert.That(
                ESTrackIdentity.RepairSequenceIdentity(
                    sequence,
                    out _,
                    out _,
                    out int unsupportedTrackCount,
                    out int unsupportedClipCount),
                Is.False);
            Assert.That(unsupportedTrackCount, Is.EqualTo(1));
            Assert.That(unsupportedClipCount, Is.EqualTo(1));
        }

        [Test]
        public void TrackSequenceMutableOrder_MovesThroughProtocolWithoutChangingIdentity()
        {
            var first = new ProbeTrack(new ProbeTrackPlayer()) { TrackId = "track-first" };
            var second = new ProbeTrack(new ProbeTrackPlayer()) { TrackId = "track-second" };
            var third = new ProbeTrack(new ProbeTrackPlayer()) { TrackId = "track-third" };
            SkillProcessTrackSequence sequence = CreateSequence(first, second, third);
            var mutableOrder = (ITrackSequenceMutableOrder)sequence;

            Assert.That(mutableOrder.TrackItemCount, Is.EqualTo(3));
            Assert.That(mutableOrder.IndexOfTrackItem(second), Is.EqualTo(1));
            Assert.That(mutableOrder.TryMoveTrackItem(second, 2), Is.True);
            Assert.That(sequence.tracks_[0], Is.SameAs(first));
            Assert.That(sequence.tracks_[1], Is.SameAs(third));
            Assert.That(sequence.tracks_[2], Is.SameAs(second));
            Assert.That(first.TrackId, Is.EqualTo("track-first"));
            Assert.That(second.TrackId, Is.EqualTo("track-second"));
            Assert.That(third.TrackId, Is.EqualTo("track-third"));
            Assert.That(mutableOrder.TryMoveTrackItem(second, 2), Is.False);
            Assert.That(mutableOrder.TryMoveTrackItem(second, -1), Is.False);
        }

        private static EntityState_Skill PrepareState(SkillProcessTrackSequence sequence)
        {
            var state = new EntityState_Skill();
            state.SetSkillSequence(sequence);
            Invoke(state, "PrepareRuntimeIfNeeded");
            Invoke(state, "ResetRuntimeStates");
            return state;
        }

        private static SkillProcessTrackSequence CreateSequence(params ProbeTrack[] tracks)
        {
            var sequence = new SkillProcessTrackSequence();
            for (int i = 0; i < tracks.Length; i++)
                sequence.tracks_.Add(tracks[i]);
            return sequence;
        }

        private static void ReleaseSequence(SkillProcessTrackSequence sequence)
        {
            SkillSequenceRuntimeCache.Invalidate(sequence);
        }

        private static void Invoke(EntityState_Skill state, string methodName, params object[] arguments)
        {
            MethodInfo method = typeof(EntityState_Skill).GetMethod(methodName, PrivateInstance);
            Assert.That(method, Is.Not.Null, "Missing lifecycle method: " + methodName);
            method.Invoke(state, arguments);
        }

        private static void SetPrivateField(EntityState_Skill state, string fieldName, object value)
        {
            FieldInfo field = typeof(EntityState_Skill).GetField(fieldName, PrivateInstance);
            Assert.That(field, Is.Not.Null, "Missing runtime field: " + fieldName);
            field.SetValue(state, value);
        }

        private sealed class ProbeTrack : SkillTrackItem<ProbeClip>, ISkillRuntimeTrackCompiler
        {
            private readonly ProbeTrackPlayer player;

            public ProbeTrack(ProbeTrackPlayer player, params ProbeClip[] clips)
            {
                this.player = player;
                if (clips != null)
                    this.clips.AddRange(clips);
            }

            public ISkillRuntimeTrackPlayer CreateRuntimeTrackPlayer(SkillRuntimeBuildContext context)
            {
                return player;
            }
        }

        private sealed class ProbeClip : SkillTrackClip, ISkillRuntimeClipCompiler
        {
            private readonly ProbeClipPlayer player;

            public ProbeClip(ProbeClipPlayer player, float start, float duration)
            {
                this.player = player;
                startTime = start;
                durationTime = duration;
            }

            public ISkillRuntimeClipPlayer CreateRuntimeClipPlayer(SkillRuntimeBuildContext context)
            {
                return player;
            }
        }

        private sealed class RawTrack : ITrackItem, ISkillTrackItem
        {
            public bool Enabled { get; set; } = true;
            public IEnumerable<ITrackClip> Clips => new ITrackClip[] { new RawClip() };
            public UnityEngine.Color ItemBGColor => UnityEngine.Color.white;
            public string DisplayName { get; set; } = "RawTrack";
            public bool TryAddTrackClip(ITrackClip item) => false;
            public bool TryRemoveTrackClip(ITrackClip item) => false;
            public bool SortClipsByTime() => false;
            public IEnumerable<Type> SupportedClipTypes() => new Type[] { typeof(RawClip) };
            public List<IEditorTimeSampler> CreateSamplers(ITrackSequence sequence) => new List<IEditorTimeSampler>();
#if UNITY_EDITOR
            public List<IEditorTimeSampler> CreateEditorSamplers(ITrackSequence sequence, object editorTarget) => new List<IEditorTimeSampler>();
#endif
        }

        private sealed class RawClip : ITrackClip
        {
            public bool Enabled { get; set; } = true;
            public string DisplayName { get; set; } = "RawClip";
            public float StartTime { get; set; }
            public float DurationTime { get; set; } = 1f;
            public IEditorTimeSampler CreateSampler(ITrackSequence sequence, ITrackItem track) => null;
#if UNITY_EDITOR
            public IEditorTimeSampler CreateEditorSampler(ITrackSequence sequence, ITrackItem track, object editorTarget) => null;
#endif
        }

        private sealed class ProbeTrackPlayer : ISkillRuntimeTrackPlayer
        {
            public bool ThrowOnEnter;
            public bool ThrowOnExit;
            public bool UseSkillTargetAsUserData;
            public int EnterCount;
            public int ExitCount;
            public int TickCount;

            public void OnSkillEnter(EntityState_Skill state, ref SkillRuntimeTrackState trackState)
            {
                EnterCount++;
                if (UseSkillTargetAsUserData)
                    trackState.UserData = state != null ? state.SkillRuntimeTarget : null;
                if (ThrowOnEnter)
                    throw new InvalidOperationException("Probe Track Enter failure.");
            }

            public void Tick(EntityState_Skill state, ref SkillRuntimeTrackState trackState, float time, float deltaTime)
            {
                TickCount++;
            }

            public void OnSkillExit(EntityState_Skill state, ref SkillRuntimeTrackState trackState)
            {
                ExitCount++;
                if (ThrowOnExit)
                    throw new InvalidOperationException("Probe Track Exit failure.");
            }
        }

        private sealed class ProbeClipPlayer : ISkillRuntimeClipPlayer
        {
            public bool ThrowOnEnter;
            public bool ThrowOnExit;
            public IPoolableAuto UserDataOnEnter;
            public int EnterCount;
            public int ExitCount;

            public void OnClipEnter(EntityState_Skill state, ref SkillRuntimeClipState clipState)
            {
                EnterCount++;
                clipState.UserData = UserDataOnEnter;
                if (ThrowOnEnter)
                    throw new InvalidOperationException("Probe Clip Enter failure.");
            }

            public void Tick(EntityState_Skill state, ref SkillRuntimeClipState clipState, float time, float deltaTime)
            {
            }

            public void OnClipExit(EntityState_Skill state, ref SkillRuntimeClipState clipState)
            {
                ExitCount++;
                if (ThrowOnExit)
                    throw new InvalidOperationException("Probe Clip Exit failure.");
            }
        }

        private sealed class ProbePoolable : ISkillRuntimeOwnedUserData
        {
            public bool IsRecycled { get; set; }

            public void OnResetAsPoolable()
            {
            }

            public void TryAutoPushedToPool()
            {
                OnResetAsPoolable();
                IsRecycled = true;
            }
        }

        private sealed class ThrowingStopOperation : ESOutputOp
        {
            public override bool NeedsStop => true;

            protected override void StartOperation(ESRuntimeTargetPack target, ESOpSupport scopeSupport, ESOpSupport hostSupport)
            {
            }

            protected override void StopOperation(ESRuntimeTargetPack target, ESOpSupport scopeSupport, ESOpSupport hostSupport)
            {
                throw new InvalidOperationException("Probe Operation Stop failure.");
            }
        }

        private sealed class StopProbeOperation : ESOutputOp
        {
            private readonly bool needsStop;

            public StopProbeOperation(bool needsStop)
            {
                this.needsStop = needsStop;
            }

            public int StopCount { get; private set; }
            public override bool NeedsStop => needsStop;

            protected override void StartOperation(ESRuntimeTargetPack target, ESOpSupport scopeSupport, ESOpSupport hostSupport)
            {
            }

            protected override void StopOperation(ESRuntimeTargetPack target, ESOpSupport scopeSupport, ESOpSupport hostSupport)
            {
                StopCount++;
            }
        }
    }
}
