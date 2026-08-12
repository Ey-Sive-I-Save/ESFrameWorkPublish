using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// Serializable identity map with an enum hot path and an ordinal string extension path.
    /// The entry list is authoritative; runtime indexes are disposable mirrors.
    /// </summary>
    [Serializable]
    public class ESEnumStringMirrorMap<TEnum, TValue> : ISerializationCallbackReceiver
        where TEnum : struct, Enum
    {
        public const int DefaultDenseEnumLimit = 4096;
        public const int DefaultDenseEnumRatio = 8;

        public enum EnumMirrorKind
        {
            None,
            DenseArray,
            SparseDictionary
        }

        public enum ConflictKind
        {
            None,
            MissingKey,
            InvalidStringKey,
            NullValue,
            DuplicateEnumKey,
            DuplicateStringKey,
            AliasMismatch,
            InvalidIndex
        }

        [Serializable]
        public struct Entry
        {
            public bool hasEnumKey;
            public TEnum enumKey;
            public string stringKey;
            public TValue value;

            public bool HasStringKey => !string.IsNullOrEmpty(stringKey);

            public Entry(TEnum enumKey, TValue value)
            {
                hasEnumKey = true;
                this.enumKey = enumKey;
                stringKey = null;
                this.value = value;
            }

            public Entry(string stringKey, TValue value)
            {
                hasEnumKey = false;
                enumKey = default;
                this.stringKey = stringKey;
                this.value = value;
            }

            public Entry(TEnum enumKey, string stringKey, TValue value)
            {
                hasEnumKey = true;
                this.enumKey = enumKey;
                this.stringKey = stringKey;
                this.value = value;
            }
        }

        public readonly struct Conflict
        {
            public static Conflict None => default;

            public ConflictKind Kind { get; }
            public int EntryIndex { get; }
            public int ExistingEntryIndex { get; }
            public string Message { get; }

            public bool HasConflict => Kind != ConflictKind.None;

            internal Conflict(ConflictKind kind, int entryIndex, int existingEntryIndex, string message)
            {
                Kind = kind;
                EntryIndex = entryIndex;
                ExistingEntryIndex = existingEntryIndex;
                Message = message;
            }

            public override string ToString()
            {
                return HasConflict ? Message : "None";
            }
        }

        private sealed class RuntimeMirrors
        {
            public int[] denseEnumEntries;
            public Dictionary<TEnum, int> sparseEnumEntries;
            public Dictionary<string, int> stringEntries;
            public EnumMirrorKind enumMirrorKind;
            public int enumEntryCount;
        }

        private static class EnumNumeric
        {
            public static readonly TypeCode UnderlyingTypeCode = Type.GetTypeCode(Enum.GetUnderlyingType(typeof(TEnum)));
        }

        private static class ValueTypeTraits
        {
            public static readonly bool IsValueType = typeof(TValue).IsValueType;
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();
        [SerializeField, Min(1)] private int denseEnumLimit = DefaultDenseEnumLimit;
        [SerializeField, Min(1)] private int denseEnumRatio = DefaultDenseEnumRatio;
        [SerializeField, HideInInspector] private int serializedRevision;

        [NonSerialized] private int[] denseEnumEntries;
        [NonSerialized] private Dictionary<TEnum, int> sparseEnumEntries;
        [NonSerialized] private Dictionary<string, int> stringEntries;
        [NonSerialized] private EnumMirrorKind enumMirrorKind;
        [NonSerialized] private int enumEntryCount;
        [NonSerialized] private Conflict lastConflict;
        [NonSerialized] private bool isReady;
        [NonSerialized] private bool isDirty = true;
        [NonSerialized] private int generation;
        [NonSerialized] private int observedSerializedRevision;

        public int Count => entries?.Count ?? 0;
        public int Generation => generation;
        public EnumMirrorKind ActiveEnumMirror => EnsureReady() ? enumMirrorKind : EnumMirrorKind.None;
        public bool IsValid => EnsureReady();
        public Conflict LastConflict => lastConflict;

        public TValue this[TEnum enumKey]
        {
            get
            {
                if (TryGetValue(enumKey, out TValue value))
                    return value;
                throw new KeyNotFoundException("Enum key was not found: " + enumKey + ".");
            }
        }

        public TValue this[string stringKey]
        {
            get
            {
                if (TryGetValue(stringKey, out TValue value))
                    return value;
                throw new KeyNotFoundException("String key was not found: " + stringKey + ".");
            }
        }

        public ESEnumStringMirrorMap()
        {
        }

        public ESEnumStringMirrorMap(int capacity)
        {
            if (capacity < 0)
                throw new ArgumentOutOfRangeException(nameof(capacity));
            entries = new List<Entry>(capacity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(TEnum enumKey, out TValue value)
        {
            value = default;
            if (!EnsureReady() || !TryGetEnumEntryIndex(enumKey, out int entryIndex))
                return false;

            TValue candidate = entries[entryIndex].value;
            if (IsNullValue(candidate))
                return false;

            value = candidate;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(string stringKey, out TValue value)
        {
            value = default;
            if (!EnsureReady() || stringKey == null || !stringEntries.TryGetValue(stringKey, out int entryIndex))
                return false;

            TValue candidate = entries[entryIndex].value;
            if (IsNullValue(candidate))
                return false;

            value = candidate;
            return true;
        }

        /// <summary>Both aliases must exist and resolve to the same serialized entry.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(TEnum enumKey, string stringKey, out TValue value)
        {
            value = default;
            if (!EnsureReady()
                || stringKey == null
                || !TryGetEnumEntryIndex(enumKey, out int enumEntryIndex)
                || !stringEntries.TryGetValue(stringKey, out int stringEntryIndex)
                || enumEntryIndex != stringEntryIndex)
            {
                return false;
            }

            TValue candidate = entries[enumEntryIndex].value;
            if (IsNullValue(candidate))
                return false;

            value = candidate;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ContainsKey(TEnum enumKey)
        {
            return TryGetValue(enumKey, out _);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ContainsKey(string stringKey)
        {
            return TryGetValue(stringKey, out _);
        }

        public bool TryGetEntry(TEnum enumKey, out Entry entry)
        {
            if (EnsureReady() && TryGetEnumEntryIndex(enumKey, out int entryIndex))
            {
                entry = entries[entryIndex];
                return true;
            }

            entry = default;
            return false;
        }

        public bool TryGetEntry(string stringKey, out Entry entry)
        {
            if (EnsureReady()
                && stringKey != null
                && stringEntries.TryGetValue(stringKey, out int entryIndex))
            {
                entry = entries[entryIndex];
                return true;
            }

            entry = default;
            return false;
        }

        public bool TryGetEntry(TEnum enumKey, string stringKey, out Entry entry)
        {
            if (EnsureReady()
                && stringKey != null
                && TryGetEnumEntryIndex(enumKey, out int enumEntryIndex)
                && stringEntries.TryGetValue(stringKey, out int stringEntryIndex)
                && enumEntryIndex == stringEntryIndex)
            {
                entry = entries[enumEntryIndex];
                return true;
            }

            entry = default;
            return false;
        }

        /// <summary>Reads by current authoring order. The index is not a stable identity.</summary>
        public bool TryGetEntryAt(int index, out Entry entry)
        {
            if (EnsureReady() && (uint)index < (uint)entries.Count)
            {
                entry = entries[index];
                return true;
            }

            entry = default;
            return false;
        }

        public bool TryAdd(TEnum enumKey, TValue value, out Conflict conflict)
        {
            return TryAddInternal(new Entry(enumKey, value), out conflict);
        }

        public bool TryAdd(string stringKey, TValue value, out Conflict conflict)
        {
            return TryAddInternal(new Entry(stringKey, value), out conflict);
        }

        public bool TryAdd(TEnum enumKey, string stringKey, TValue value, out Conflict conflict)
        {
            return TryAddInternal(new Entry(enumKey, stringKey, value), out conflict);
        }

        public bool TrySet(TEnum enumKey, TValue value, out Conflict conflict)
        {
            return TrySetInternal(true, enumKey, null, value, out conflict);
        }

        public bool TrySet(string stringKey, TValue value, out Conflict conflict)
        {
            return TrySetInternal(false, default, stringKey, value, out conflict);
        }

        /// <summary>Adds missing aliases or updates the one entry already identified by either alias.</summary>
        public bool TrySet(TEnum enumKey, string stringKey, TValue value, out Conflict conflict)
        {
            return TrySetInternal(true, enumKey, stringKey, value, out conflict);
        }

        /// <summary>Updates only the value of an existing enum entry. It never creates an entry or changes aliases.</summary>
        public bool TrySetValue(TEnum enumKey, TValue value, out Conflict conflict)
        {
            if (!TryFindEntryIndex(enumKey, out int entryIndex, out conflict))
                return false;
            return TrySetValueAt(entryIndex, value, out conflict);
        }

        /// <summary>Updates only the value of an existing string entry. It never creates an entry or changes aliases.</summary>
        public bool TrySetValue(string stringKey, TValue value, out Conflict conflict)
        {
            if (!TryFindEntryIndex(stringKey, out int entryIndex, out conflict))
                return false;
            return TrySetValueAt(entryIndex, value, out conflict);
        }

        /// <summary>Updates only the value when both aliases identify the same existing entry.</summary>
        public bool TrySetValue(TEnum enumKey, string stringKey, TValue value, out Conflict conflict)
        {
            if (!TryFindEntryIndex(enumKey, stringKey, out int entryIndex, out conflict))
                return false;
            return TrySetValueAt(entryIndex, value, out conflict);
        }

        /// <summary>Adds or replaces the enum alias on the entry identified by its string alias.</summary>
        public bool TrySetEnumAlias(string existingStringKey, TEnum enumKey, out Conflict conflict)
        {
            if (!TryFindEntryIndex(existingStringKey, out int entryIndex, out conflict))
                return false;

            Entry entry = entries[entryIndex];
            if (entry.hasEnumKey && EqualityComparer<TEnum>.Default.Equals(entry.enumKey, enumKey))
            {
                conflict = Conflict.None;
                return true;
            }
            entry.hasEnumKey = true;
            entry.enumKey = enumKey;
            return TryReplaceEntryAtInternal(entryIndex, entry, out conflict);
        }

        /// <summary>Adds or replaces the string alias on the entry identified by its enum alias.</summary>
        public bool TrySetStringAlias(TEnum existingEnumKey, string stringKey, out Conflict conflict)
        {
            if (!ValidateRequiredStringKey(stringKey, out conflict)
                || !TryFindEntryIndex(existingEnumKey, out int entryIndex, out conflict))
            {
                return false;
            }

            Entry entry = entries[entryIndex];
            if (string.Equals(entry.stringKey, stringKey, StringComparison.Ordinal))
            {
                conflict = Conflict.None;
                return true;
            }
            entry.stringKey = stringKey;
            return TryReplaceEntryAtInternal(entryIndex, entry, out conflict);
        }

        public bool TryReplaceEnumKey(TEnum currentKey, TEnum replacementKey, out Conflict conflict)
        {
            if (!TryFindEntryIndex(currentKey, out int entryIndex, out conflict))
                return false;

            Entry entry = entries[entryIndex];
            if (EqualityComparer<TEnum>.Default.Equals(entry.enumKey, replacementKey))
            {
                conflict = Conflict.None;
                return true;
            }
            entry.enumKey = replacementKey;
            return TryReplaceEntryAtInternal(entryIndex, entry, out conflict);
        }

        public bool TryReplaceStringKey(string currentKey, string replacementKey, out Conflict conflict)
        {
            if (!ValidateRequiredStringKey(replacementKey, out conflict)
                || !TryFindEntryIndex(currentKey, out int entryIndex, out conflict))
            {
                return false;
            }

            Entry entry = entries[entryIndex];
            if (string.Equals(entry.stringKey, replacementKey, StringComparison.Ordinal))
            {
                conflict = Conflict.None;
                return true;
            }
            entry.stringKey = replacementKey;
            return TryReplaceEntryAtInternal(entryIndex, entry, out conflict);
        }

        public bool TryReplaceEntry(TEnum currentKey, Entry replacement, out Conflict conflict)
        {
            if (!TryFindEntryIndex(currentKey, out int entryIndex, out conflict))
                return false;
            return TryReplaceEntryAtInternal(entryIndex, replacement, out conflict);
        }

        public bool TryReplaceEntry(string currentKey, Entry replacement, out Conflict conflict)
        {
            if (!TryFindEntryIndex(currentKey, out int entryIndex, out conflict))
                return false;
            return TryReplaceEntryAtInternal(entryIndex, replacement, out conflict);
        }

        /// <summary>Replaces by current authoring order. The index is not a stable identity.</summary>
        public bool TryReplaceEntryAt(int index, Entry replacement, out Conflict conflict)
        {
            if (!ValidateExistingIndex(index, out conflict))
                return false;
            return TryReplaceEntryAtInternal(index, replacement, out conflict);
        }

        /// <summary>Inserts into the current authoring order and adjusts only affected mirror indexes.</summary>
        public bool TryInsertEntry(int index, Entry entry, out Conflict conflict)
        {
            if (!EnsureReady())
            {
                conflict = lastConflict;
                return false;
            }

            int count = entries.Count;
            if ((uint)index > (uint)count)
            {
                conflict = NewInvalidIndexConflict(index, count, true);
                return false;
            }

            return TryInsertEntryIncremental(index, entry, out conflict);
        }

        /// <summary>Moves one entry in authoring order and adjusts only the moved index range.</summary>
        public bool TryMoveEntry(int fromIndex, int toIndex, out Conflict conflict)
        {
            if (!EnsureReady())
            {
                conflict = lastConflict;
                return false;
            }

            int count = entries?.Count ?? 0;
            if ((uint)fromIndex >= (uint)count)
            {
                conflict = NewInvalidIndexConflict(fromIndex, count, false);
                return false;
            }
            if ((uint)toIndex >= (uint)count)
            {
                conflict = NewInvalidIndexConflict(toIndex, count, false);
                return false;
            }
            if (fromIndex == toIndex)
            {
                conflict = Conflict.None;
                return true;
            }

            Entry moving = entries[fromIndex];
            entries.RemoveAt(fromIndex);
            entries.Insert(toIndex, moving);
            RefreshMirrorIndexes(Math.Min(fromIndex, toIndex), Math.Max(fromIndex, toIndex));
            CompleteIncrementalMutation();
            conflict = Conflict.None;
            return true;
        }

        public bool Remove(TEnum enumKey)
        {
            if (!EnsureReady() || !TryGetEnumEntryIndex(enumKey, out int entryIndex))
                return false;

            Entry entry = entries[entryIndex];
            if (entry.HasStringKey)
            {
                entry.hasEnumKey = false;
                entry.enumKey = default;
                return TryReplaceEntryAtInternal(entryIndex, entry, out _);
            }

            return RemoveAt(entryIndex, out _, out _);
        }

        public bool Remove(string stringKey)
        {
            if (!EnsureReady() || stringKey == null || !stringEntries.TryGetValue(stringKey, out int entryIndex))
                return false;

            Entry entry = entries[entryIndex];
            if (entry.hasEnumKey)
            {
                entry.stringKey = null;
                return TryReplaceEntryAtInternal(entryIndex, entry, out _);
            }

            return RemoveAt(entryIndex, out _, out _);
        }

        /// <summary>Removes only the enum alias. Removing the last alias requires TryRemoveEntry.</summary>
        public bool TryRemoveEnumAlias(TEnum enumKey, out Conflict conflict)
        {
            if (!TryFindEntryIndex(enumKey, out int entryIndex, out conflict))
                return false;

            Entry current = entries[entryIndex];
            if (!current.HasStringKey)
            {
                conflict = NewConflict(
                    ConflictKind.MissingKey,
                    entryIndex,
                    -1,
                    "The enum alias is the entry's last alias. Remove the whole entry instead.");
                return false;
            }

            Entry entry = current;
            entry.hasEnumKey = false;
            entry.enumKey = default;
            return TryReplaceEntryAtInternal(entryIndex, entry, out conflict);
        }

        /// <summary>Removes only the string alias. Removing the last alias requires TryRemoveEntry.</summary>
        public bool TryRemoveStringAlias(string stringKey, out Conflict conflict)
        {
            if (!TryFindEntryIndex(stringKey, out int entryIndex, out conflict))
                return false;

            Entry current = entries[entryIndex];
            if (!current.hasEnumKey)
            {
                conflict = NewConflict(
                    ConflictKind.MissingKey,
                    entryIndex,
                    -1,
                    "The string alias is the entry's last alias. Remove the whole entry instead.");
                return false;
            }

            Entry entry = current;
            entry.stringKey = null;
            return TryReplaceEntryAtInternal(entryIndex, entry, out conflict);
        }

        public bool TryRemoveEntry(TEnum enumKey, out TValue value, out Conflict conflict)
        {
            value = default;
            if (!TryFindEntryIndex(enumKey, out int entryIndex, out conflict))
                return false;
            return RemoveAt(entryIndex, out value, out conflict);
        }

        public bool TryRemoveEntry(string stringKey, out TValue value, out Conflict conflict)
        {
            value = default;
            if (!TryFindEntryIndex(stringKey, out int entryIndex, out conflict))
                return false;
            return RemoveAt(entryIndex, out value, out conflict);
        }

        public bool TryRemoveEntry(
            TEnum enumKey,
            string stringKey,
            out TValue value,
            out Conflict conflict)
        {
            value = default;
            if (!TryFindEntryIndex(enumKey, stringKey, out int entryIndex, out conflict))
                return false;
            return RemoveAt(entryIndex, out value, out conflict);
        }

        /// <summary>Removes by current authoring order. The index is not a stable identity.</summary>
        public bool TryRemoveEntryAt(int index, out TValue value, out Conflict conflict)
        {
            value = default;
            if (!ValidateExistingIndex(index, out conflict))
                return false;
            return RemoveAt(index, out value, out conflict);
        }

        public bool TryRemove(TEnum enumKey, string stringKey, out TValue value, out Conflict conflict)
        {
            return TryRemoveEntry(enumKey, stringKey, out value, out conflict);
        }

        public void Clear()
        {
            if (entries == null)
                entries = new List<Entry>();
            if (entries.Count == 0 && isReady)
                return;

            entries.Clear();
            ApplyMirrors(CreateEmptyMirrors());
            AdvanceGeneration();
        }

        /// <summary>Atomically replaces serialized authority after validating every alias.</summary>
        public bool TryReplaceEntries(IEnumerable<Entry> source, out Conflict conflict)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            List<Entry> candidate = source is ICollection<Entry> collection
                ? new List<Entry>(collection.Count)
                : new List<Entry>();
            candidate.AddRange(source);
            return TryCommit(candidate, out conflict);
        }

        /// <summary>Atomically appends a batch after validating it together with all existing entries.</summary>
        public bool TryAddEntries(IEnumerable<Entry> source, out Conflict conflict)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            List<Entry> candidate = CloneEntries();
            candidate.AddRange(source);
            return TryCommit(candidate, out conflict);
        }

        /// <summary>Reserves runtime list capacity without changing content or Generation.</summary>
        public void EnsureCapacity(int capacity)
        {
            if (capacity < 0)
                throw new ArgumentOutOfRangeException(nameof(capacity));
            entries ??= new List<Entry>(capacity);
            if (entries.Capacity < capacity)
                entries.Capacity = capacity;
        }

        public void CopyEntries(List<Entry> destination)
        {
            if (destination == null)
                throw new ArgumentNullException(nameof(destination));
            if (entries != null)
                destination.AddRange(entries);
        }

        /// <summary>Call after direct SerializedProperty/Odin edits when no deserialize callback occurs.</summary>
        public void MarkDirty()
        {
            isDirty = true;
            isReady = false;
        }

        public bool TryRebuild(out Conflict conflict)
        {
            if (entries == null)
                entries = new List<Entry>();

            if (!TryBuildMirrors(entries, out RuntimeMirrors mirrors, out conflict))
            {
                ClearRuntimeMirrors();
                lastConflict = conflict;
                isDirty = false;
                isReady = false;
                return false;
            }

            ApplyMirrors(mirrors);
            AdvanceGeneration();
            conflict = Conflict.None;
            return true;
        }

        public void RebuildOrThrow()
        {
            if (!TryRebuild(out Conflict conflict))
                throw new InvalidOperationException(conflict.Message);
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
        }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            if (entries == null)
                entries = new List<Entry>();
            ClearRuntimeMirrors();
            lastConflict = Conflict.None;
            isDirty = true;
        }

        private bool TryAddInternal(Entry entry, out Conflict conflict)
        {
            if (!EnsureReady())
            {
                conflict = lastConflict;
                return false;
            }

            return TryInsertEntryIncremental(entries.Count, entry, out conflict);
        }

        private bool TrySetInternal(bool hasEnumKey, TEnum enumKey, string stringKey, TValue value, out Conflict conflict)
        {
            bool hasStringKey = !string.IsNullOrEmpty(stringKey);
            if (!hasEnumKey && !hasStringKey)
            {
                conflict = NewConflict(ConflictKind.MissingKey, -1, -1, "At least one alias is required.");
                return false;
            }

            if (!ValidateStringKey(stringKey, hasStringKey, -1, out conflict) || !ValidateValue(value, -1, out conflict))
                return false;

            if (!EnsureReady())
            {
                conflict = lastConflict;
                return false;
            }

            int enumEntryIndex = -1;
            int stringEntryIndex = -1;
            bool enumExists = hasEnumKey && TryGetEnumEntryIndex(enumKey, out enumEntryIndex);
            bool stringExists = hasStringKey && stringEntries.TryGetValue(stringKey, out stringEntryIndex);
            if (enumExists && stringExists && enumEntryIndex != stringEntryIndex)
            {
                conflict = NewConflict(ConflictKind.AliasMismatch, enumEntryIndex, stringEntryIndex, "Enum and string aliases already resolve to different entries.");
                return false;
            }

            int targetIndex = enumExists ? enumEntryIndex : stringExists ? stringEntryIndex : -1;
            if (targetIndex < 0)
            {
                return TryInsertEntryIncremental(entries.Count, new Entry
                {
                    hasEnumKey = hasEnumKey,
                    enumKey = enumKey,
                    stringKey = hasStringKey ? stringKey : null,
                    value = value
                }, out conflict);
            }

            Entry target = entries[targetIndex];
            if (hasEnumKey)
            {
                target.hasEnumKey = true;
                target.enumKey = enumKey;
            }
            if (hasStringKey)
                target.stringKey = stringKey;
            target.value = value;
            return TryReplaceEntryAtInternal(targetIndex, target, out conflict);
        }

        private bool TrySetValueAt(int entryIndex, TValue value, out Conflict conflict)
        {
            if (!ValidateValue(value, entryIndex, out conflict))
                return false;

            Entry entry = entries[entryIndex];
            if (EqualityComparer<TValue>.Default.Equals(entry.value, value))
            {
                conflict = Conflict.None;
                return true;
            }

            entry.value = value;
            entries[entryIndex] = entry;
            CompleteIncrementalMutation();
            conflict = Conflict.None;
            return true;
        }

        private bool TryReplaceEntryAtInternal(int entryIndex, Entry replacement, out Conflict conflict)
        {
            if (!ValidateEntry(replacement, entryIndex, entryIndex, out conflict))
            {
                lastConflict = conflict;
                return false;
            }

            Entry current = entries[entryIndex];
            bool enumChanged = current.hasEnumKey != replacement.hasEnumKey
                || current.hasEnumKey && !EqualityComparer<TEnum>.Default.Equals(current.enumKey, replacement.enumKey);
            bool stringChanged = !string.Equals(current.stringKey, replacement.stringKey, StringComparison.Ordinal);

            int futureEnumCount = enumEntryCount;
            if (current.hasEnumKey != replacement.hasEnumKey)
                futureEnumCount += replacement.hasEnumKey ? 1 : -1;
            if (replacement.hasEnumKey && enumChanged)
                PrepareEnumMirrorForKey(replacement.enumKey, futureEnumCount);

            if (enumChanged && current.hasEnumKey)
                RemoveEnumMirrorEntry(current.enumKey);
            if (stringChanged && current.HasStringKey)
                stringEntries.Remove(current.stringKey);

            entries[entryIndex] = replacement;
            enumEntryCount = futureEnumCount;

            if (enumChanged && replacement.hasEnumKey)
                SetEnumMirrorEntry(replacement.enumKey, entryIndex);
            if (stringChanged && replacement.HasStringKey)
                stringEntries[replacement.stringKey] = entryIndex;

            NormalizeEmptyEnumMirror();
            CompleteIncrementalMutation();
            conflict = Conflict.None;
            return true;
        }

        private bool ValidateExistingIndex(int index, out Conflict conflict)
        {
            if (!EnsureReady())
            {
                conflict = lastConflict;
                return false;
            }

            int count = entries.Count;
            if ((uint)index < (uint)count)
            {
                conflict = Conflict.None;
                return true;
            }

            conflict = NewInvalidIndexConflict(index, count, false);
            return false;
        }

        private static Conflict NewInvalidIndexConflict(int index, int count, bool allowEnd)
        {
            string validRange = allowEnd
                ? "0 through " + count
                : count > 0 ? "0 through " + (count - 1) : "an empty range";
            return NewConflict(
                ConflictKind.InvalidIndex,
                index,
                -1,
                "Index " + index + " is outside the valid range " + validRange + ".");
        }

        private bool RemoveAt(int entryIndex, out TValue value, out Conflict conflict)
        {
            Entry removed = entries[entryIndex];
            value = removed.value;

            if (removed.hasEnumKey)
                RemoveEnumMirrorEntry(removed.enumKey);
            if (removed.HasStringKey)
                stringEntries.Remove(removed.stringKey);

            entries.RemoveAt(entryIndex);
            if (removed.hasEnumKey)
                enumEntryCount--;
            NormalizeEmptyEnumMirror();
            RefreshMirrorIndexes(entryIndex, entries.Count - 1);
            CompleteIncrementalMutation();
            conflict = Conflict.None;
            return true;
        }

        private bool TryFindEntryIndex(TEnum enumKey, out int entryIndex, out Conflict conflict)
        {
            if (!EnsureReady())
            {
                entryIndex = -1;
                conflict = lastConflict;
                return false;
            }

            if (TryGetEnumEntryIndex(enumKey, out entryIndex))
            {
                conflict = Conflict.None;
                return true;
            }

            conflict = NewConflict(ConflictKind.MissingKey, -1, -1, "Enum key was not found: " + enumKey + ".");
            return false;
        }

        private bool TryFindEntryIndex(string stringKey, out int entryIndex, out Conflict conflict)
        {
            if (!EnsureReady())
            {
                entryIndex = -1;
                conflict = lastConflict;
                return false;
            }

            if (stringKey != null && stringEntries.TryGetValue(stringKey, out entryIndex))
            {
                conflict = Conflict.None;
                return true;
            }

            entryIndex = -1;
            conflict = NewConflict(ConflictKind.MissingKey, -1, -1, "String key was not found: " + (stringKey ?? "<null>") + ".");
            return false;
        }

        private bool TryFindEntryIndex(
            TEnum enumKey,
            string stringKey,
            out int entryIndex,
            out Conflict conflict)
        {
            entryIndex = -1;
            if (!EnsureReady())
            {
                conflict = lastConflict;
                return false;
            }

            bool hasEnum = TryGetEnumEntryIndex(enumKey, out int enumEntryIndex);
            int stringEntryIndex = -1;
            bool hasString = stringKey != null && stringEntries.TryGetValue(stringKey, out stringEntryIndex);
            if (!hasEnum || !hasString)
            {
                conflict = NewConflict(
                    ConflictKind.MissingKey,
                    -1,
                    -1,
                    "Both aliases must exist and identify one entry.");
                return false;
            }

            if (enumEntryIndex != stringEntryIndex)
            {
                conflict = NewConflict(
                    ConflictKind.AliasMismatch,
                    enumEntryIndex,
                    stringEntryIndex,
                    "Enum and string aliases resolve to different entries.");
                return false;
            }

            entryIndex = enumEntryIndex;
            conflict = Conflict.None;
            return true;
        }

        private static bool ValidateRequiredStringKey(string stringKey, out Conflict conflict)
        {
            if (!string.IsNullOrEmpty(stringKey))
                return ValidateStringKey(stringKey, true, -1, out conflict);

            conflict = NewConflict(ConflictKind.MissingKey, -1, -1, "A non-empty string alias is required.");
            return false;
        }

        private bool TryCommit(List<Entry> candidate, out Conflict conflict)
        {
            if (!TryBuildMirrors(candidate, out RuntimeMirrors mirrors, out conflict))
            {
                lastConflict = conflict;
                return false;
            }

            entries = candidate;
            ApplyMirrors(mirrors);
            AdvanceGeneration();
            conflict = Conflict.None;
            return true;
        }

        private bool TryInsertEntryIncremental(int index, Entry entry, out Conflict conflict)
        {
            if (!ValidateEntry(entry, index, -1, out conflict))
            {
                lastConflict = conflict;
                return false;
            }

            int futureEnumCount = enumEntryCount + (entry.hasEnumKey ? 1 : 0);
            if (entry.hasEnumKey)
                PrepareEnumMirrorForKey(entry.enumKey, futureEnumCount);

            entries.Insert(index, entry);
            enumEntryCount = futureEnumCount;
            RefreshMirrorIndexes(index, entries.Count - 1);
            CompleteIncrementalMutation();
            conflict = Conflict.None;
            return true;
        }

        private bool ValidateEntry(Entry entry, int entryIndex, int ignoredIndex, out Conflict conflict)
        {
            bool hasStringKey = entry.HasStringKey;
            if (!entry.hasEnumKey && !hasStringKey)
            {
                conflict = NewConflict(
                    ConflictKind.MissingKey,
                    entryIndex,
                    -1,
                    "Entry " + entryIndex + " has neither an enum alias nor a string alias.");
                return false;
            }

            if (!ValidateStringKey(entry.stringKey, hasStringKey, entryIndex, out conflict)
                || !ValidateValue(entry.value, entryIndex, out conflict))
            {
                return false;
            }

            if (entry.hasEnumKey
                && TryGetEnumEntryIndex(entry.enumKey, out int enumExistingIndex)
                && enumExistingIndex != ignoredIndex)
            {
                conflict = NewConflict(
                    ConflictKind.DuplicateEnumKey,
                    entryIndex,
                    enumExistingIndex,
                    "Entry " + entryIndex + " duplicates enum alias from entry " + enumExistingIndex + ".");
                return false;
            }

            if (hasStringKey
                && stringEntries.TryGetValue(entry.stringKey, out int stringExistingIndex)
                && stringExistingIndex != ignoredIndex)
            {
                conflict = NewConflict(
                    ConflictKind.DuplicateStringKey,
                    entryIndex,
                    stringExistingIndex,
                    "Entry " + entryIndex + " duplicates string alias from entry " + stringExistingIndex + ".");
                return false;
            }

            conflict = Conflict.None;
            return true;
        }

        private void PrepareEnumMirrorForKey(TEnum enumKey, int futureEnumCount)
        {
            if (enumMirrorKind == EnumMirrorKind.SparseDictionary)
                return;

            if (!TryGetDenseEnumIndex(enumKey, out int denseIndex) || !CanUseDenseEnumIndex(denseIndex, futureEnumCount))
            {
                ConvertEnumMirrorToSparse(futureEnumCount);
                return;
            }

            if (enumMirrorKind == EnumMirrorKind.None)
            {
                denseEnumEntries = new int[denseIndex + 1];
                enumMirrorKind = EnumMirrorKind.DenseArray;
                return;
            }

            if (denseIndex >= denseEnumEntries.Length)
                Array.Resize(ref denseEnumEntries, denseIndex + 1);
        }

        private bool CanUseDenseEnumIndex(int denseIndex, int futureEnumCount)
        {
            int limit = denseEnumLimit > 0 ? denseEnumLimit : DefaultDenseEnumLimit;
            int ratio = denseEnumRatio > 0 ? denseEnumRatio : DefaultDenseEnumRatio;
            int densityLimit = Math.Max(64, futureEnumCount * ratio);
            return denseIndex <= limit && denseIndex <= densityLimit;
        }

        private void ConvertEnumMirrorToSparse(int capacity)
        {
            Dictionary<TEnum, int> sparse = new Dictionary<TEnum, int>(Math.Max(capacity, enumEntryCount));
            for (int index = 0; index < entries.Count; index++)
            {
                Entry entry = entries[index];
                if (entry.hasEnumKey)
                    sparse.Add(entry.enumKey, index);
            }

            denseEnumEntries = Array.Empty<int>();
            sparseEnumEntries = sparse;
            enumMirrorKind = EnumMirrorKind.SparseDictionary;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetEnumMirrorEntry(TEnum enumKey, int entryIndex)
        {
            if (enumMirrorKind == EnumMirrorKind.DenseArray)
            {
                TryGetDenseEnumIndex(enumKey, out int denseIndex);
                denseEnumEntries[denseIndex] = entryIndex + 1;
                return;
            }

            sparseEnumEntries[enumKey] = entryIndex;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RemoveEnumMirrorEntry(TEnum enumKey)
        {
            if (enumMirrorKind == EnumMirrorKind.DenseArray)
            {
                if (TryGetDenseEnumIndex(enumKey, out int denseIndex)
                    && (uint)denseIndex < (uint)denseEnumEntries.Length)
                {
                    denseEnumEntries[denseIndex] = 0;
                }
                return;
            }

            sparseEnumEntries?.Remove(enumKey);
        }

        private void RefreshMirrorIndexes(int firstIndex, int lastIndex)
        {
            for (int index = firstIndex; index <= lastIndex; index++)
            {
                Entry entry = entries[index];
                if (entry.hasEnumKey)
                    SetEnumMirrorEntry(entry.enumKey, index);
                if (entry.HasStringKey)
                    stringEntries[entry.stringKey] = index;
            }
        }

        private void NormalizeEmptyEnumMirror()
        {
            if (enumEntryCount != 0)
                return;

            denseEnumEntries = Array.Empty<int>();
            sparseEnumEntries = null;
            enumMirrorKind = EnumMirrorKind.None;
        }

        private void CompleteIncrementalMutation()
        {
            lastConflict = Conflict.None;
            isDirty = false;
            isReady = true;
            observedSerializedRevision = serializedRevision;
            AdvanceGeneration();
        }

        private bool EnsureReady()
        {
            if (observedSerializedRevision != serializedRevision)
            {
                observedSerializedRevision = serializedRevision;
                isDirty = true;
                isReady = false;
            }

            if (isReady)
                return true;
            if (!isDirty)
                return false;
            return TryRebuild(out _);
        }

        private bool TryBuildMirrors(List<Entry> source, out RuntimeMirrors mirrors, out Conflict conflict)
        {
            int capacity = source.Count;
            Dictionary<TEnum, int> enumEntries = new Dictionary<TEnum, int>(capacity);
            Dictionary<string, int> strings = new Dictionary<string, int>(capacity, StringComparer.Ordinal);
            bool canUseDenseEnum = true;
            int enumCount = 0;
            int maxDenseIndex = -1;

            for (int i = 0; i < source.Count; i++)
            {
                Entry entry = source[i];
                bool hasStringKey = !string.IsNullOrEmpty(entry.stringKey);
                if (!entry.hasEnumKey && !hasStringKey)
                {
                    mirrors = null;
                    conflict = NewConflict(ConflictKind.MissingKey, i, -1, "Entry " + i + " has neither an enum alias nor a string alias.");
                    return false;
                }

                if (!ValidateStringKey(entry.stringKey, hasStringKey, i, out conflict)
                    || !ValidateValue(entry.value, i, out conflict))
                {
                    mirrors = null;
                    return false;
                }

                if (entry.hasEnumKey)
                {
                    if (enumEntries.TryGetValue(entry.enumKey, out int existingIndex))
                    {
                        mirrors = null;
                        conflict = NewConflict(ConflictKind.DuplicateEnumKey, i, existingIndex, "Entry " + i + " duplicates enum alias from entry " + existingIndex + ".");
                        return false;
                    }

                    enumEntries.Add(entry.enumKey, i);
                    enumCount++;
                    if (canUseDenseEnum && TryGetDenseEnumIndex(entry.enumKey, out int denseIndex))
                        maxDenseIndex = Math.Max(maxDenseIndex, denseIndex);
                    else
                        canUseDenseEnum = false;
                }

                if (hasStringKey)
                {
                    if (strings.TryGetValue(entry.stringKey, out int existingIndex))
                    {
                        mirrors = null;
                        conflict = NewConflict(ConflictKind.DuplicateStringKey, i, existingIndex, "Entry " + i + " duplicates string alias from entry " + existingIndex + ".");
                        return false;
                    }
                    strings.Add(entry.stringKey, i);
                }
            }

            int limit = denseEnumLimit > 0 ? denseEnumLimit : DefaultDenseEnumLimit;
            int ratio = denseEnumRatio > 0 ? denseEnumRatio : DefaultDenseEnumRatio;
            int densityLimit = Math.Max(64, enumCount * ratio);
            canUseDenseEnum = enumCount > 0
                && canUseDenseEnum
                && maxDenseIndex <= limit
                && maxDenseIndex <= densityLimit;

            mirrors = new RuntimeMirrors
            {
                stringEntries = strings,
                enumEntryCount = enumCount
            };

            if (enumCount == 0)
            {
                mirrors.denseEnumEntries = Array.Empty<int>();
                mirrors.enumMirrorKind = EnumMirrorKind.None;
            }
            else if (canUseDenseEnum)
            {
                int[] denseEntries = new int[maxDenseIndex + 1];
                foreach (KeyValuePair<TEnum, int> pair in enumEntries)
                {
                    TryGetDenseEnumIndex(pair.Key, out int denseIndex);
                    denseEntries[denseIndex] = pair.Value + 1;
                }
                mirrors.denseEnumEntries = denseEntries;
                mirrors.enumMirrorKind = EnumMirrorKind.DenseArray;
            }
            else
            {
                mirrors.denseEnumEntries = Array.Empty<int>();
                mirrors.sparseEnumEntries = enumEntries;
                mirrors.enumMirrorKind = EnumMirrorKind.SparseDictionary;
            }

            conflict = Conflict.None;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryGetEnumEntryIndex(TEnum enumKey, out int entryIndex)
        {
            if (enumMirrorKind == EnumMirrorKind.DenseArray)
            {
                if (TryGetDenseEnumIndex(enumKey, out int denseIndex) && (uint)denseIndex < (uint)denseEnumEntries.Length)
                {
                    int encodedIndex = denseEnumEntries[denseIndex];
                    if (encodedIndex != 0)
                    {
                        entryIndex = encodedIndex - 1;
                        return true;
                    }
                }
                entryIndex = -1;
                return false;
            }

            if (enumMirrorKind == EnumMirrorKind.SparseDictionary)
                return sparseEnumEntries.TryGetValue(enumKey, out entryIndex);

            entryIndex = -1;
            return false;
        }

        private static bool TryGetDenseEnumIndex(TEnum value, out int index)
        {
            switch (EnumNumeric.UnderlyingTypeCode)
            {
                case TypeCode.SByte:
                    sbyte signedByte = Unsafe.As<TEnum, sbyte>(ref value);
                    index = signedByte;
                    return signedByte >= 0;
                case TypeCode.Byte:
                    index = Unsafe.As<TEnum, byte>(ref value);
                    return true;
                case TypeCode.Int16:
                    short signedShort = Unsafe.As<TEnum, short>(ref value);
                    index = signedShort;
                    return signedShort >= 0;
                case TypeCode.UInt16:
                    index = Unsafe.As<TEnum, ushort>(ref value);
                    return true;
                case TypeCode.Int32:
                    index = Unsafe.As<TEnum, int>(ref value);
                    return index >= 0;
                case TypeCode.UInt32:
                    uint unsignedInt = Unsafe.As<TEnum, uint>(ref value);
                    index = (int)unsignedInt;
                    return unsignedInt <= int.MaxValue;
                case TypeCode.Int64:
                    long signedLong = Unsafe.As<TEnum, long>(ref value);
                    index = (int)signedLong;
                    return signedLong >= 0 && signedLong <= int.MaxValue;
                case TypeCode.UInt64:
                    ulong unsignedLong = Unsafe.As<TEnum, ulong>(ref value);
                    index = (int)unsignedLong;
                    return unsignedLong <= int.MaxValue;
                default:
                    index = -1;
                    return false;
            }
        }

        protected static bool ValidateStringKey(string stringKey, bool hasStringKey, int entryIndex, out Conflict conflict)
        {
            if (!hasStringKey)
            {
                conflict = Conflict.None;
                return true;
            }

            if (string.IsNullOrWhiteSpace(stringKey) || !string.Equals(stringKey, stringKey.Trim(), StringComparison.Ordinal))
            {
                conflict = NewConflict(ConflictKind.InvalidStringKey, entryIndex, -1, "String aliases must be non-blank and must not contain leading or trailing whitespace.");
                return false;
            }

            conflict = Conflict.None;
            return true;
        }

        protected static bool ValidateValue(TValue value, int entryIndex, out Conflict conflict)
        {
            if (IsNullValue(value))
            {
                conflict = NewConflict(ConflictKind.NullValue, entryIndex, -1, "Entry " + entryIndex + " has a null or destroyed value.");
                return false;
            }

            conflict = Conflict.None;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsNullValue(TValue value)
        {
            if (ValueTypeTraits.IsValueType)
                return false;
            if (ReferenceEquals(value, null))
                return true;
            return value is UnityEngine.Object unityObject && unityObject == null;
        }

        private List<Entry> CloneEntriesWithAdditionalCapacity()
        {
            int count = entries?.Count ?? 0;
            List<Entry> candidate = new List<Entry>(count + 1);
            if (count > 0)
                candidate.AddRange(entries);
            return candidate;
        }

        private List<Entry> CloneEntries()
        {
            return entries != null ? new List<Entry>(entries) : new List<Entry>();
        }

        private void ApplyMirrors(RuntimeMirrors mirrors)
        {
            denseEnumEntries = mirrors.denseEnumEntries;
            sparseEnumEntries = mirrors.sparseEnumEntries;
            stringEntries = mirrors.stringEntries;
            enumMirrorKind = mirrors.enumMirrorKind;
            enumEntryCount = mirrors.enumEntryCount;
            lastConflict = Conflict.None;
            isDirty = false;
            isReady = true;
            observedSerializedRevision = serializedRevision;
        }

        private void ClearRuntimeMirrors()
        {
            denseEnumEntries = null;
            sparseEnumEntries = null;
            stringEntries = null;
            enumMirrorKind = EnumMirrorKind.None;
            enumEntryCount = 0;
            isReady = false;
        }

        private RuntimeMirrors CreateEmptyMirrors()
        {
            return new RuntimeMirrors
            {
                denseEnumEntries = Array.Empty<int>(),
                stringEntries = new Dictionary<string, int>(StringComparer.Ordinal),
                enumMirrorKind = EnumMirrorKind.None,
                enumEntryCount = 0
            };
        }

        private void AdvanceGeneration()
        {
            unchecked
            {
                generation++;
                if (generation == 0)
                    generation++;
            }
        }

        protected static Conflict NewConflict(ConflictKind kind, int entryIndex, int existingEntryIndex, string message)
        {
            return new Conflict(kind, entryIndex, existingEntryIndex, message);
        }
    }
}
