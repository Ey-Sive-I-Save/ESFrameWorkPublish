using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace ES.Tests
{
    public sealed class ESEnumStringMirrorMapTests
    {
        private enum DenseKey
        {
            None = 0,
            Hand = 1,
            Head = 2,
            Weapon = 8
        }

        private enum SparseKey : long
        {
            Negative = -4,
            Huge = 100000
        }

        [Test]
        public void PairAliases_ResolveTheSameEntryThroughDenseEnumAndStringMirrors()
        {
            ESEnumStringMirrorMap<DenseKey, string> map = new ESEnumStringMirrorMap<DenseKey, string>();

            Assert.That(map.TryAdd(DenseKey.Hand, "hand.primary", "right-hand", out var conflict), Is.True, conflict.ToString());
            Assert.That(map.TryGetValue(DenseKey.Hand, out string byEnum), Is.True);
            Assert.That(map.TryGetValue("hand.primary", out string byString), Is.True);
            Assert.That(map.TryGetValue(DenseKey.Hand, "hand.primary", out string byPair), Is.True);
            Assert.That(byEnum, Is.EqualTo("right-hand"));
            Assert.That(byString, Is.EqualTo(byEnum));
            Assert.That(byPair, Is.EqualTo(byEnum));
            Assert.That(map.ActiveEnumMirror, Is.EqualTo(ESEnumStringMirrorMap<DenseKey, string>.EnumMirrorKind.DenseArray));
        }

        [Test]
        public void TrySet_AddsMissingAliasAndUpdatesExistingEntryAtomically()
        {
            ESEnumStringMirrorMap<DenseKey, string> map = new ESEnumStringMirrorMap<DenseKey, string>();
            Assert.That(map.TryAdd(DenseKey.Head, "old", out var addConflict), Is.True, addConflict.ToString());
            int generation = map.Generation;

            Assert.That(map.TrySet(DenseKey.Head, "head.default", "new", out var setConflict), Is.True, setConflict.ToString());

            Assert.That(map.Count, Is.EqualTo(1));
            Assert.That(map.Generation, Is.GreaterThan(generation));
            Assert.That(map.TryGetValue(DenseKey.Head, "head.default", out string value), Is.True);
            Assert.That(value, Is.EqualTo("new"));
        }

        [Test]
        public void RuntimeMutationMatrix_BuildsAdjustsReplacesMovesAndRemoves()
        {
            ESEnumStringMirrorMap<DenseKey, string> map = new ESEnumStringMirrorMap<DenseKey, string>();
            map.EnsureCapacity(8);

            Assert.That(
                map.TryAddEntries(new[]
                {
                    new ESEnumStringMirrorMap<DenseKey, string>.Entry(DenseKey.Hand, "slot.hand", "hand"),
                    new ESEnumStringMirrorMap<DenseKey, string>.Entry("slot.head", "head")
                }, out var buildConflict),
                Is.True,
                buildConflict.ToString());

            Assert.That(map.TrySetValue(DenseKey.Hand, "hand.updated", out var valueConflict), Is.True, valueConflict.ToString());
            Assert.That(map.TrySetEnumAlias("slot.head", DenseKey.Head, out var enumAliasConflict), Is.True, enumAliasConflict.ToString());
            Assert.That(map.TrySetStringAlias(DenseKey.Hand, "slot.primary-hand", out var stringAliasConflict), Is.True, stringAliasConflict.ToString());
            Assert.That(map.TryReplaceEnumKey(DenseKey.Head, DenseKey.Weapon, out var enumReplaceConflict), Is.True, enumReplaceConflict.ToString());
            Assert.That(map.TryReplaceStringKey("slot.head", "slot.weapon", out var stringReplaceConflict), Is.True, stringReplaceConflict.ToString());

            Assert.That(
                map.TryInsertEntry(
                    1,
                    new ESEnumStringMirrorMap<DenseKey, string>.Entry(DenseKey.Head, "slot.head", "inserted"),
                    out var insertConflict),
                Is.True,
                insertConflict.ToString());
            Assert.That(map.TryMoveEntry(2, 0, out var moveConflict), Is.True, moveConflict.ToString());

            Assert.That(map.TryGetEntryAt(0, out var first), Is.True);
            Assert.That(first.enumKey, Is.EqualTo(DenseKey.Weapon));
            Assert.That(first.stringKey, Is.EqualTo("slot.weapon"));
            Assert.That(map.TryGetValue(DenseKey.Hand, "slot.primary-hand", out string hand), Is.True);
            Assert.That(hand, Is.EqualTo("hand.updated"));

            Assert.That(
                map.TryReplaceEntry(
                    DenseKey.Head,
                    new ESEnumStringMirrorMap<DenseKey, string>.Entry(DenseKey.Head, "slot.helmet", "helmet"),
                    out var entryReplaceConflict),
                Is.True,
                entryReplaceConflict.ToString());
            Assert.That(map.TryRemoveEnumAlias(DenseKey.Hand, out var removeEnumConflict), Is.True, removeEnumConflict.ToString());
            Assert.That(map.TryRemoveStringAlias("slot.weapon", out var removeStringConflict), Is.True, removeStringConflict.ToString());
            Assert.That(map.TryRemoveEntry("slot.helmet", out string removed, out var removeConflict), Is.True, removeConflict.ToString());

            Assert.That(removed, Is.EqualTo("helmet"));
            Assert.That(map.Count, Is.EqualTo(2));
            Assert.That(map.TryGetValue("slot.primary-hand", out hand), Is.True);
            Assert.That(map.TryGetValue(DenseKey.Hand, out _), Is.False);
            Assert.That(map.TryGetValue(DenseKey.Weapon, out string weapon), Is.True);
            Assert.That(weapon, Is.EqualTo("head"));
            Assert.That(map.TryGetValue("slot.weapon", out _), Is.False);
        }

        [Test]
        public void SingleEntryMutations_KeepAuthorityAndMirrorInstances()
        {
            ESEnumStringMirrorMap<DenseKey, string> map = new ESEnumStringMirrorMap<DenseKey, string>(4);
            Assert.That(map.TryAdd(DenseKey.Hand, "slot.hand", "hand", out _), Is.True);
            Assert.That(map.TryAdd(DenseKey.Weapon, "slot.weapon", "weapon", out _), Is.True);
            Assert.That(map.TryRemoveEntry(DenseKey.Weapon, out _, out _), Is.True);

            object entries = GetPrivateField(map, "entries");
            object denseMirror = GetPrivateField(map, "denseEnumEntries");
            object stringMirror = GetPrivateField(map, "stringEntries");

            Assert.That(map.TrySetValue(DenseKey.Hand, "hand.updated", out var valueConflict), Is.True, valueConflict.ToString());
            Assert.That(GetPrivateField(map, "entries"), Is.SameAs(entries));
            Assert.That(GetPrivateField(map, "denseEnumEntries"), Is.SameAs(denseMirror));
            Assert.That(GetPrivateField(map, "stringEntries"), Is.SameAs(stringMirror));

            Assert.That(map.TryReplaceEnumKey(DenseKey.Hand, DenseKey.Head, out var enumConflict), Is.True, enumConflict.ToString());
            Assert.That(map.TryReplaceStringKey("slot.hand", "slot.primary-hand", out var stringConflict), Is.True, stringConflict.ToString());
            Assert.That(GetPrivateField(map, "entries"), Is.SameAs(entries));
            Assert.That(GetPrivateField(map, "denseEnumEntries"), Is.SameAs(denseMirror));
            Assert.That(GetPrivateField(map, "stringEntries"), Is.SameAs(stringMirror));
            Assert.That(map.TryGetValue(DenseKey.Head, "slot.primary-hand", out string value), Is.True);
            Assert.That(value, Is.EqualTo("hand.updated"));
        }

        [Test]
        public void SequentialAdds_GrowAuthorityAndDenseMirrorGeometrically()
        {
            ESEnumStringMirrorMap<DenseKey, string> map = new ESEnumStringMirrorMap<DenseKey, string>();
            Assert.That(map.TryAdd(DenseKey.None, "none", out _), Is.True);
            List<ESEnumStringMirrorMap<DenseKey, string>.Entry> entries =
                (List<ESEnumStringMirrorMap<DenseKey, string>.Entry>)GetPrivateField(map, "entries");
            int initialAuthorityCapacity = entries.Capacity;
            object initialDenseMirror = GetPrivateField(map, "denseEnumEntries");

            Assert.That(map.TryAdd("one", "one", out _), Is.True);
            Assert.That(map.TryAdd("two", "two", out _), Is.True);
            Assert.That(map.TryAdd("three", "three", out _), Is.True);
            Assert.That(map.TryAdd("four", "four", out _), Is.True);
            Assert.That(map.TryAdd(DenseKey.Weapon, "weapon", out _), Is.True);

            Assert.That(entries.Capacity, Is.GreaterThanOrEqualTo(initialAuthorityCapacity * 2));
            Assert.That(GetPrivateField(map, "denseEnumEntries"), Is.Not.SameAs(initialDenseMirror));
            Assert.That(map.TryGetValue(DenseKey.None, out _), Is.True);
            Assert.That(map.TryGetValue(DenseKey.Weapon, out _), Is.True);
        }

        [Test]
        public void IdenticalSetAndEntryReplacement_DoNotAdvanceGeneration()
        {
            ESEnumStringMirrorMap<DenseKey, string> map = new ESEnumStringMirrorMap<DenseKey, string>();
            Assert.That(map.TryAdd(DenseKey.Hand, "slot.hand", "hand", out _), Is.True);
            int generation = map.Generation;

            Assert.That(map.TrySet(DenseKey.Hand, "slot.hand", "hand", out var setConflict), Is.True, setConflict.ToString());
            Assert.That(
                map.TryReplaceEntryAt(
                    0,
                    new ESEnumStringMirrorMap<DenseKey, string>.Entry(DenseKey.Hand, "slot.hand", "hand"),
                    out var replaceConflict),
                Is.True,
                replaceConflict.ToString());

            Assert.That(map.Generation, Is.EqualTo(generation));
        }

        [Test]
        public void EquivalentAbsentAliases_DoNotAdvanceGeneration()
        {
            ESEnumStringMirrorMap<DenseKey, string> map = new ESEnumStringMirrorMap<DenseKey, string>();
            Assert.That(map.TryAdd(DenseKey.Hand, "hand", out _), Is.True);
            int generation = map.Generation;

            var replacement = new ESEnumStringMirrorMap<DenseKey, string>.Entry
            {
                hasEnumKey = true,
                enumKey = DenseKey.Hand,
                stringKey = string.Empty,
                value = "hand"
            };
            Assert.That(map.TryReplaceEntryAt(0, replacement, out var conflict), Is.True, conflict.ToString());
            Assert.That(map.Generation, Is.EqualTo(generation));
        }

        [Test]
        public void IncrementalInsertMoveAndRemove_KeepEveryMirrorConsistent()
        {
            ESEnumStringMirrorMap<DenseKey, string> map = new ESEnumStringMirrorMap<DenseKey, string>();
            Assert.That(map.TryAdd(DenseKey.Hand, "slot.hand", "hand", out _), Is.True);
            Assert.That(map.TryAdd(DenseKey.Weapon, "slot.weapon", "weapon", out _), Is.True);
            object authority = GetPrivateField(map, "entries");
            object stringMirror = GetPrivateField(map, "stringEntries");

            Assert.That(
                map.TryInsertEntry(
                    1,
                    new ESEnumStringMirrorMap<DenseKey, string>.Entry(DenseKey.Head, "slot.head", "head"),
                    out var insertConflict),
                Is.True,
                insertConflict.ToString());
            Assert.That(map.TryMoveEntry(2, 0, out var moveConflict), Is.True, moveConflict.ToString());
            Assert.That(map.TryRemoveEntry(DenseKey.Head, out string removed, out var removeConflict), Is.True, removeConflict.ToString());

            Assert.That(GetPrivateField(map, "entries"), Is.SameAs(authority));
            Assert.That(GetPrivateField(map, "stringEntries"), Is.SameAs(stringMirror));
            Assert.That(removed, Is.EqualTo("head"));
            Assert.That(map.TryGetValue(DenseKey.Weapon, "slot.weapon", out string weapon), Is.True);
            Assert.That(map.TryGetValue(DenseKey.Hand, "slot.hand", out string hand), Is.True);
            Assert.That(weapon, Is.EqualTo("weapon"));
            Assert.That(hand, Is.EqualTo("hand"));
            Assert.That(map.TryGetValue(DenseKey.Head, out _), Is.False);
            Assert.That(map.TryGetValue("slot.head", out _), Is.False);
            Assert.That(map.TryGetEntryAt(0, out var first), Is.True);
            Assert.That(map.TryGetEntryAt(1, out var second), Is.True);
            Assert.That(first.enumKey, Is.EqualTo(DenseKey.Weapon));
            Assert.That(second.enumKey, Is.EqualTo(DenseKey.Hand));
        }

        [Test]
        public void RuntimeMutations_RejectConflictsWithoutPartialChanges()
        {
            ESEnumStringMirrorMap<DenseKey, string> map = new ESEnumStringMirrorMap<DenseKey, string>();
            Assert.That(map.TryAdd(DenseKey.Hand, "slot.hand", "hand", out _), Is.True);
            Assert.That(map.TryAdd(DenseKey.Head, "slot.head", "head", out _), Is.True);
            int generation = map.Generation;

            Assert.That(map.TryReplaceEnumKey(DenseKey.Hand, DenseKey.Head, out var enumConflict), Is.False);
            Assert.That(enumConflict.Kind, Is.EqualTo(ESEnumStringMirrorMap<DenseKey, string>.ConflictKind.DuplicateEnumKey));
            Assert.That(map.TryReplaceStringKey("slot.hand", "slot.head", out var stringConflict), Is.False);
            Assert.That(stringConflict.Kind, Is.EqualTo(ESEnumStringMirrorMap<DenseKey, string>.ConflictKind.DuplicateStringKey));
            Assert.That(map.TrySetEnumAlias("slot.hand", DenseKey.Head, out _), Is.False);
            Assert.That(map.TrySetStringAlias(DenseKey.Hand, "slot.head", out _), Is.False);
            Assert.That(
                map.TryReplaceEntryAt(
                    0,
                    new ESEnumStringMirrorMap<DenseKey, string>.Entry(DenseKey.Head, "slot.replacement", "invalid"),
                    out _),
                Is.False);
            Assert.That(
                map.TryAddEntries(
                    new[] { new ESEnumStringMirrorMap<DenseKey, string>.Entry(DenseKey.Weapon, "slot.head", "invalid") },
                    out _),
                Is.False);
            Assert.That(map.TryMoveEntry(-1, 0, out var indexConflict), Is.False);
            Assert.That(indexConflict.Kind, Is.EqualTo(ESEnumStringMirrorMap<DenseKey, string>.ConflictKind.InvalidIndex));

            Assert.That(map.Generation, Is.EqualTo(generation));
            Assert.That(map.Count, Is.EqualTo(2));
            Assert.That(map.TryGetValue(DenseKey.Hand, "slot.hand", out string hand), Is.True);
            Assert.That(map.TryGetValue(DenseKey.Head, "slot.head", out string head), Is.True);
            Assert.That(hand, Is.EqualTo("hand"));
            Assert.That(head, Is.EqualTo("head"));
            Assert.That(map.LastConflict.HasConflict, Is.False);
        }

        [Test]
        public void AliasOnlyRemoval_RejectsRemovingTheLastIdentity()
        {
            ESEnumStringMirrorMap<DenseKey, string> enumOnly = new ESEnumStringMirrorMap<DenseKey, string>();
            ESEnumStringMirrorMap<DenseKey, string> stringOnly = new ESEnumStringMirrorMap<DenseKey, string>();
            Assert.That(enumOnly.TryAdd(DenseKey.Hand, "hand", out _), Is.True);
            Assert.That(stringOnly.TryAdd("slot.hand", "hand", out _), Is.True);

            Assert.That(enumOnly.TryRemoveEnumAlias(DenseKey.Hand, out var enumConflict), Is.False);
            Assert.That(stringOnly.TryRemoveStringAlias("slot.hand", out var stringConflict), Is.False);

            Assert.That(enumConflict.Kind, Is.EqualTo(ESEnumStringMirrorMap<DenseKey, string>.ConflictKind.MissingKey));
            Assert.That(stringConflict.Kind, Is.EqualTo(ESEnumStringMirrorMap<DenseKey, string>.ConflictKind.MissingKey));
            Assert.That(enumOnly.TryGetValue(DenseKey.Hand, out _), Is.True);
            Assert.That(stringOnly.TryGetValue("slot.hand", out _), Is.True);
        }

        [Test]
        public void NoOpMove_DoesNotAdvanceGeneration()
        {
            ESEnumStringMirrorMap<DenseKey, string> map = new ESEnumStringMirrorMap<DenseKey, string>();
            Assert.That(map.TryAdd(DenseKey.Hand, "slot.hand", "hand", out _), Is.True);
            int generation = map.Generation;

            Assert.That(map.TryMoveEntry(0, 0, out var conflict), Is.True, conflict.ToString());

            Assert.That(map.Generation, Is.EqualTo(generation));
            Assert.That(map.TryGetValue(DenseKey.Hand, "slot.hand", out string value), Is.True);
            Assert.That(value, Is.EqualTo("hand"));
        }

        [Test]
        public void NoOpKeyChanges_DoNotAdvanceGeneration()
        {
            ESEnumStringMirrorMap<DenseKey, string> map = new ESEnumStringMirrorMap<DenseKey, string>();
            Assert.That(map.TryAdd(DenseKey.Hand, "slot.hand", "hand", out _), Is.True);
            int generation = map.Generation;

            Assert.That(map.TrySetEnumAlias("slot.hand", DenseKey.Hand, out var enumAliasConflict), Is.True, enumAliasConflict.ToString());
            Assert.That(map.TrySetStringAlias(DenseKey.Hand, "slot.hand", out var stringAliasConflict), Is.True, stringAliasConflict.ToString());
            Assert.That(map.TryReplaceEnumKey(DenseKey.Hand, DenseKey.Hand, out var enumReplaceConflict), Is.True, enumReplaceConflict.ToString());
            Assert.That(map.TryReplaceStringKey("slot.hand", "slot.hand", out var stringReplaceConflict), Is.True, stringReplaceConflict.ToString());

            Assert.That(map.Generation, Is.EqualTo(generation));
            Assert.That(map.TryGetValue(DenseKey.Hand, "slot.hand", out string value), Is.True);
            Assert.That(value, Is.EqualTo("hand"));
        }

        [Test]
        public void Clear_RemovesAllEntriesAndBothMirrors()
        {
            ESEnumStringMirrorMap<DenseKey, string> map = new ESEnumStringMirrorMap<DenseKey, string>();
            Assert.That(map.TryAdd(DenseKey.Hand, "slot.hand", "hand", out _), Is.True);
            int generation = map.Generation;

            map.Clear();

            Assert.That(map.Count, Is.Zero);
            Assert.That(map.Generation, Is.GreaterThan(generation));
            Assert.That(map.ActiveEnumMirror, Is.EqualTo(ESEnumStringMirrorMap<DenseKey, string>.EnumMirrorKind.None));
            Assert.That(map.TryGetValue(DenseKey.Hand, out _), Is.False);
            Assert.That(map.TryGetValue("slot.hand", out _), Is.False);
        }

        [Test]
        public void InvalidValueReplacement_KeepsThePreviousEntry()
        {
            ESEnumStringMirrorMap<DenseKey, string> map = new ESEnumStringMirrorMap<DenseKey, string>();
            Assert.That(map.TryAdd(DenseKey.Hand, "slot.hand", "hand", out _), Is.True);
            int generation = map.Generation;

            Assert.That(map.TrySetValue(DenseKey.Hand, null, out var conflict), Is.False);

            Assert.That(conflict.Kind, Is.EqualTo(ESEnumStringMirrorMap<DenseKey, string>.ConflictKind.NullValue));
            Assert.That(map.Generation, Is.EqualTo(generation));
            Assert.That(map.TryGetValue(DenseKey.Hand, "slot.hand", out string value), Is.True);
            Assert.That(value, Is.EqualTo("hand"));
        }

        [Test]
        public void FullReplacement_RemovesOldKeysAndBuildsOnlyTheNewAuthority()
        {
            ESEnumStringMirrorMap<DenseKey, string> map = new ESEnumStringMirrorMap<DenseKey, string>();
            Assert.That(map.TryAdd(DenseKey.Hand, "slot.hand", "hand", out _), Is.True);

            Assert.That(
                map.TryReplaceEntries(
                    new[]
                    {
                        new ESEnumStringMirrorMap<DenseKey, string>.Entry(DenseKey.Weapon, "slot.weapon", "weapon")
                    },
                    out var conflict),
                Is.True,
                conflict.ToString());

            Assert.That(map.Count, Is.EqualTo(1));
            Assert.That(map.TryGetValue(DenseKey.Hand, out _), Is.False);
            Assert.That(map.TryGetValue("slot.hand", out _), Is.False);
            Assert.That(map.TryGetValue(DenseKey.Weapon, "slot.weapon", out string value), Is.True);
            Assert.That(value, Is.EqualTo("weapon"));
        }

        [Test]
        public void MutationOverloads_ResolveTheSameEntriesAndApplyTheRequestedScope()
        {
            ESEnumStringMirrorMap<DenseKey, string> map = new ESEnumStringMirrorMap<DenseKey, string>();
            Assert.That(map.TryAdd(DenseKey.Hand, "slot.hand", "hand", out _), Is.True);
            Assert.That(map.TryAdd(DenseKey.Head, "slot.head", "head", out _), Is.True);
            Assert.That(map.TryAdd(DenseKey.Weapon, "slot.weapon", "weapon", out _), Is.True);

            Assert.That(map.TryGetEntry(DenseKey.Hand, out var byEnum), Is.True);
            Assert.That(map.TryGetEntry("slot.hand", out var byString), Is.True);
            Assert.That(map.TryGetEntry(DenseKey.Hand, "slot.hand", out var byPair), Is.True);
            Assert.That(byEnum.value, Is.EqualTo(byString.value));
            Assert.That(byPair.value, Is.EqualTo(byEnum.value));

            Assert.That(map.TrySetValue("slot.hand", "hand.string", out var stringSetConflict), Is.True, stringSetConflict.ToString());
            Assert.That(map.TrySetValue(DenseKey.Hand, "slot.hand", "hand.pair", out var pairSetConflict), Is.True, pairSetConflict.ToString());
            Assert.That(map.TryGetValue(DenseKey.Hand, out string hand), Is.True);
            Assert.That(hand, Is.EqualTo("hand.pair"));

            Assert.That(
                map.TryReplaceEntry(
                    "slot.head",
                    new ESEnumStringMirrorMap<DenseKey, string>.Entry(DenseKey.Head, "slot.helmet", "helmet"),
                    out var stringReplaceConflict),
                Is.True,
                stringReplaceConflict.ToString());
            Assert.That(
                map.TryReplaceEntryAt(
                    2,
                    new ESEnumStringMirrorMap<DenseKey, string>.Entry(DenseKey.Weapon, "slot.main-weapon", "main-weapon"),
                    out var indexReplaceConflict),
                Is.True,
                indexReplaceConflict.ToString());

            Assert.That(map.TryRemoveEntry(DenseKey.Hand, out string removedByEnum, out var enumRemoveConflict), Is.True, enumRemoveConflict.ToString());
            Assert.That(map.TryRemoveEntry("slot.helmet", out string removedByString, out var stringRemoveConflict), Is.True, stringRemoveConflict.ToString());
            Assert.That(map.TryRemoveEntryAt(0, out string removedByIndex, out var indexRemoveConflict), Is.True, indexRemoveConflict.ToString());

            Assert.That(removedByEnum, Is.EqualTo("hand.pair"));
            Assert.That(removedByString, Is.EqualTo("helmet"));
            Assert.That(removedByIndex, Is.EqualTo("main-weapon"));
            Assert.That(map.Count, Is.Zero);
        }

        [Test]
        public void ConflictingAliases_AreRejectedWithoutMutatingExistingMappings()
        {
            ESEnumStringMirrorMap<DenseKey, string> map = new ESEnumStringMirrorMap<DenseKey, string>();
            Assert.That(map.TryAdd(DenseKey.Hand, "slot.hand", "hand", out _), Is.True);
            Assert.That(map.TryAdd(DenseKey.Head, "slot.head", "head", out _), Is.True);
            int generation = map.Generation;

            Assert.That(map.TrySet(DenseKey.Hand, "slot.head", "invalid", out var conflict), Is.False);

            Assert.That(conflict.Kind, Is.EqualTo(ESEnumStringMirrorMap<DenseKey, string>.ConflictKind.AliasMismatch));
            Assert.That(map.Generation, Is.EqualTo(generation));
            Assert.That(map.TryGetValue(DenseKey.Hand, out string hand), Is.True);
            Assert.That(map.TryGetValue("slot.head", out string head), Is.True);
            Assert.That(hand, Is.EqualTo("hand"));
            Assert.That(head, Is.EqualTo("head"));
        }

        [Test]
        public void BulkReplacement_ReportsDuplicateSerializedAliasesAndKeepsOldAuthority()
        {
            ESEnumStringMirrorMap<DenseKey, string> map = new ESEnumStringMirrorMap<DenseKey, string>();
            Assert.That(map.TryAdd(DenseKey.Hand, "valid", out _), Is.True);
            var entries = new List<ESEnumStringMirrorMap<DenseKey, string>.Entry>
            {
                new ESEnumStringMirrorMap<DenseKey, string>.Entry(DenseKey.Head, "head.one", "one"),
                new ESEnumStringMirrorMap<DenseKey, string>.Entry(DenseKey.Head, "head.two", "two")
            };

            Assert.That(map.TryReplaceEntries(entries, out var conflict), Is.False);

            Assert.That(conflict.Kind, Is.EqualTo(ESEnumStringMirrorMap<DenseKey, string>.ConflictKind.DuplicateEnumKey));
            Assert.That(conflict.EntryIndex, Is.EqualTo(1));
            Assert.That(conflict.ExistingEntryIndex, Is.EqualTo(0));
            Assert.That(map.TryGetValue(DenseKey.Hand, out string existing), Is.True);
            Assert.That(existing, Is.EqualTo("valid"));
        }

        [Test]
        public void DeserializeCallback_DropsRuntimeMirrorsAndLazilyRebuildsFromEntries()
        {
            ESEnumStringMirrorMap<DenseKey, string> map = new ESEnumStringMirrorMap<DenseKey, string>();
            Assert.That(map.TryAdd(DenseKey.Weapon, "weapon.main", "sword", out _), Is.True);
            int generation = map.Generation;

            ((ISerializationCallbackReceiver)map).OnAfterDeserialize();

            Assert.That(map.TryGetValue(DenseKey.Weapon, "weapon.main", out string value), Is.True);
            Assert.That(value, Is.EqualTo("sword"));
            Assert.That(map.Generation, Is.GreaterThan(generation));
        }

        [Test]
        public void SerializedRevisionChange_InvalidatesMirrorsWithoutDeserializeCallback()
        {
            ESEnumStringMirrorMap<DenseKey, string> map = new ESEnumStringMirrorMap<DenseKey, string>();
            Assert.That(map.TryAdd(DenseKey.Hand, "hand.primary", "old", out _), Is.True);
            int generation = map.Generation;

            FieldInfo entriesField = typeof(ESEnumStringMirrorMap<DenseKey, string>).GetField(
                "entries",
                BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo revisionField = typeof(ESEnumStringMirrorMap<DenseKey, string>).GetField(
                "serializedRevision",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(entriesField, Is.Not.Null);
            Assert.That(revisionField, Is.Not.Null);

            var entries = (List<ESEnumStringMirrorMap<DenseKey, string>.Entry>)entriesField.GetValue(map);
            ESEnumStringMirrorMap<DenseKey, string>.Entry edited = entries[0];
            edited.value = "new";
            entries[0] = edited;
            revisionField.SetValue(map, 1);

            Assert.That(map.TryGetValue(DenseKey.Hand, "hand.primary", out string value), Is.True);
            Assert.That(value, Is.EqualTo("new"));
            Assert.That(map.Generation, Is.GreaterThan(generation));
        }

        [Test]
        public void SparseAndNegativeEnums_UseDictionaryFallback()
        {
            ESEnumStringMirrorMap<SparseKey, string> map = new ESEnumStringMirrorMap<SparseKey, string>();
            Assert.That(map.TryAdd(SparseKey.Negative, "negative", out _), Is.True);
            Assert.That(map.TryAdd(SparseKey.Huge, "huge", out _), Is.True);

            object sparseMirror = GetPrivateField(map, "sparseEnumEntries");
            Assert.That(map.TrySetValue(SparseKey.Negative, "negative.updated", out var valueConflict), Is.True, valueConflict.ToString());
            Assert.That(map.TryRemoveEntry(SparseKey.Huge, out string removed, out var removeConflict), Is.True, removeConflict.ToString());

            Assert.That(map.ActiveEnumMirror, Is.EqualTo(ESEnumStringMirrorMap<SparseKey, string>.EnumMirrorKind.SparseDictionary));
            Assert.That(GetPrivateField(map, "sparseEnumEntries"), Is.SameAs(sparseMirror));
            Assert.That(map.TryGetValue(SparseKey.Negative, out string negative), Is.True);
            Assert.That(map.TryGetValue(SparseKey.Huge, out _), Is.False);
            Assert.That(negative, Is.EqualTo("negative.updated"));
            Assert.That(removed, Is.EqualTo("huge"));
        }

        [Test]
        public void RemovingOneAlias_PreservesTheOtherAliasAndEntry()
        {
            ESEnumStringMirrorMap<DenseKey, string> map = new ESEnumStringMirrorMap<DenseKey, string>();
            Assert.That(map.TryAdd(DenseKey.Hand, "slot.hand", "hand", out _), Is.True);

            Assert.That(map.Remove("slot.hand"), Is.True);

            Assert.That(map.Count, Is.EqualTo(1));
            Assert.That(map.TryGetValue(DenseKey.Hand, out string value), Is.True);
            Assert.That(value, Is.EqualTo("hand"));
            Assert.That(map.TryGetValue("slot.hand", out _), Is.False);
        }

        [Test]
        public void RemovingByAliasPair_RemovesTheWholeEntry()
        {
            ESEnumStringMirrorMap<DenseKey, string> map = new ESEnumStringMirrorMap<DenseKey, string>();
            Assert.That(map.TryAdd(DenseKey.Hand, "slot.hand", "hand", out _), Is.True);

            Assert.That(map.TryRemove(DenseKey.Hand, "slot.hand", out string value, out _), Is.True);

            Assert.That(value, Is.EqualTo("hand"));
            Assert.That(map.Count, Is.Zero);
            Assert.That(map.TryGetValue(DenseKey.Hand, out _), Is.False);
            Assert.That(map.TryGetValue("slot.hand", out _), Is.False);
        }

        [Test]
        public void DestroyedUnityObject_IsNotReturnedAsAValidValue()
        {
            ESEnumStringMirrorMap<DenseKey, object> map = new ESEnumStringMirrorMap<DenseKey, object>();
            GameObject target = new GameObject("MirrorMapTarget");
            Assert.That(map.TryAdd(DenseKey.Hand, "slot.hand", target, out _), Is.True);

            Object.DestroyImmediate(target);

            Assert.That(map.ContainsAlias(DenseKey.Hand), Is.True);
            Assert.That(map.ContainsAlias("slot.hand"), Is.True);
            Assert.That(map.ContainsKey(DenseKey.Hand), Is.False);
            Assert.That(map.ContainsKey("slot.hand"), Is.False);
            Assert.That(map.TryGetValue(DenseKey.Hand, out _), Is.False);
            Assert.That(map.TryGetValue("slot.hand", out _), Is.False);
        }

        [Test]
        public void DestroyedUnityObject_AfterDeserializeStillOccupiesAliases()
        {
            ESEnumStringMirrorMap<DenseKey, object> map = new ESEnumStringMirrorMap<DenseKey, object>();
            GameObject target = new GameObject("MirrorMapDeserializedTarget");
            Assert.That(map.TryAdd(DenseKey.Hand, "slot.hand", target, out _), Is.True);

            Object.DestroyImmediate(target);
            ((ISerializationCallbackReceiver)map).OnAfterDeserialize();

            Assert.That(map.IsValid, Is.False);
            Assert.That(map.LastConflict.Kind, Is.EqualTo(ESEnumStringMirrorMap<DenseKey, object>.ConflictKind.NullValue));
            Assert.That(map.ContainsAlias(DenseKey.Hand), Is.True);
            Assert.That(map.ContainsAlias("slot.hand"), Is.True);
            Assert.That(map.ContainsKey(DenseKey.Hand), Is.False);
            Assert.That(map.ContainsKey("slot.hand"), Is.False);
        }

        private static object GetPrivateField<TEnum, TValue>(
            ESEnumStringMirrorMap<TEnum, TValue> map,
            string fieldName)
            where TEnum : struct, System.Enum
        {
            FieldInfo field = typeof(ESEnumStringMirrorMap<TEnum, TValue>).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            return field.GetValue(map);
        }

        [TestCase("")]
        [TestCase(" ")]
        [TestCase(" leading")]
        [TestCase("trailing ")]
        public void StringAliases_MustAlreadyBeNormalized(string key)
        {
            ESEnumStringMirrorMap<DenseKey, string> map = new ESEnumStringMirrorMap<DenseKey, string>();

            Assert.That(map.TryAdd(key, "value", out var conflict), Is.False);
            Assert.That(
                conflict.Kind == ESEnumStringMirrorMap<DenseKey, string>.ConflictKind.MissingKey
                || conflict.Kind == ESEnumStringMirrorMap<DenseKey, string>.ConflictKind.InvalidStringKey,
                Is.True);
            Assert.That(map.Count, Is.Zero);
        }
    }
}
