using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

#if UNITY_EDITOR
using Sirenix.OdinInspector;
#endif

namespace ES
{
    public enum ESTagBuiltin : ushort
    {
        None = 0,
        CustomStart = 1
    }

    public enum ESTagMaskLevel : byte
    {
        Mask32 = 32,
        Mask64 = 64,
        Mask256 = 255
    }

    public static class ESTagMaskLevelUtility
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetRuntimeCapacity(ESTagMaskLevel level)
        {
            switch (level)
            {
                case ESTagMaskLevel.Mask32:
                    return ESTagMask32.MaxTagCount;
                case ESTagMaskLevel.Mask64:
                    return ESTagMask64.MaxTagCount;
                case ESTagMaskLevel.Mask256:
                    return ESTagMask256.MaxTagCount;
                default:
                    return ESTagMask32.MaxTagCount;
            }
        }
    }

    public static class ESTagIdRange
    {
        public const ushort Invalid = 0;
        public const ushort EnumStart = 1;
        public const ushort EnumDefaultEnd = 63;
        public const ushort StringDefaultStart = 1;
        public const ushort StringDefaultEnd = 63;
        public const ushort Mask32End = 31;
        public const ushort Mask64End = 63;
        public const ushort Mask256End = 255;
        public const ushort MaxValue = ushort.MaxValue;
    }

    [Serializable]
    public struct ESTagId : IEquatable<ESTagId>
    {
        public const ushort InvalidValue = 0;

        [SerializeField]
        private ushort value;

        public ESTagId(ushort value)
        {
            this.value = value;
        }

        public ushort Value
        {
            get { return value; }
        }

        public bool IsValid
        {
            get { return value != InvalidValue; }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(ESTagId other)
        {
            return value == other.value;
        }

        public override bool Equals(object obj)
        {
            return obj is ESTagId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ESTagId FromValue(ushort value)
        {
            return new ESTagId(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ESTagId FromInt32(int value)
        {
            return value > InvalidValue && value <= ushort.MaxValue
                ? new ESTagId((ushort)value)
                : Invalid;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ESTagId FromBuiltin(ESTagBuiltin tag)
        {
            return new ESTagId((ushort)tag);
        }

        public static readonly ESTagId Invalid = new ESTagId(InvalidValue);

        public static bool operator ==(ESTagId left, ESTagId right)
        {
            return left.value == right.value;
        }

        public static bool operator !=(ESTagId left, ESTagId right)
        {
            return left.value != right.value;
        }
    }

    [Serializable]
    public struct ESTagMask32
    {
        public const int MaxTagCount = 32;

        [SerializeField] private uint bits;

        public bool IsEmpty
        {
            get { return bits == 0U; }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            bits = 0U;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Add(ESTagId tag)
        {
            ushort value = tag.Value;
            if (value == 0 || value >= MaxTagCount)
                return false;

            bits |= 1U << value;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Remove(ESTagId tag)
        {
            ushort value = tag.Value;
            if (value == 0 || value >= MaxTagCount)
                return false;

            bits &= ~(1U << value);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(ESTagId tag)
        {
            ushort value = tag.Value;
            return value != 0
                   && value < MaxTagCount
                   && (bits & (1U << value)) != 0U;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Overlaps(ESTagMask32 other)
        {
            return (bits & other.bits) != 0U;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ContainsAll(ESTagMask32 other)
        {
            return (bits & other.bits) == other.bits;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal uint GetBits()
        {
            return bits;
        }

        public static ESTagMask32 From(ESTagId tag)
        {
            ESTagMask32 mask = default;
            mask.Add(tag);
            return mask;
        }

        public static ESTagMask32 From(ESTagId tag0, ESTagId tag1)
        {
            ESTagMask32 mask = default;
            mask.Add(tag0);
            mask.Add(tag1);
            return mask;
        }

        public static ESTagMask32 From(ESTagId tag0, ESTagId tag1, ESTagId tag2)
        {
            ESTagMask32 mask = default;
            mask.Add(tag0);
            mask.Add(tag1);
            mask.Add(tag2);
            return mask;
        }

        public static ESTagMask32 From(ESTagId tag0, ESTagId tag1, ESTagId tag2, ESTagId tag3)
        {
            ESTagMask32 mask = default;
            mask.Add(tag0);
            mask.Add(tag1);
            mask.Add(tag2);
            mask.Add(tag3);
            return mask;
        }
    }

    [Serializable]
    public struct ESTagMask64
    {
        public const int MaxTagCount = 64;

        [SerializeField] private ulong bits;

        public bool IsEmpty
        {
            get { return bits == 0UL; }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            bits = 0UL;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Add(ESTagId tag)
        {
            ushort value = tag.Value;
            if (value == 0 || value >= MaxTagCount)
                return false;

            bits |= 1UL << value;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Remove(ESTagId tag)
        {
            ushort value = tag.Value;
            if (value == 0 || value >= MaxTagCount)
                return false;

            bits &= ~(1UL << value);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(ESTagId tag)
        {
            ushort value = tag.Value;
            return value != 0
                   && value < MaxTagCount
                   && (bits & (1UL << value)) != 0UL;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Overlaps(ESTagMask64 other)
        {
            return (bits & other.bits) != 0UL;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ContainsAll(ESTagMask64 other)
        {
            return (bits & other.bits) == other.bits;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ulong GetBits()
        {
            return bits;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void AddRaw64(ushort value)
        {
            bits |= 1UL << value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RemoveRaw64(ushort value)
        {
            bits &= ~(1UL << value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool ContainsRaw64(ushort value)
        {
            return (bits & (1UL << value)) != 0UL;
        }

        public static ESTagMask64 From(ESTagId tag)
        {
            ESTagMask64 mask = default;
            mask.Add(tag);
            return mask;
        }

        public static ESTagMask64 From(ESTagId tag0, ESTagId tag1)
        {
            ESTagMask64 mask = default;
            mask.Add(tag0);
            mask.Add(tag1);
            return mask;
        }

        public static ESTagMask64 From(ESTagId tag0, ESTagId tag1, ESTagId tag2)
        {
            ESTagMask64 mask = default;
            mask.Add(tag0);
            mask.Add(tag1);
            mask.Add(tag2);
            return mask;
        }

        public static ESTagMask64 From(ESTagId tag0, ESTagId tag1, ESTagId tag2, ESTagId tag3)
        {
            ESTagMask64 mask = default;
            mask.Add(tag0);
            mask.Add(tag1);
            mask.Add(tag2);
            mask.Add(tag3);
            return mask;
        }
    }

    [Serializable]
    public struct ESTagMask256
    {
        public const int MaxTagCount = 256;

        [SerializeField] private ulong bucket0;
        [SerializeField] private ulong bucket1;
        [SerializeField] private ulong bucket2;
        [SerializeField] private ulong bucket3;

        public bool IsEmpty
        {
            get { return (bucket0 | bucket1 | bucket2 | bucket3) == 0UL; }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            bucket0 = 0UL;
            bucket1 = 0UL;
            bucket2 = 0UL;
            bucket3 = 0UL;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Add(ESTagId tag)
        {
            ushort value = tag.Value;
            if (value == 0 || value >= 256)
                return false;

            ulong bit = 1UL << (value & 63);
            switch (value >> 6)
            {
                case 0:
                    bucket0 |= bit;
                    return true;
                case 1:
                    bucket1 |= bit;
                    return true;
                case 2:
                    bucket2 |= bit;
                    return true;
                case 3:
                    bucket3 |= bit;
                    return true;
                default:
                    return false;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Remove(ESTagId tag)
        {
            ushort value = tag.Value;
            if (value == 0 || value >= 256)
                return false;

            ulong bit = ~(1UL << (value & 63));
            switch (value >> 6)
            {
                case 0:
                    bucket0 &= bit;
                    return true;
                case 1:
                    bucket1 &= bit;
                    return true;
                case 2:
                    bucket2 &= bit;
                    return true;
                case 3:
                    bucket3 &= bit;
                    return true;
                default:
                    return false;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(ESTagId tag)
        {
            ushort value = tag.Value;
            if (value == 0 || value >= 256)
                return false;

            ulong bit = 1UL << (value & 63);
            switch (value >> 6)
            {
                case 0:
                    return (bucket0 & bit) != 0UL;
                case 1:
                    return (bucket1 & bit) != 0UL;
                case 2:
                    return (bucket2 & bit) != 0UL;
                case 3:
                    return (bucket3 & bit) != 0UL;
                default:
                    return false;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Overlaps(ESTagMask256 other)
        {
            return ((bucket0 & other.bucket0)
                    | (bucket1 & other.bucket1)
                    | (bucket2 & other.bucket2)
                    | (bucket3 & other.bucket3)) != 0UL;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ContainsAll(ESTagMask256 other)
        {
            return (bucket0 & other.bucket0) == other.bucket0
                   && (bucket1 & other.bucket1) == other.bucket1
                   && (bucket2 & other.bucket2) == other.bucket2
                   && (bucket3 & other.bucket3) == other.bucket3;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ulong GetBucket(int index)
        {
            switch (index)
            {
                case 0:
                    return bucket0;
                case 1:
                    return bucket1;
                case 2:
                    return bucket2;
                case 3:
                    return bucket3;
                default:
                    return 0UL;
            }
        }

        public static ESTagMask256 From(ESTagId tag)
        {
            ESTagMask256 mask = default;
            mask.Add(tag);
            return mask;
        }

        public static ESTagMask256 From(ESTagId tag0, ESTagId tag1)
        {
            ESTagMask256 mask = default;
            mask.Add(tag0);
            mask.Add(tag1);
            return mask;
        }

        public static ESTagMask256 From(ESTagId tag0, ESTagId tag1, ESTagId tag2)
        {
            ESTagMask256 mask = default;
            mask.Add(tag0);
            mask.Add(tag1);
            mask.Add(tag2);
            return mask;
        }

        public static ESTagMask256 From(ESTagId tag0, ESTagId tag1, ESTagId tag2, ESTagId tag3)
        {
            ESTagMask256 mask = default;
            mask.Add(tag0);
            mask.Add(tag1);
            mask.Add(tag2);
            mask.Add(tag3);
            return mask;
        }
    }

    [Serializable]
    public sealed class ESTagSet
    {
        [SerializeField]
        private ulong[] buckets;
        [NonSerialized] private ESTagBakeTable bakeTable;
        [NonSerialized] private Dictionary<string, ESTagId> cachedStringIds;

        public ESTagSet()
        {
            Warmup(ESTagMask32.MaxTagCount);
        }

        public ESTagSet(int maxTags)
        {
            Warmup(maxTags);
        }

        public ESTagSet(ESTagMaskLevel level)
        {
            Warmup(ESTagMaskLevelUtility.GetRuntimeCapacity(level));
        }

        public int Capacity
        {
            get { return buckets != null ? buckets.Length << 6 : 0; }
        }

        public void BindBakeTable(ESTagBakeTable table, int stringCacheCapacity = 16)
        {
            bakeTable = table;
            if (bakeTable != null)
                bakeTable.Warmup();

            if (stringCacheCapacity > 0 && cachedStringIds == null)
                cachedStringIds = new Dictionary<string, ESTagId>(stringCacheCapacity);
        }

        public void Warmup(int maxTags)
        {
            int bucketCount = maxTags <= 0 ? 0 : ((maxTags + 63) >> 6);
            if (bucketCount <= 0)
                return;

            if (buckets == null || buckets.Length < bucketCount)
                buckets = new ulong[bucketCount];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            if (buckets != null)
                Array.Clear(buckets, 0, buckets.Length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Add(ESTagId tag)
        {
            ushort value = tag.Value;
            if (value == 0 || buckets == null)
                return false;

            int bucketIndex = value >> 6;
            if ((uint)bucketIndex >= (uint)buckets.Length)
                return false;

            buckets[bucketIndex] |= 1UL << (value & 63);
            return true;
        }

        public bool AddTag(string key)
        {
            return TryResolveCachedStringId(key, out ESTagId tag) && Add(tag);
        }

        public bool TryBakeTag(string key, out ESTagId tag)
        {
            return TryResolveCachedStringId(key, out tag);
        }

        public ESTagId BakeTagOrInvalid(string key)
        {
            return TryResolveCachedStringId(key, out ESTagId tag) ? tag : ESTagId.Invalid;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Remove(ESTagId tag)
        {
            ushort value = tag.Value;
            if (value == 0 || buckets == null)
                return false;

            int bucketIndex = value >> 6;
            if ((uint)bucketIndex >= (uint)buckets.Length)
                return false;

            buckets[bucketIndex] &= ~(1UL << (value & 63));
            return true;
        }

        public bool RemoveTag(string key)
        {
            return TryResolveCachedStringId(key, out ESTagId tag) && Remove(tag);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Has(ESTagId tag)
        {
            ushort value = tag.Value;
            if (value == 0 || buckets == null)
                return false;

            int bucketIndex = value >> 6;
            return (uint)bucketIndex < (uint)buckets.Length
                   && (buckets[bucketIndex] & (1UL << (value & 63))) != 0UL;
        }

        public bool HasTag(string key)
        {
            return TryResolveCachedStringId(key, out ESTagId tag) && Has(tag);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Overlaps(ESTagMask32 mask)
        {
            return buckets != null
                   && buckets.Length > 0
                   && ((uint)buckets[0] & mask.GetBits()) != 0U;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasAll(ESTagMask32 mask)
        {
            if (buckets == null || buckets.Length == 0)
                return mask.IsEmpty;

            uint required = mask.GetBits();
            return ((uint)buckets[0] & required) == required;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Overlaps(ESTagMask64 mask)
        {
            return buckets != null
                   && buckets.Length > 0
                   && (buckets[0] & mask.GetBits()) != 0UL;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasAll(ESTagMask64 mask)
        {
            if (buckets == null || buckets.Length == 0)
                return mask.IsEmpty;

            ulong required = mask.GetBits();
            return (buckets[0] & required) == required;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Overlaps(ESTagMask256 mask)
        {
            if (buckets == null || buckets.Length == 0)
                return false;

            int max = buckets.Length < 4 ? buckets.Length : 4;
            for (int i = 0; i < max; i++)
            {
                if ((buckets[i] & mask.GetBucket(i)) != 0UL)
                    return true;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasAll(ESTagMask256 mask)
        {
            if (buckets == null || buckets.Length == 0)
                return mask.IsEmpty;

            int max = buckets.Length < 4 ? buckets.Length : 4;
            for (int i = 0; i < max; i++)
            {
                ulong required = mask.GetBucket(i);
                if ((buckets[i] & required) != required)
                    return false;
            }

            for (int i = max; i < 4; i++)
            {
                if (mask.GetBucket(i) != 0UL)
                    return false;
            }

            return true;
        }

        private bool TryResolveCachedStringId(string key, out ESTagId tag)
        {
            if (string.IsNullOrEmpty(key) || bakeTable == null)
            {
                tag = ESTagId.Invalid;
                return false;
            }

            if (cachedStringIds != null && cachedStringIds.TryGetValue(key, out tag))
                return tag.IsValid;

            if (!bakeTable.TryGetId(key, out tag))
                return false;

            if (cachedStringIds != null)
                cachedStringIds[key] = tag;

            return tag.IsValid;
        }
    }

    [Serializable]
    public struct ESTagSet32
    {
        [SerializeField]
        private ESTagMask32 mask;

        public bool IsEmpty
        {
            get { return mask.IsEmpty; }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            mask.Clear();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Add(ESTagId tag)
        {
            return mask.Add(tag);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Remove(ESTagId tag)
        {
            return mask.Remove(tag);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Has(ESTagId tag)
        {
            return mask.Contains(tag);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Overlaps(ESTagMask32 other)
        {
            return mask.Overlaps(other);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasAll(ESTagMask32 other)
        {
            return mask.ContainsAll(other);
        }
    }

    [Serializable]
    public struct ESTagSet64
    {
        [SerializeField]
        private ESTagMask64 mask;

        public bool IsEmpty
        {
            get { return mask.IsEmpty; }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            mask.Clear();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Add(ESTagId tag)
        {
            return mask.Add(tag);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Remove(ESTagId tag)
        {
            return mask.Remove(tag);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Has(ESTagId tag)
        {
            return mask.Contains(tag);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Overlaps(ESTagMask64 other)
        {
            return mask.Overlaps(other);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasAll(ESTagMask64 other)
        {
            return mask.ContainsAll(other);
        }
    }

    [Serializable]
    public struct ESTagSet256
    {
        [SerializeField]
        private ESTagMask256 mask;

        public bool IsEmpty
        {
            get { return mask.IsEmpty; }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            mask.Clear();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Add(ESTagId tag)
        {
            return mask.Add(tag);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Remove(ESTagId tag)
        {
            return mask.Remove(tag);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Has(ESTagId tag)
        {
            return mask.Contains(tag);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Overlaps(ESTagMask256 other)
        {
            return mask.Overlaps(other);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasAll(ESTagMask256 other)
        {
            return mask.ContainsAll(other);
        }
    }

    [Serializable]
    public struct ESTagRefCountSet32
    {
        [SerializeField] private ESTagMask32 active;
        [SerializeField] private byte[] counts;

        public void Warmup()
        {
            if (counts == null || counts.Length < ESTagMask32.MaxTagCount)
                counts = new byte[ESTagMask32.MaxTagCount];
        }

        public bool Add(ESTagId tag)
        {
            ushort value = tag.Value;
            if (value == 0 || value >= ESTagMask32.MaxTagCount)
                return false;

            Warmup();
            if (counts[value] != byte.MaxValue)
                counts[value]++;

            return active.Add(tag);
        }

        public bool Remove(ESTagId tag)
        {
            ushort value = tag.Value;
            if (value == 0 || value >= ESTagMask32.MaxTagCount || counts == null)
                return false;

            if (counts[value] == 0)
                return false;

            counts[value]--;
            if (counts[value] == 0)
                active.Remove(tag);

            return true;
        }

        public bool RemoveAll(ESTagId tag)
        {
            return SetCount(tag, 0);
        }

        public bool SetCount(ESTagId tag, byte count)
        {
            ushort value = tag.Value;
            if (value == 0 || value >= ESTagMask32.MaxTagCount)
                return false;

            Warmup();
            counts[value] = count;
            if (count > 0)
                active.Add(tag);
            else
                active.Remove(tag);

            return true;
        }

        public bool Has(ESTagId tag)
        {
            return active.Contains(tag);
        }

        public byte GetCount(ESTagId tag)
        {
            ushort value = tag.Value;
            if (value == 0 || value >= ESTagMask32.MaxTagCount || counts == null)
                return 0;

            return counts[value];
        }

        public void Clear()
        {
            active.Clear();
            if (counts != null)
                Array.Clear(counts, 0, counts.Length);
        }

        public bool Overlaps(ESTagMask32 mask)
        {
            return active.Overlaps(mask);
        }

        public bool HasAll(ESTagMask32 mask)
        {
            return active.ContainsAll(mask);
        }
    }

    [Serializable]
    public struct ESTagRefCountSet64
    {
        [SerializeField] private ESTagMask64 active;
        [SerializeField] private byte[] counts;

        public void Warmup()
        {
            if (counts == null || counts.Length < ESTagMask64.MaxTagCount)
                counts = new byte[ESTagMask64.MaxTagCount];
        }

        public bool Add(ESTagId tag)
        {
            ushort value = tag.Value;
            if (value == 0 || value >= ESTagMask64.MaxTagCount)
                return false;

            Warmup();
            if (counts[value] != byte.MaxValue)
                counts[value]++;

            return active.Add(tag);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Add(ESGameTag tag)
        {
            ushort value = (ushort)tag;
            if (value == 0 || value >= ESTagMask64.MaxTagCount)
                return false;

            Warmup();
            if (counts[value] != byte.MaxValue)
                counts[value]++;

            active.AddRaw64(value);
            return true;
        }

        public bool Remove(ESTagId tag)
        {
            ushort value = tag.Value;
            if (value == 0 || value >= ESTagMask64.MaxTagCount || counts == null)
                return false;

            if (counts[value] == 0)
                return false;

            counts[value]--;
            if (counts[value] == 0)
                active.Remove(tag);

            return true;
        }

        public bool RemoveAll(ESTagId tag)
        {
            return SetCount(tag, 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Remove(ESGameTag tag)
        {
            ushort value = (ushort)tag;
            if (value == 0 || value >= ESTagMask64.MaxTagCount || counts == null)
                return false;

            if (counts[value] == 0)
                return false;

            counts[value]--;
            if (counts[value] == 0)
                active.RemoveRaw64(value);

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool RemoveAll(ESGameTag tag)
        {
            return SetCount(tag, 0);
        }

        public bool SetCount(ESTagId tag, byte count)
        {
            ushort value = tag.Value;
            if (value == 0 || value >= ESTagMask64.MaxTagCount)
                return false;

            Warmup();
            counts[value] = count;
            if (count > 0)
                active.Add(tag);
            else
                active.Remove(tag);

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool SetCount(ESGameTag tag, byte count)
        {
            ushort value = (ushort)tag;
            if (value == 0 || value >= ESTagMask64.MaxTagCount)
                return false;

            Warmup();
            counts[value] = count;
            if (count > 0)
                active.AddRaw64(value);
            else
                active.RemoveRaw64(value);

            return true;
        }

        public bool Has(ESTagId tag)
        {
            return active.Contains(tag);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Has(ESGameTag tag)
        {
            ushort value = (ushort)tag;
            return value != 0
                   && value < ESTagMask64.MaxTagCount
                   && active.ContainsRaw64(value);
        }

        public byte GetCount(ESTagId tag)
        {
            ushort value = tag.Value;
            if (value == 0 || value >= ESTagMask64.MaxTagCount || counts == null)
                return 0;

            return counts[value];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte GetCount(ESGameTag tag)
        {
            ushort value = (ushort)tag;
            if (value == 0 || value >= ESTagMask64.MaxTagCount || counts == null)
                return 0;

            return counts[value];
        }

        public void Clear()
        {
            active.Clear();
            if (counts != null)
                Array.Clear(counts, 0, counts.Length);
        }

        public bool Overlaps(ESTagMask64 mask)
        {
            return active.Overlaps(mask);
        }

        public bool HasAll(ESTagMask64 mask)
        {
            return active.ContainsAll(mask);
        }
    }

    [Serializable]
    public struct ESTagRefCountSet256
    {
        [SerializeField] private ESTagMask256 active;
        [SerializeField] private byte[] counts;

        public void Warmup()
        {
            if (counts == null || counts.Length < ESTagMask256.MaxTagCount)
                counts = new byte[ESTagMask256.MaxTagCount];
        }

        public bool Add(ESTagId tag)
        {
            ushort value = tag.Value;
            if (value == 0 || value >= ESTagMask256.MaxTagCount)
                return false;

            Warmup();
            if (counts[value] != byte.MaxValue)
                counts[value]++;

            return active.Add(tag);
        }

        public bool Remove(ESTagId tag)
        {
            ushort value = tag.Value;
            if (value == 0 || value >= ESTagMask256.MaxTagCount || counts == null)
                return false;

            if (counts[value] == 0)
                return false;

            counts[value]--;
            if (counts[value] == 0)
                active.Remove(tag);

            return true;
        }

        public bool RemoveAll(ESTagId tag)
        {
            return SetCount(tag, 0);
        }

        public bool SetCount(ESTagId tag, byte count)
        {
            ushort value = tag.Value;
            if (value == 0 || value >= ESTagMask256.MaxTagCount)
                return false;

            Warmup();
            counts[value] = count;
            if (count > 0)
                active.Add(tag);
            else
                active.Remove(tag);

            return true;
        }

        public bool Has(ESTagId tag)
        {
            return active.Contains(tag);
        }

        public byte GetCount(ESTagId tag)
        {
            ushort value = tag.Value;
            if (value == 0 || value >= ESTagMask256.MaxTagCount || counts == null)
                return 0;

            return counts[value];
        }

        public void Clear()
        {
            active.Clear();
            if (counts != null)
                Array.Clear(counts, 0, counts.Length);
        }

        public bool Overlaps(ESTagMask256 mask)
        {
            return active.Overlaps(mask);
        }

        public bool HasAll(ESTagMask256 mask)
        {
            return active.ContainsAll(mask);
        }
    }

    [CreateAssetMenu(menuName = "ES/Tag/Tag Bake Table", fileName = "ESTagBakeTable")]
    public sealed class ESTagBakeTable : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
#if UNITY_EDITOR
            [LabelText("Tag Key")]
#endif
            public string key;

#if UNITY_EDITOR
            [LabelText("Enum Id")]
#endif
            public ushort enumValue;

#if UNITY_EDITOR
            [LabelText("Baked Id")]
#endif
            public ushort bakedId;
        }

#if UNITY_EDITOR
        [LabelText("Tag Entries")]
#endif
        [SerializeField]
        private List<Entry> entries = new List<Entry>(64);

#if UNITY_EDITOR
        [LabelText("Mask Level")]
#endif
        [SerializeField]
        private ESTagMaskLevel maskLevel = ESTagMaskLevel.Mask64;

#if UNITY_EDITOR
        [LabelText("String Start Id")]
#endif
        [SerializeField]
        private ushort stringStartId = ESTagIdRange.StringDefaultStart;

#if UNITY_EDITOR
        [LabelText("String End Id")]
#endif
        [SerializeField]
        private ushort stringEndId = ESTagIdRange.StringDefaultEnd;

        [NonSerialized] private Dictionary<string, ESTagId> keyToId;
        [NonSerialized] private bool cacheReady;

        public IReadOnlyList<Entry> Entries
        {
            get { return entries; }
        }

        public int Count
        {
            get { return entries != null ? entries.Count : 0; }
        }

        public ESTagMaskLevel MaskLevel
        {
            get { return maskLevel; }
        }

        public int RuntimeCapacity
        {
            get { return ESTagMaskLevelUtility.GetRuntimeCapacity(maskLevel); }
        }

        public void Warmup()
        {
            BuildRuntimeCache();
        }

        public void BuildRuntimeCache()
        {
            int count = entries != null ? entries.Count : 0;
            keyToId = new Dictionary<string, ESTagId>(count);
            HashSet<ushort> usedIds = new HashSet<ushort>();

            for (int i = 0; i < count; i++)
            {
                Entry entry = entries[i];
                if (string.IsNullOrEmpty(entry.key) || entry.bakedId == ESTagId.InvalidValue)
                    continue;

                if (!usedIds.Add(entry.bakedId))
                {
#if UNITY_EDITOR
                    Debug.LogWarning("[ESTagBakeTable] Duplicate tag id: " + entry.bakedId + ". Please rebake or adjust enum ids.");
#endif
                    continue;
                }

                keyToId[entry.key] = new ESTagId(entry.bakedId);
            }

            cacheReady = true;
        }

        public bool TryGetId(string key, out ESTagId id)
        {
            if (!cacheReady)
                BuildRuntimeCache();

            if (keyToId != null && key != null && keyToId.TryGetValue(key, out id))
                return true;

            id = ESTagId.Invalid;
            return false;
        }

        public bool TryBakeTag(string key, out ESTagId id)
        {
            return TryGetId(key, out id);
        }

        public ESTagId BakeTagOrInvalid(string key)
        {
            return TryGetId(key, out ESTagId id) ? id : ESTagId.Invalid;
        }

        public bool TryAddToMask(string key, ref ESTagMask32 mask)
        {
            if (!TryGetId(key, out ESTagId id))
                return false;

            return mask.Add(id);
        }

        public bool TryAddToMask(string key, ref ESTagMask64 mask)
        {
            if (!TryGetId(key, out ESTagId id))
                return false;

            return mask.Add(id);
        }

        public bool TryAddToMask(string key, ref ESTagMask256 mask)
        {
            if (!TryGetId(key, out ESTagId id))
                return false;

            return mask.Add(id);
        }

        public bool TryGetMask32(string key, out ESTagMask32 mask)
        {
            mask = default;
            return TryAddToMask(key, ref mask);
        }

        public bool TryGetMask64(string key, out ESTagMask64 mask)
        {
            mask = default;
            return TryAddToMask(key, ref mask);
        }

        public bool TryGetMask256(string key, out ESTagMask256 mask)
        {
            mask = default;
            return TryAddToMask(key, ref mask);
        }

        public bool TryHasKey(ESTagSet32 set, string key)
        {
            return TryGetId(key, out ESTagId id) && set.Has(id);
        }

        public bool TryHasKey(ESTagSet64 set, string key)
        {
            return TryGetId(key, out ESTagId id) && set.Has(id);
        }

        public bool TryHasKey(ESTagSet256 set, string key)
        {
            return TryGetId(key, out ESTagId id) && set.Has(id);
        }

        public bool TryHasKey(ESTagSet set, string key)
        {
            return TryGetId(key, out ESTagId id) && set.Has(id);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ESTagId GetEnumId(ushort enumValue)
        {
            return ESTagId.FromValue(enumValue);
        }


#if UNITY_EDITOR
        [Button("Bake Tag Ids")]
        private void EditorBakeIds()
        {
            EditorApplyMaskLevelRange();

            if (entries == null)
                entries = new List<Entry>(64);

            ushort minStringId = stringStartId < ESTagIdRange.EnumStart
                ? ESTagIdRange.EnumStart
                : stringStartId;
            ushort maxStringId = stringEndId < minStringId
                ? minStringId
                : stringEndId;
            int nextStringId = maxStringId;

            HashSet<ushort> enumIds = new HashSet<ushort>();
            for (int i = 0; i < entries.Count; i++)
            {
                Entry entry = entries[i];
                if (entry.enumValue != ESTagId.InvalidValue)
                    enumIds.Add(entry.enumValue);
            }

            for (int i = 0; i < entries.Count; i++)
            {
                Entry entry = entries[i];
                if (entry.enumValue != ESTagId.InvalidValue)
                {
                    entry.bakedId = entry.enumValue;
                }
                else if (nextStringId >= minStringId)
                {
                    while (nextStringId >= minStringId && enumIds.Contains((ushort)nextStringId))
                        nextStringId--;

                    if (nextStringId < minStringId)
                    {
                        entry.bakedId = ESTagId.InvalidValue;
#if UNITY_EDITOR
                        Debug.LogWarning("[ESTagBakeTable] String tag id range is exhausted by enum/string tags. Expand range or split table.");
#endif
                        entries[i] = entry;
                        continue;
                    }

                    entry.bakedId = (ushort)nextStringId;
                    nextStringId--;
                }
                else
                {
                    entry.bakedId = ESTagId.InvalidValue;
#if UNITY_EDITOR
                    Debug.LogWarning("[ESTagBakeTable] String tag id range is exhausted. Expand range or split table.");
#endif
                }

                entries[i] = entry;
            }

            cacheReady = false;
        }

        [Button("Apply Mask Level")]
        private void EditorApplyMaskLevelRange()
        {
            stringStartId = ESTagIdRange.EnumStart;
            switch (maskLevel)
            {
                case ESTagMaskLevel.Mask32:
                    stringEndId = ESTagIdRange.Mask32End;
                    break;
                case ESTagMaskLevel.Mask64:
                    stringEndId = ESTagIdRange.Mask64End;
                    break;
                case ESTagMaskLevel.Mask256:
                    stringEndId = ESTagIdRange.Mask256End;
                    break;
                default:
                    stringEndId = ESTagIdRange.Mask32End;
                    break;
            }
        }

        [Button("Upgrade To 64")]
        private void EditorUpgradeTo64()
        {
            maskLevel = ESTagMaskLevel.Mask64;
            EditorBakeIds();
        }

        [Button("Upgrade To 256")]
        private void EditorUpgradeTo256()
        {
            maskLevel = ESTagMaskLevel.Mask256;
            EditorBakeIds();
        }
#endif
    }
}
