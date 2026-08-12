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

            Assert.That(map.ActiveEnumMirror, Is.EqualTo(ESEnumStringMirrorMap<SparseKey, string>.EnumMirrorKind.SparseDictionary));
            Assert.That(map.TryGetValue(SparseKey.Negative, out string negative), Is.True);
            Assert.That(map.TryGetValue(SparseKey.Huge, out string huge), Is.True);
            Assert.That(negative, Is.EqualTo("negative"));
            Assert.That(huge, Is.EqualTo("huge"));
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

            Assert.That(map.TryGetValue(DenseKey.Hand, out _), Is.False);
            Assert.That(map.TryGetValue("slot.hand", out _), Is.False);
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
