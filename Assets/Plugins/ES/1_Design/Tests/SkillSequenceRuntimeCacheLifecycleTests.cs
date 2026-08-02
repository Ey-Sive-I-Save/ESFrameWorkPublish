using NUnit.Framework;

namespace ES.Tests
{
    public sealed class SkillSequenceRuntimeCacheLifecycleTests
    {
        [SetUp]
        public void SetUp()
        {
            SkillSequenceRuntimeCache.ClearAll();
        }

        [TearDown]
        public void TearDown()
        {
            SkillSequenceRuntimeCache.ClearAll();
        }

        [Test]
        public void Release_RetainsThreeRecentSequencesAndEvictsOldestOnFourth()
        {
            SkillProcessTrackSequence[] sequences = CreateSequences(4);
            SkillSequenceRuntimeCache[] original = BuildAll(sequences);

            for (int i = 0; i < sequences.Length; i++)
                SkillSequenceRuntimeCache.Release(sequences[i]);

            Assert.That(SkillSequenceRuntimeCache.GetOrBuild(sequences[0]), Is.Not.SameAs(original[0]));
            Assert.That(SkillSequenceRuntimeCache.GetOrBuild(sequences[1]), Is.SameAs(original[1]));
            Assert.That(SkillSequenceRuntimeCache.GetOrBuild(sequences[2]), Is.SameAs(original[2]));
            Assert.That(SkillSequenceRuntimeCache.GetOrBuild(sequences[3]), Is.SameAs(original[3]));
        }

        [Test]
        public void GetOrBuild_CancelsPendingReleaseWithoutAffectingPersistentCache()
        {
            SkillProcessTrackSequence persistentSequence = new SkillProcessTrackSequence();
            SkillSequenceRuntimeCache persistent = SkillSequenceRuntimeCache.GetOrBuild(persistentSequence);
            SkillProcessTrackSequence[] temporarySequences = CreateSequences(5);
            SkillSequenceRuntimeCache firstTemporary = SkillSequenceRuntimeCache.GetOrBuild(temporarySequences[0]);

            for (int i = 0; i < 3; i++)
            {
                SkillSequenceRuntimeCache.GetOrBuild(temporarySequences[i]);
                SkillSequenceRuntimeCache.Release(temporarySequences[i]);
            }

            Assert.That(SkillSequenceRuntimeCache.GetOrBuild(temporarySequences[0]), Is.SameAs(firstTemporary));

            for (int i = 3; i < temporarySequences.Length; i++)
            {
                SkillSequenceRuntimeCache.GetOrBuild(temporarySequences[i]);
                SkillSequenceRuntimeCache.Release(temporarySequences[i]);
            }

            Assert.That(SkillSequenceRuntimeCache.GetOrBuild(temporarySequences[0]), Is.SameAs(firstTemporary));
            Assert.That(SkillSequenceRuntimeCache.GetOrBuild(persistentSequence), Is.SameAs(persistent));
        }

        [Test]
        public void Invalidate_RemovesOneSequenceImmediately()
        {
            var sequence = new SkillProcessTrackSequence();
            SkillSequenceRuntimeCache original = SkillSequenceRuntimeCache.GetOrBuild(sequence);

            SkillSequenceRuntimeCache.Invalidate(sequence);

            Assert.That(SkillSequenceRuntimeCache.GetOrBuild(sequence), Is.Not.SameAs(original));
        }

        [Test]
        public void MarkAllDirty_RebuildsEverySequenceLazily()
        {
            SkillProcessTrackSequence[] sequences = CreateSequences(2);
            SkillSequenceRuntimeCache[] original = BuildAll(sequences);

            SkillSequenceRuntimeCache.MarkAllDirty();

            Assert.That(SkillSequenceRuntimeCache.GetOrBuild(sequences[0]), Is.Not.SameAs(original[0]));
            Assert.That(SkillSequenceRuntimeCache.GetOrBuild(sequences[1]), Is.Not.SameAs(original[1]));
        }

        [Test]
        public void ClearAll_RemovesPersistentAndRecentlyReleasedCaches()
        {
            SkillProcessTrackSequence persistentSequence = new SkillProcessTrackSequence();
            SkillProcessTrackSequence releasedSequence = new SkillProcessTrackSequence();
            SkillSequenceRuntimeCache persistent = SkillSequenceRuntimeCache.GetOrBuild(persistentSequence);
            SkillSequenceRuntimeCache released = SkillSequenceRuntimeCache.GetOrBuild(releasedSequence);
            SkillSequenceRuntimeCache.Release(releasedSequence);

            SkillSequenceRuntimeCache.ClearAll();

            Assert.That(SkillSequenceRuntimeCache.GetOrBuild(persistentSequence), Is.Not.SameAs(persistent));
            Assert.That(SkillSequenceRuntimeCache.GetOrBuild(releasedSequence), Is.Not.SameAs(released));
        }

        [Test]
        public void TemporarySkillState_ReturnToPoolQueuesItsSequenceForRelease()
        {
            var ownedSequence = new SkillProcessTrackSequence();
            SkillSequenceRuntimeCache ownedCache = SkillSequenceRuntimeCache.GetOrBuild(ownedSequence);
            EntityState_Skill state = EntityState_Skill.Pool.GetInPool();
            state.SetTemporarySkillSequence(ownedSequence);

            Assert.That(EntityState_Skill.Pool.PushToPool(state), Is.True);

            SkillProcessTrackSequence[] laterReleased = CreateSequences(3);
            for (int i = 0; i < laterReleased.Length; i++)
            {
                SkillSequenceRuntimeCache.GetOrBuild(laterReleased[i]);
                SkillSequenceRuntimeCache.Release(laterReleased[i]);
            }

            Assert.That(SkillSequenceRuntimeCache.GetOrBuild(ownedSequence), Is.Not.SameAs(ownedCache));
        }

        private static SkillProcessTrackSequence[] CreateSequences(int count)
        {
            var sequences = new SkillProcessTrackSequence[count];
            for (int i = 0; i < count; i++)
                sequences[i] = new SkillProcessTrackSequence();
            return sequences;
        }

        private static SkillSequenceRuntimeCache[] BuildAll(SkillProcessTrackSequence[] sequences)
        {
            var caches = new SkillSequenceRuntimeCache[sequences.Length];
            for (int i = 0; i < sequences.Length; i++)
                caches[i] = SkillSequenceRuntimeCache.GetOrBuild(sequences[i]);
            return caches;
        }
    }
}
