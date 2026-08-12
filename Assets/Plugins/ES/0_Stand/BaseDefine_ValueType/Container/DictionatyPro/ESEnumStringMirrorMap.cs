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
            AliasMismatch
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

        [NonSerialized] private int[] denseEnumEntries;
        [NonSerialized] private Dictionary<TEnum, int> sparseEnumEntries;
        [NonSerialized] private Dictionary<string, int> stringEntries;
        [NonSerialized] private EnumMirrorKind enumMirrorKind;
        [NonSerialized] private Conflict lastConflict;
        [NonSerialized] private bool isReady;
        [NonSerialized] private bool isDirty = true;
        [NonSerialized] private int generation;

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

        public bool Remove(TEnum enumKey)
        {
            if (!EnsureReady() || !TryGetEnumEntryIndex(enumKey, out int entryIndex))
                return false;

            List<Entry> candidate = CloneEntriesWithAdditionalCapacity();
            Entry entry = candidate[entryIndex];
            if (entry.HasStringKey)
            {
                entry.hasEnumKey = false;
                entry.enumKey = default;
                candidate[entryIndex] = entry;
            }
            else
            {
                candidate.RemoveAt(entryIndex);
            }

            return TryCommit(candidate, out _);
        }

        public bool Remove(string stringKey)
        {
            if (!EnsureReady() || stringKey == null || !stringEntries.TryGetValue(stringKey, out int entryIndex))
                return false;

            List<Entry> candidate = CloneEntriesWithAdditionalCapacity();
            Entry entry = candidate[entryIndex];
            if (entry.hasEnumKey)
            {
                entry.stringKey = null;
                candidate[entryIndex] = entry;
            }
            else
            {
                candidate.RemoveAt(entryIndex);
            }

            return TryCommit(candidate, out _);
        }

        public bool TryRemove(TEnum enumKey, string stringKey, out TValue value, out Conflict conflict)
        {
            value = default;
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
                conflict = NewConflict(ConflictKind.MissingKey, -1, -1, "Both aliases must exist before an entry can be removed by alias pair.");
                return false;
            }

            if (enumEntryIndex != stringEntryIndex)
            {
                conflict = NewConflict(ConflictKind.AliasMismatch, enumEntryIndex, stringEntryIndex, "Enum and string aliases resolve to different entries.");
                return false;
            }

            conflict = Conflict.None;
            return RemoveAt(enumEntryIndex, out value);
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
            List<Entry> candidate = CloneEntriesWithAdditionalCapacity();
            candidate.Add(entry);
            return TryCommit(candidate, out conflict);
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
            List<Entry> candidate = CloneEntriesWithAdditionalCapacity();
            if (targetIndex < 0)
            {
                candidate.Add(new Entry
                {
                    hasEnumKey = hasEnumKey,
                    enumKey = enumKey,
                    stringKey = hasStringKey ? stringKey : null,
                    value = value
                });
            }
            else
            {
                Entry target = candidate[targetIndex];
                if (hasEnumKey)
                {
                    target.hasEnumKey = true;
                    target.enumKey = enumKey;
                }
                if (hasStringKey)
                    target.stringKey = stringKey;
                target.value = value;
                candidate[targetIndex] = target;
            }

            return TryCommit(candidate, out conflict);
        }

        private bool RemoveAt(int entryIndex, out TValue value)
        {
            List<Entry> candidate = CloneEntriesWithAdditionalCapacity();
            value = candidate[entryIndex].value;
            candidate.RemoveAt(entryIndex);
            return TryCommit(candidate, out _);
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

        private bool EnsureReady()
        {
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
                stringEntries = strings
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

        private void ApplyMirrors(RuntimeMirrors mirrors)
        {
            denseEnumEntries = mirrors.denseEnumEntries;
            sparseEnumEntries = mirrors.sparseEnumEntries;
            stringEntries = mirrors.stringEntries;
            enumMirrorKind = mirrors.enumMirrorKind;
            lastConflict = Conflict.None;
            isDirty = false;
            isReady = true;
        }

        private void ClearRuntimeMirrors()
        {
            denseEnumEntries = null;
            sparseEnumEntries = null;
            stringEntries = null;
            enumMirrorKind = EnumMirrorKind.None;
            isReady = false;
        }

        private RuntimeMirrors CreateEmptyMirrors()
        {
            return new RuntimeMirrors
            {
                denseEnumEntries = Array.Empty<int>(),
                stringEntries = new Dictionary<string, int>(StringComparer.Ordinal),
                enumMirrorKind = EnumMirrorKind.None
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
