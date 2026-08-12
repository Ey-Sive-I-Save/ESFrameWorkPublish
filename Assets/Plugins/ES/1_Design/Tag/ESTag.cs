using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Serialization;

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

    /// <summary>
    /// Fixed Enum authoring groups. They are stable identities only; neither group implies a
    /// runtime storage tier. StringKey declarations may be used with either storage tier too.
    /// </summary>
    public enum ESTagEnumGroup : byte
    {
        Primary = 0,
        Optional = 1
    }

    /// <summary>
    /// Entity runtime storage policy. This is deliberately independent from EnumKey/StringKey.
    /// </summary>
    public enum ESTagStorageTier : byte
    {
        HotSlot = 0,
        Sparse = 1
    }

    [Obsolete("Use ESTagStorageTier. Category is a read-only migration view and must not drive new logic.")]
    public enum ESTagCatalogCategory : byte
    {
        Core = 0,
        Extension = 1
    }

    /// <summary>Declares whether a Catalog tag may participate in runtime facts and queries.</summary>
    public enum ESTagAvailability : byte
    {
        Runtime = 0,
        EditorOnly = 1,
        Deprecated = 2
    }

    /// <summary>
    /// Declares which stable external representations may contain a Tag. RuntimeKey values never
    /// leave the current process; save and network snapshots use only EnumKey/StringKey plus the
    /// Catalog SchemaHash.
    /// </summary>
    [Flags]
    public enum ESTagStableTransferScope : byte
    {
        None = 0,
        SaveGame = 1 << 0,
        Network = 1 << 1
    }

    /// <summary>
    /// A stable Tag selector used by authored conditions, grants, snapshots, and Catalog lookup.
    /// It may carry an EnumKey, a StringKey, or both aliases for the same declaration.
    /// </summary>
    [Serializable]
    public struct ESTagStableReference : IEquatable<ESTagStableReference>
    {
        public ESTagEnumGroup enumGroup;
        public ushort enumValue;
        public string stringKey;

        public bool HasEnumKey => enumValue != ESTagId.InvalidValue;
        public bool HasStringKey => !string.IsNullOrWhiteSpace(stringKey);
        public bool IsEmpty => !HasEnumKey && !HasStringKey;

        public static ESTagStableReference From(ESGameTag tag)
        {
            return new ESTagStableReference
            {
                enumGroup = ESTagEnumGroup.Primary,
                enumValue = (ushort)tag
            };
        }

        public static ESTagStableReference From(ESGameTagOptional tag)
        {
            return new ESTagStableReference
            {
                enumGroup = ESTagEnumGroup.Optional,
                enumValue = (ushort)tag
            };
        }

        public static ESTagStableReference FromString(string key)
        {
            return new ESTagStableReference { stringKey = key };
        }

        public bool Equals(ESTagStableReference other)
        {
            return enumGroup == other.enumGroup
                   && enumValue == other.enumValue
                   && string.Equals(stringKey, other.stringKey, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is ESTagStableReference other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)enumGroup;
                hash = (hash * 397) ^ enumValue.GetHashCode();
                hash = (hash * 397) ^ (stringKey != null ? StringComparer.Ordinal.GetHashCode(stringKey) : 0);
                return hash;
            }
        }

        public override string ToString()
        {
            if (HasEnumKey && HasStringKey)
                return enumGroup + ":" + enumValue + " / " + stringKey;
            if (HasEnumKey)
                return enumGroup + ":" + enumValue;
            return stringKey ?? string.Empty;
        }
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
        public const ushort CoreRuntimeEnd = 63;
        public const ushort ExtensionRuntimeStart = 64;
        public const ushort StringDefaultStart = ExtensionRuntimeStart;
        // Sparse RuntimeKeys are Entity dictionary keys, not mask bits. Their catalog capacity is
        // therefore independent from Mask256's explicit-query capacity.
        public const ushort StringDefaultEnd = MaxValue;
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

        /// <summary>为核心 GameTag 构造 Mask。保留位和 None 不可加入。</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Add(ESGameTag tag)
        {
            if (!ESGameTagCatalog.IsDefinedCore(tag))
                return false;

            bits |= 1U << (ushort)tag;
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
        public bool Remove(ESGameTag tag)
        {
            if (!ESGameTagCatalog.IsDefinedCore(tag))
                return false;

            bits &= ~(1U << (ushort)tag);
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

        /// <summary>Read-only diagnostic view of the active mask bits.</summary>
        public ulong Bits
        {
            get { return bits; }
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

        /// <summary>为核心 GameTag 构造 Mask。保留位和 None 不可加入。</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Add(ESGameTag tag)
        {
            if (!ESGameTagCatalog.IsDefinedCore(tag))
                return false;

            bits |= 1UL << (ushort)tag;
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
        public bool Remove(ESGameTag tag)
        {
            if (!ESGameTagCatalog.IsDefinedCore(tag))
                return false;

            bits &= ~(1UL << (ushort)tag);
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
        public bool Contains(ESGameTag tag)
        {
            return ESGameTagCatalog.IsDefinedCore(tag)
                   && (bits & (1UL << (ushort)tag)) != 0UL;
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

        public static ESTagMask64 From(ESGameTag tag)
        {
            ESTagMask64 mask = default;
            mask.Add(tag);
            return mask;
        }

        public static ESTagMask64 From(ESGameTag tag0, ESGameTag tag1)
        {
            ESTagMask64 mask = default;
            mask.Add(tag0);
            mask.Add(tag1);
            return mask;
        }

        public static ESTagMask64 From(ESGameTag tag0, ESGameTag tag1, ESGameTag tag2)
        {
            ESTagMask64 mask = default;
            mask.Add(tag0);
            mask.Add(tag1);
            mask.Add(tag2);
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

    /// <summary>
    /// 可序列化的核心 GameTag 条件：必须全部满足、至少满足一个、禁止任意命中。
    /// 仅做位运算，可用于技能、AI、交互、命中等配置入口。
    /// </summary>
    [Serializable]
    public struct ESGameTagRequirement
    {
        [Tooltip("目标必须同时拥有的标签。")]
        public ESTagMask64 requiredAll;

        [Tooltip("目标至少拥有其中一个标签；为空时不限制。")]
        public ESTagMask64 requiredAny;

        [Tooltip("目标拥有任意一个时，条件失败。")]
        public ESTagMask64 blockedAny;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Matches(ESTagMask64 tags)
        {
            if (!tags.ContainsAll(requiredAll))
                return false;
            if (!requiredAny.IsEmpty && !tags.Overlaps(requiredAny))
                return false;

            return blockedAny.IsEmpty || !tags.Overlaps(blockedAny);
        }
    }

    /// <summary>
    /// 面向配置的 GameTag 条件。Inspector 使用明确的枚举列表，运行时通过
    /// <see cref="TryCompile"/> 编译为 <see cref="ESGameTagRequirement"/> 的无分配位掩码。
    /// <para>配置阶段禁止 None、保留位、废弃身份/阵营位、重复项和自相矛盾条件。</para>
    /// </summary>
    [Serializable]
    public sealed class ESGameTagRequirementConfig
    {
        [Tooltip("目标必须同时拥有的标签；为空时不限制。")]
        public List<ESGameTag> requiredAll = new List<ESGameTag>();

        [Tooltip("目标至少拥有其中一个标签；为空时不限制。")]
        public List<ESGameTag> requiredAny = new List<ESGameTag>();

        [Tooltip("目标拥有任意一个时，条件失败；为空时不限制。")]
        public List<ESGameTag> blockedAny = new List<ESGameTag>();

        public bool IsEmpty
        {
            get
            {
                return (requiredAll == null || requiredAll.Count == 0)
                       && (requiredAny == null || requiredAny.Count == 0)
                       && (blockedAny == null || blockedAny.Count == 0);
            }
        }

        /// <summary>
        /// 将编辑器友好列表编译为运行时条件。失败时必须将该配置视为不可用，不能退化为“无条件通过”。
        /// </summary>
        public bool TryCompile(out ESGameTagRequirement requirement, out string error)
        {
            requirement = default;
            error = null;

            if (!TryBuildMask(requiredAll, "requiredAll", out requirement.requiredAll, out error)
                || !TryBuildMask(requiredAny, "requiredAny", out requirement.requiredAny, out error)
                || !TryBuildMask(blockedAny, "blockedAny", out requirement.blockedAny, out error))
                return false;

            if (requirement.requiredAll.Overlaps(requirement.blockedAny))
            {
                error = "requiredAll 与 blockedAny 包含同一 Tag，条件永远无法满足。";
                return false;
            }

            if (!requirement.requiredAny.IsEmpty && requirement.requiredAny.Overlaps(requirement.blockedAny)
                && IsEveryAnyTagBlocked(requiredAny, blockedAny))
            {
                error = "requiredAny 中的全部 Tag 都同时存在于 blockedAny，条件永远无法满足。";
                return false;
            }

            return true;
        }

        private static bool TryBuildMask(List<ESGameTag> source, string fieldName, out ESTagMask64 mask, out string error)
        {
            mask = default;
            error = null;
            if (source == null)
                return true;

            for (int i = 0; i < source.Count; i++)
            {
                ESGameTag tag = source[i];
                if (!ESGameTagCatalog.IsUsableInNewConfiguration(tag))
                {
                    error = fieldName + "[" + i + "]=" + tag
                            + " 不是可用于新配置的核心 GameTag（None、保留位和废弃身份/阵营位均不可用）。";
                    return false;
                }

                if (mask.Contains(tag))
                {
                    error = fieldName + " 包含重复 Tag：" + tag + "。";
                    return false;
                }

                mask.Add(tag);
            }

            return true;
        }

        private static bool IsEveryAnyTagBlocked(List<ESGameTag> anyTags, List<ESGameTag> blockedTags)
        {
            if (anyTags == null || anyTags.Count == 0 || blockedTags == null || blockedTags.Count == 0)
                return false;

            var blocked = new HashSet<ESGameTag>(blockedTags);
            for (int i = 0; i < anyTags.Count; i++)
            {
                if (!blocked.Contains(anyTags[i]))
                    return false;
            }

            return true;
        }
    }

    /// <summary>
    /// Baked condition for Entity Tag queries. HotSlot fields remain a branch-free 64-bit path;
    /// sparse RuntimeKeys are consulted only when a sparse condition is present.
    /// </summary>
    [Serializable]
    public struct ESTagConditionRuntime
    {
        public ulong RequiredHotMask;
        public ulong RequiredAnyHotMask;
        public ulong ForbiddenHotMask;
        public int[] RequiredSparse;
        public int[] RequiredAnySparse;
        public int[] ForbiddenSparse;
        public string SchemaHash;
        public string RuntimeLayoutHash;

        // Internal compatibility aliases keep existing consumers on the same hot/sparse runtime
        // representation while authored configuration migrates to unified references.
        public ulong RequiredCoreMask { get => RequiredHotMask; set => RequiredHotMask = value; }
        public ulong RequiredAnyCoreMask { get => RequiredAnyHotMask; set => RequiredAnyHotMask = value; }
        public ulong ForbiddenCoreMask { get => ForbiddenHotMask; set => ForbiddenHotMask = value; }
        public int[] RequiredExtensions { get => RequiredSparse; set => RequiredSparse = value; }
        public int[] RequiredAnyExtensions { get => RequiredAnySparse; set => RequiredAnySparse = value; }
        public int[] ForbiddenExtensions { get => ForbiddenSparse; set => ForbiddenSparse = value; }
        public bool HasExtensionConditions => HasSparseConditions;

        public bool HasSparseConditions
        {
            get
            {
                return (RequiredSparse != null && RequiredSparse.Length > 0)
                       || (RequiredAnySparse != null && RequiredAnySparse.Length > 0)
                       || (ForbiddenSparse != null && ForbiddenSparse.Length > 0);
            }
        }

        public bool IsEmpty
        {
            get
            {
                return RequiredHotMask == 0UL
                       && RequiredAnyHotMask == 0UL
                       && ForbiddenHotMask == 0UL
                       && !HasSparseConditions;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MatchesHot(ulong hotMask)
        {
            return (hotMask & RequiredHotMask) == RequiredHotMask
                   && (RequiredAnyHotMask == 0UL || (hotMask & RequiredAnyHotMask) != 0UL)
                   && (hotMask & ForbiddenHotMask) == 0UL;
        }

        public bool MatchesCore(ulong coreMask) => MatchesHot(coreMask);

        /// <summary>
        /// Extension RuntimeKeys are only meaningful under the Catalog layout that baked them.
        /// Core-only conditions deliberately do not require a bound Catalog.
        /// </summary>
        public bool TryValidateActiveCatalog(out string error)
        {
            error = null;
            if (IsEmpty)
                return true;

            if (!ESTagRuntimeCatalog.IsBound)
            {
                error = "Tag Catalog is not bound; baked Tag conditions cannot be evaluated.";
                return false;
            }

            if (string.IsNullOrEmpty(SchemaHash) || string.IsNullOrEmpty(RuntimeLayoutHash))
            {
                error = "Condition lacks the Catalog SchemaHash or RuntimeLayoutHash required for RuntimeKey evaluation.";
                return false;
            }

            if (!string.Equals(SchemaHash, ESTagRuntimeCatalog.SchemaHash, StringComparison.Ordinal))
            {
                error = "Condition SchemaHash does not match the active Tag Catalog.";
                return false;
            }

            if (!string.Equals(RuntimeLayoutHash, ESTagRuntimeCatalog.RuntimeLayoutHash, StringComparison.Ordinal))
            {
                error = "Condition RuntimeKey layout does not match the active Tag Catalog.";
                return false;
            }

            return true;
        }
    }

    /// <summary>
    /// Editor-facing stable Tag condition. EnumKey and StringKey references are resolved through
    /// the Catalog, then separated into HotSlot and Sparse runtime paths.
    /// </summary>
    [Serializable]
    public sealed class ESTagConditionConfig
    {
        [NonSerialized]
        private bool hasCachedRuntime;

        [NonSerialized]
        private ESTagConditionRuntime cachedRuntime;

        [Tooltip("Tags that must all be present.")]
        public List<ESTagStableReference> required = new List<ESTagStableReference>();

        [Tooltip("At least one of these Tags must be present.")]
        public List<ESTagStableReference> requiredAny = new List<ESTagStableReference>();

        [Tooltip("Tags that must all be absent.")]
        public List<ESTagStableReference> forbidden = new List<ESTagStableReference>();

        [HideInInspector] public List<ESGameTag> requiredCore = new List<ESGameTag>();
        [HideInInspector] public List<ESGameTag> requiredAnyCore = new List<ESGameTag>();
        [HideInInspector] public List<ESGameTag> forbiddenCore = new List<ESGameTag>();
        [HideInInspector] public List<string> requiredExtensions = new List<string>();
        [HideInInspector] public List<string> requiredAnyExtensions = new List<string>();
        [HideInInspector] public List<string> forbiddenExtensions = new List<string>();

        public bool IsEmpty
        {
            get
            {
                return IsNullOrEmpty(required)
                       && IsNullOrEmpty(requiredAny)
                       && IsNullOrEmpty(forbidden);
            }
        }

        /// <summary>
        /// Resolves this stable configuration for the active process. Normal gameplay code should
            /// query through <c>ESTagCollection.Matches(config)</c> instead of handling RuntimeKeys directly.
        /// </summary>
        public bool TryGetRuntime(out ESTagConditionRuntime runtime, out string error)
        {
            if (hasCachedRuntime && cachedRuntime.TryValidateActiveCatalog(out _))
            {
                runtime = cachedRuntime;
                error = null;
                return true;
            }

            hasCachedRuntime = false;
            cachedRuntime = default;
            if (!TryCompile(out runtime, out error))
                return false;

            cachedRuntime = runtime;
            hasCachedRuntime = true;
            return true;
        }

        /// <summary>
        /// Call after changing this configuration at runtime. Serialized asset edits naturally
        /// create a new non-serialized cache when Unity reloads the object.
        /// </summary>
        public void InvalidateRuntime()
        {
            hasCachedRuntime = false;
            cachedRuntime = default;
        }

        public bool TryCompile(out ESTagConditionRuntime runtime, out string error)
        {
            runtime = default;
            error = null;

            MigrateLegacyReferences();

            if (!TryBakeReferences(required, "required", out runtime.RequiredHotMask, out runtime.RequiredSparse, out error)
                || !TryBakeReferences(requiredAny, "requiredAny", out runtime.RequiredAnyHotMask, out runtime.RequiredAnySparse, out error)
                || !TryBakeReferences(forbidden, "forbidden", out runtime.ForbiddenHotMask, out runtime.ForbiddenSparse, out error))
                return false;

            if ((runtime.RequiredHotMask & runtime.ForbiddenHotMask) != 0UL
                || SharesRuntimeKey(runtime.RequiredSparse, runtime.ForbiddenSparse))
            {
                error = "required and forbidden contain the same Tag, so the condition can never match.";
                return false;
            }

            if (runtime.RequiredAnyHotMask != 0UL
                && (runtime.RequiredAnyHotMask & ~runtime.ForbiddenHotMask) == 0UL)
            {
                error = "Every requiredAny HotSlot Tag is also forbidden.";
                return false;
            }

            if (runtime.RequiredAnySparse != null
                && runtime.RequiredAnySparse.Length > 0
                && SharesEveryRuntimeKey(runtime.RequiredAnySparse, runtime.ForbiddenSparse))
            {
                error = "Every requiredAny Sparse Tag is also forbidden.";
                return false;
            }

            if (!runtime.IsEmpty)
            {
                runtime.SchemaHash = ESTagRuntimeCatalog.SchemaHash;
                runtime.RuntimeLayoutHash = ESTagRuntimeCatalog.RuntimeLayoutHash;
            }

            return true;
        }

        private static bool IsNullOrEmpty<T>(List<T> source)
        {
            return source == null || source.Count == 0;
        }

        private void MigrateLegacyReferences()
        {
            AppendLegacy(required, requiredCore, requiredExtensions);
            AppendLegacy(requiredAny, requiredAnyCore, requiredAnyExtensions);
            AppendLegacy(forbidden, forbiddenCore, forbiddenExtensions);
        }

        private static void AppendLegacy(List<ESTagStableReference> target, List<ESGameTag> enumTags, List<string> stringKeys)
        {
            target ??= new List<ESTagStableReference>();
            if (enumTags != null)
            {
                for (int i = 0; i < enumTags.Count; i++)
                {
                    ESTagStableReference reference = ESTagStableReference.From(enumTags[i]);
                    if (!target.Contains(reference))
                        target.Add(reference);
                }
            }

            if (stringKeys != null)
            {
                for (int i = 0; i < stringKeys.Count; i++)
                {
                    ESTagStableReference reference = ESTagStableReference.FromString(stringKeys[i]);
                    if (!target.Contains(reference))
                        target.Add(reference);
                }
            }
        }

        private static bool TryBakeReferences(List<ESTagStableReference> source, string fieldName,
            out ulong hotMask, out int[] sparseKeys, out string error)
        {
            hotMask = 0UL;
            sparseKeys = Array.Empty<int>();
            error = null;
            if (source == null || source.Count == 0)
                return true;

            if (!ESTagRuntimeCatalog.IsBound)
            {
                error = fieldName + " requires an active Tag Catalog before stable identities can be baked.";
                return false;
            }

            var runtimeKeys = new HashSet<int>();
            var sparse = new List<int>(source.Count);
            for (int i = 0; i < source.Count; i++)
            {
                ESTagStableReference reference = source[i];
                if (reference.IsEmpty || !ESTagRuntimeCatalog.TryGetRuntimeKey(reference, out int runtimeKey))
                {
                    error = fieldName + "[" + i + "] is not registered by the active Tag Catalog: " + reference + ".";
                    return false;
                }

                ESTagId tag = ESTagId.FromInt32(runtimeKey);
                if (!ESTagRuntimeCatalog.TryGetEntry(tag, out ESTagBakeTable.Entry entry)
                    || entry.availability != ESTagAvailability.Runtime)
                {
                    error = fieldName + "[" + i + "] is not a runtime-available Tag: " + reference + ".";
                    return false;
                }

                if (!runtimeKeys.Add(runtimeKey))
                {
                    error = fieldName + " contains multiple aliases for RuntimeKey " + runtimeKey + ".";
                    return false;
                }

                if (entry.storageTier == ESTagStorageTier.HotSlot)
                {
                    hotMask |= 1UL << runtimeKey;
                }
                else
                {
                    sparse.Add(runtimeKey);
                }
            }

            sparse.Sort();
            sparseKeys = sparse.ToArray();
            return true;
        }

        private static bool SharesRuntimeKey(int[] required, int[] forbidden)
        {
            if (required == null || forbidden == null || required.Length == 0 || forbidden.Length == 0)
                return false;

            int requiredIndex = 0;
            int forbiddenIndex = 0;
            while (requiredIndex < required.Length && forbiddenIndex < forbidden.Length)
            {
                int requiredKey = required[requiredIndex];
                int forbiddenKey = forbidden[forbiddenIndex];
                if (requiredKey == forbiddenKey)
                    return true;

                if (requiredKey < forbiddenKey)
                    requiredIndex++;
                else
                    forbiddenIndex++;
            }

            return false;
        }

        private static bool SharesEveryRuntimeKey(int[] requiredAny, int[] forbidden)
        {
            if (requiredAny == null || requiredAny.Length == 0 || forbidden == null || forbidden.Length == 0)
                return false;

            int requiredIndex = 0;
            int forbiddenIndex = 0;
            while (requiredIndex < requiredAny.Length && forbiddenIndex < forbidden.Length)
            {
                int requiredKey = requiredAny[requiredIndex];
                int forbiddenKey = forbidden[forbiddenIndex];
                if (requiredKey == forbiddenKey)
                {
                    requiredIndex++;
                    forbiddenIndex++;
                    continue;
                }

                if (requiredKey < forbiddenKey)
                    return false;

                forbiddenIndex++;
            }

            return requiredIndex == requiredAny.Length;
        }
    }

#if UNITY_EDITOR
    /// <summary>Editor-only cached picker source. The project must have one formal Tag BakeTable.</summary>
    public static class ESTagEditorCatalogCache
    {
        public readonly struct PickerEntry
        {
            public readonly ESTagStableReference Reference;
            public readonly string DisplayName;
            public readonly string GroupPath;
            public readonly string StringKey;
            public readonly string StorageBadge;

            internal PickerEntry(
                ESTagStableReference reference,
                string displayName,
                string groupPath,
                string stringKey,
                string storageBadge)
            {
                Reference = reference;
                DisplayName = displayName;
                GroupPath = groupPath;
                StringKey = stringKey;
                StorageBadge = storageBadge;
            }

            public string FullDisplayName
            {
                get
                {
                    string result = DisplayName ?? string.Empty;
                    if (!string.IsNullOrEmpty(StringKey))
                        result += " · " + StringKey;
                    if (!string.IsNullOrEmpty(StorageBadge))
                        result += " · " + StorageBadge;
                    return result;
                }
            }
        }

        private static readonly List<PickerEntry> Empty = new List<PickerEntry>(0);
        private static List<PickerEntry> pickerEntries;
        private static bool dirty = true;

        /// <summary>
        /// Registers the editor invalidation callback through ES AssemblyStream.
        /// The cache itself remains in the Design assembly because editor drawers consume it.
        /// </summary>
        public static void InitializeEditorEvents()
        {
            UnityEditor.EditorApplication.projectChanged -= Invalidate;
            UnityEditor.EditorApplication.projectChanged += Invalidate;
        }

        public static IReadOnlyList<PickerEntry> GetPickerEntries()
        {
            if (dirty)
                Rebuild();

            return pickerEntries ?? Empty;
        }

        public static bool TryGetPickerEntry(ESTagStableReference reference, out PickerEntry result)
        {
            IReadOnlyList<PickerEntry> entries = GetPickerEntries();
            for (int i = 0; i < entries.Count; i++)
            {
                PickerEntry entry = entries[i];
                if (Matches(reference, entry.Reference))
                {
                    result = entry;
                    return true;
                }
            }

            result = default;
            return false;
        }

        public static void Invalidate()
        {
            dirty = true;
        }

        private static void Rebuild()
        {
            dirty = false;
            pickerEntries = new List<PickerEntry>();
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:ESTagBakeTable");
            if (guids == null || guids.Length != 1)
                return;

            string assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
            ESTagBakeTable table = UnityEditor.AssetDatabase.LoadAssetAtPath<ESTagBakeTable>(assetPath);
            if (table == null)
                return;

            IReadOnlyList<ESTagBakeTable.Entry> entries = table.Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                ESTagBakeTable.Entry entry = entries[i];
                if (entry.availability != ESTagAvailability.Runtime)
                    continue;

                var reference = new ESTagStableReference
                {
                    enumGroup = entry.enumGroup,
                    enumValue = entry.enumValue,
                    stringKey = entry.key
                };
                ResolveDisplay(entry, out string displayName, out string groupPath);
                pickerEntries.Add(new PickerEntry(
                    reference,
                    displayName,
                    groupPath,
                    entry.key,
                    entry.storageTier == ESTagStorageTier.HotSlot ? "Hot" : "Sparse"));
            }

            pickerEntries.Sort((left, right) => string.CompareOrdinal(left.FullDisplayName, right.FullDisplayName));
        }

        private static bool Matches(ESTagStableReference value, ESTagStableReference candidate)
        {
            if (value.IsEmpty)
                return false;

            if (value.HasEnumKey && candidate.HasEnumKey
                && value.enumGroup == candidate.enumGroup && value.enumValue == candidate.enumValue)
            {
                return !value.HasStringKey || !candidate.HasStringKey
                       || string.Equals(value.stringKey, candidate.stringKey, StringComparison.Ordinal);
            }

            return value.HasStringKey && candidate.HasStringKey
                   && string.Equals(value.stringKey, candidate.stringKey, StringComparison.Ordinal);
        }

        private static void ResolveDisplay(ESTagBakeTable.Entry entry, out string displayName, out string groupPath)
        {
            if (entry.enumValue == ESTagId.InvalidValue)
            {
                groupPath = "StringKey Tag";
                displayName = entry.key ?? string.Empty;
                return;
            }

            Type enumType = entry.enumGroup == ESTagEnumGroup.Optional
                ? typeof(ESGameTagOptional)
                : typeof(ESGameTag);
            string enumName = Enum.GetName(enumType, entry.enumValue);
            string inspectorName = ResolveInspectorName(enumType, enumName);
            string source = string.IsNullOrEmpty(inspectorName) ? (enumName ?? entry.key) : inspectorName;
            int separator = source.LastIndexOf('/');
            if (separator >= 0)
            {
                groupPath = source.Substring(0, separator);
                displayName = source.Substring(separator + 1);
            }
            else
            {
                groupPath = entry.enumValue != ESTagId.InvalidValue ? "枚举 Tag" : "StringKey Tag";
                displayName = source;
            }
        }

        private static string ResolveInspectorName(Type enumType, string enumName)
        {
            if (enumType == null || string.IsNullOrEmpty(enumName))
                return null;

            FieldInfo field = enumType.GetField(enumName, BindingFlags.Public | BindingFlags.Static);
            InspectorNameAttribute attribute = field?.GetCustomAttribute<InspectorNameAttribute>();
            return attribute?.displayName;
        }
    }
#endif

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

        /// <summary>当前激活标签的只读快照；返回值是值类型，不暴露计数容器。</summary>
        public ESTagMask64 ActiveMask
        {
            get { return active; }
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

    [CreateAssetMenu(menuName = "【ES】/配置/Tag/Tag 烘焙表", fileName = "ESTagBakeTable")]
    public sealed partial class ESTagBakeTable : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
#if UNITY_EDITOR
            [LabelText("Tag Key")]
#endif
            public string key;

#if UNITY_EDITOR
            [LabelText("Enum Group")]
#endif
            public ESTagEnumGroup enumGroup;

#if UNITY_EDITOR
            [LabelText("Enum Key")]
#endif
            public ushort enumValue;

#if UNITY_EDITOR
            [LabelText("Baked Id")]
#endif
            public ushort bakedId;

#if UNITY_EDITOR
            [LabelText("Storage Tier")]
#endif
            [FormerlySerializedAs("category")]
            public ESTagStorageTier storageTier;

            [Obsolete("Use storageTier.")]
            public ESTagCatalogCategory category
            {
                get => storageTier == ESTagStorageTier.HotSlot
                    ? ESTagCatalogCategory.Core
                    : ESTagCatalogCategory.Extension;
                set => storageTier = value == ESTagCatalogCategory.Core
                    ? ESTagStorageTier.HotSlot
                    : ESTagStorageTier.Sparse;
            }

#if UNITY_EDITOR
            [LabelText("Availability")]
#endif
            public ESTagAvailability availability;

#if UNITY_EDITOR
            [LabelText("Deprecated Replacement")]
#endif
            [Tooltip("Only for Deprecated entries. When set, this is the stable Tag identity an explicit data migration may map to.")]
            public ESTagStableReference deprecatedReplacement;

#if UNITY_EDITOR
            [LabelText("Stable Transfer")]
#endif
            public ESTagStableTransferScope stableTransferScopes;
        }

#if UNITY_EDITOR
        [LabelText("Tag Entries（由 GameCore 生成）")]
        [ReadOnly]
#endif
        [SerializeField]
        private List<Entry> entries = new List<Entry>(64);

#if UNITY_EDITOR
        [LabelText("Mask Level")]
#endif
        [SerializeField]
        private ESTagMaskLevel maskLevel = ESTagMaskLevel.Mask256;

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
        [NonSerialized] private Dictionary<ulong, ESTagId> enumToId;
        [NonSerialized] private Dictionary<ushort, Entry> runtimeEntries;
        [NonSerialized] private List<string> validationErrors;
        [NonSerialized] private string schemaHash;
        [NonSerialized] private string runtimeLayoutHash;
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

        /// <summary>Stable-key schema hash for version negotiation. Baked ids remain process-local.</summary>
        public string SchemaHash
        {
            get
            {
                if (!cacheReady)
                    BuildRuntimeCache();
                return schemaHash ?? string.Empty;
            }
        }

        /// <summary>
        /// Process-local layout fingerprint. It includes baked RuntimeKey values and must match
        /// before replacing a bound Catalog in the same process.
        /// </summary>
        public string RuntimeLayoutHash
        {
            get
            {
                if (!cacheReady)
                    BuildRuntimeCache();
                return runtimeLayoutHash ?? string.Empty;
            }
        }

        public void Warmup()
        {
            BuildRuntimeCache();
        }

        public void BuildRuntimeCache()
        {
            int count = entries != null ? entries.Count : 0;
            keyToId = new Dictionary<string, ESTagId>(count);
            enumToId = new Dictionary<ulong, ESTagId>(count);
            runtimeEntries = new Dictionary<ushort, Entry>(count);
            validationErrors = new List<string>(4);
            HashSet<ushort> usedIds = new HashSet<ushort>();

            for (int i = 0; i < count; i++)
            {
                Entry entry = entries[i];
                bool hasString = !string.IsNullOrEmpty(entry.key);
                bool hasEnum = entry.enumValue != ESTagId.InvalidValue;
                if ((!hasString && !hasEnum) || entry.bakedId == ESTagId.InvalidValue)
                {
                    validationErrors.Add("Entry[" + i + "] lacks a stable key or baked runtime id.");
                    continue;
                }

                if (!usedIds.Add(entry.bakedId))
                {
                    validationErrors.Add("Duplicate baked id: " + entry.bakedId + ". Each declaration must own exactly one runtime bit.");
                    continue;
                }

                ESTagId id = new ESTagId(entry.bakedId);
                if (entry.storageTier == ESTagStorageTier.HotSlot)
                {
                    if (entry.bakedId > ESTagIdRange.CoreRuntimeEnd)
                    {
                        validationErrors.Add("HotSlot Entry[" + i + "] RuntimeKey must be in 1-63.");
                        continue;
                    }
                }
                else if (entry.bakedId < ESTagIdRange.ExtensionRuntimeStart)
                {
                    validationErrors.Add("Sparse Entry[" + i + "] RuntimeKey must be outside the 64-bit HotSlot range.");
                    continue;
                }

                if (hasEnum && !IsDefinedEnumKey(entry.enumGroup, entry.enumValue))
                {
                    validationErrors.Add("Entry[" + i + "] references an undefined " + entry.enumGroup + " EnumKey: " + entry.enumValue + ".");
                    continue;
                }

                if (hasEnum && entry.enumGroup == ESTagEnumGroup.Primary)
                {
                    ESTagAvailability expectedAvailability = ESGameTagCatalog.IsUsableInNewConfiguration((ESGameTag)entry.enumValue)
                        ? ESTagAvailability.Runtime
                        : ESTagAvailability.Deprecated;
                    if (entry.availability != expectedAvailability)
                    {
                        validationErrors.Add("Primary Enum Entry[" + i + "] Availability must be " + expectedAvailability + ".");
                        continue;
                    }
                }

                runtimeEntries.Add(entry.bakedId, entry);
                if (hasString)
                {
                    if (keyToId.ContainsKey(entry.key))
                    {
                        validationErrors.Add("Duplicate StringKey: " + entry.key);
                        continue;
                    }
                    keyToId.Add(entry.key, id);
                }

                if (hasEnum)
                {
                    ulong enumKey = GetEnumLookupKey(entry.enumGroup, entry.enumValue);
                    if (enumToId.ContainsKey(enumKey))
                    {
                        validationErrors.Add("Duplicate EnumKey: " + entry.enumGroup + ":" + entry.enumValue);
                        continue;
                    }
                    enumToId.Add(enumKey, id);
                }
            }

            cacheReady = true;
            ValidateDeprecatedMigrations();
            schemaHash = CalculateSchemaHash(false);
            runtimeLayoutHash = CalculateSchemaHash(true);
        }

        public bool TryValidate(out string error)
        {
            if (!cacheReady)
                BuildRuntimeCache();
            if (validationErrors == null || validationErrors.Count == 0)
            {
                error = null;
                return true;
            }

            error = string.Join("\n", validationErrors);
            return false;
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

        public bool TryGetId(ushort enumValue, out ESTagId id)
        {
            return TryGetId(ESTagEnumGroup.Primary, enumValue, out id);
        }

        public bool TryGetId(ESTagEnumGroup enumGroup, ushort enumValue, out ESTagId id)
        {
            if (!cacheReady)
                BuildRuntimeCache();

            ulong enumKey = GetEnumLookupKey(enumGroup, enumValue);
            if (enumToId != null && enumValue != ESTagId.InvalidValue && enumToId.TryGetValue(enumKey, out id))
                return true;

            id = ESTagId.Invalid;
            return false;
        }

        /// <summary>Both aliases must resolve to the same baked runtime bit.</summary>
        public bool TryGetId(ushort enumValue, string key, out ESTagId id)
        {
            return TryGetId(new ESTagStableReference
            {
                enumGroup = ESTagEnumGroup.Primary,
                enumValue = enumValue,
                stringKey = key
            }, out id);
        }

        /// <summary>All supplied aliases must resolve to the same baked RuntimeKey.</summary>
        public bool TryGetId(ESTagStableReference reference, out ESTagId id)
        {
            ESTagId enumId = ESTagId.Invalid;
            ESTagId stringId = ESTagId.Invalid;
            bool declaresEnum = reference.HasEnumKey;
            bool declaresString = reference.HasStringKey;
            bool hasEnum = declaresEnum && TryGetId(reference.enumGroup, reference.enumValue, out enumId);
            bool hasString = declaresString && TryGetId(reference.stringKey, out stringId);
            if ((!declaresEnum && !declaresString)
                || (declaresEnum && !hasEnum)
                || (declaresString && !hasString)
                || (hasEnum && hasString && enumId != stringId))
            {
                id = ESTagId.Invalid;
                return false;
            }

            id = hasEnum ? enumId : stringId;
            return id.IsValid;
        }

        public bool TryGetStableReference(ESTagId id, out ESTagStableReference reference)
        {
            if (!TryGetEntry(id, out Entry entry))
            {
                reference = default;
                return false;
            }

            reference = new ESTagStableReference
            {
                enumGroup = entry.enumGroup,
                enumValue = entry.enumValue,
                stringKey = entry.key
            };
            return true;
        }

        /// <summary>
        /// Resolves a Catalog-declared replacement for an obsolete stable identity. It is an
        /// explicit migration API; normal condition and grant resolution never silently remaps a
        /// deprecated Tag.
        /// </summary>
        public bool TryGetDeprecatedReplacement(ESTagStableReference obsolete, out ESTagStableReference replacement)
        {
            replacement = default;
            if (!TryGetId(obsolete, out ESTagId obsoleteId)
                || !TryGetEntry(obsoleteId, out Entry entry)
                || entry.availability != ESTagAvailability.Deprecated
                || entry.deprecatedReplacement.IsEmpty)
            {
                return false;
            }

            replacement = entry.deprecatedReplacement;
            return true;
        }

        public bool TryGetEntry(ESTagId id, out Entry entry)
        {
            if (!cacheReady)
                BuildRuntimeCache();

            if (runtimeEntries != null && runtimeEntries.TryGetValue(id.Value, out entry))
                return true;

            entry = default;
            return false;
        }

        public bool TryGetRuntimeKey(string key, out int runtimeKey)
        {
            if (TryGetId(key, out ESTagId id))
            {
                runtimeKey = id.Value;
                return true;
            }

            runtimeKey = 0;
            return false;
        }

        public bool TryGetRuntimeKey(ESTagStableReference reference, out int runtimeKey)
        {
            if (TryGetId(reference, out ESTagId id))
            {
                runtimeKey = id.Value;
                return true;
            }

            runtimeKey = 0;
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
            return TryGetId(enumValue, out ESTagId id) ? id : ESTagId.Invalid;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ESTagId GetEnumId(ESTagEnumGroup enumGroup, ushort enumValue)
        {
            return TryGetId(enumGroup, enumValue, out ESTagId id) ? id : ESTagId.Invalid;
        }

        private string CalculateSchemaHash(bool includeRuntimeLayout)
        {
            List<Entry> ordered = entries == null ? new List<Entry>(0) : new List<Entry>(entries);
            ordered.Sort(CompareEntries);

            ulong hash = ESKeyHash.Fnv1A64(includeRuntimeLayout ? "ESTagBakeTable/Layout/v6" : "ESTagBakeTable/Schema/v6");
            hash = ESKeyHash.Append(hash, (byte)maskLevel);
            for (int i = 0; i < ordered.Count; i++)
            {
                Entry entry = ordered[i];
                hash = ESKeyHash.Append(hash, (byte)entry.enumGroup);
                hash = ESKeyHash.Append(hash, entry.enumValue);
                hash = ESKeyHash.Append(hash, entry.key);
                hash = ESKeyHash.Append(hash, (byte)entry.storageTier);
                hash = ESKeyHash.Append(hash, (byte)entry.availability);
                hash = ESKeyHash.Append(hash, (byte)entry.deprecatedReplacement.enumGroup);
                hash = ESKeyHash.Append(hash, entry.deprecatedReplacement.enumValue);
                hash = ESKeyHash.Append(hash, entry.deprecatedReplacement.stringKey);
                hash = ESKeyHash.Append(hash, (byte)entry.stableTransferScopes);
                if (includeRuntimeLayout)
                    hash = ESKeyHash.Append(hash, entry.bakedId);
            }
            return hash.ToString("X16");
        }

        private void ValidateDeprecatedMigrations()
        {
            if (entries == null)
                return;

            for (int i = 0; i < entries.Count; i++)
            {
                Entry entry = entries[i];
                if (entry.deprecatedReplacement.IsEmpty)
                    continue;

                if (entry.availability != ESTagAvailability.Deprecated)
                {
                    validationErrors.Add("Entry[" + i + "] declares Deprecated Replacement but is not Deprecated.");
                    continue;
                }

                if (!TryGetId(entry.deprecatedReplacement, out ESTagId replacementId)
                    || replacementId.Value == entry.bakedId
                    || !TryGetEntry(replacementId, out Entry replacement)
                    || replacement.availability != ESTagAvailability.Runtime)
                {
                    validationErrors.Add("Entry[" + i + "] declares an invalid Deprecated Replacement: "
                                         + entry.deprecatedReplacement + ". The replacement must be a different Runtime Tag.");
                }
            }
        }

        /// <summary>
        /// Replaces the generated runtime Catalog from the authoritative GameCore declarations.
        /// Authored callers provide stable identities and storage policy only; RuntimeKey values
        /// are allocated here in a deterministic order.
        /// </summary>
        public bool TryReplaceEntriesAndBake(IEnumerable<Entry> source, out string error)
        {
            List<Entry> previousEntries = entries;
            entries = source != null ? new List<Entry>(source) : new List<Entry>();
            entries.Sort(CompareEntries);
            int nextHotSlot = ESTagIdRange.EnumStart;
            int nextSparseRuntimeKey = ESTagIdRange.ExtensionRuntimeStart;

            for (int i = 0; i < entries.Count; i++)
            {
                Entry entry = entries[i];
                if (entry.storageTier == ESTagStorageTier.HotSlot)
                {
                    if (nextHotSlot > ESTagIdRange.CoreRuntimeEnd)
                    {
                        error = "HotSlot capacity is exhausted. A maximum of 63 Tag declarations may use HotSlot.";
                        entries = previousEntries;
                        cacheReady = false;
                        BuildRuntimeCache();
                        return false;
                    }

                    entry.bakedId = (ushort)nextHotSlot++;
                }
                else
                {
                    if (nextSparseRuntimeKey > ESTagIdRange.MaxValue)
                    {
                        error = "Sparse RuntimeKey capacity is exhausted.";
                        entries = previousEntries;
                        cacheReady = false;
                        BuildRuntimeCache();
                        return false;
                    }

                    entry.bakedId = (ushort)nextSparseRuntimeKey++;
                }
                entries[i] = entry;
            }

            cacheReady = false;
            schemaHash = null;
            runtimeLayoutHash = null;
            BuildRuntimeCache();
            if (TryValidate(out error))
                return true;

            entries = previousEntries;
            cacheReady = false;
            BuildRuntimeCache();
            return false;
        }


#if UNITY_EDITOR
        private void EditorBakeIds()
        {
            EditorApplyMaskLevelRange();

            if (entries == null)
                entries = new List<Entry>(64);

            entries.Sort(CompareEntries);
            int nextHotSlot = ESTagIdRange.EnumStart;
            int nextSparseRuntimeKey = stringStartId < ESTagIdRange.ExtensionRuntimeStart
                ? ESTagIdRange.ExtensionRuntimeStart
                : stringStartId;
            int maxSparseRuntimeKey = stringEndId < nextSparseRuntimeKey
                ? nextSparseRuntimeKey
                : stringEndId;

            for (int i = 0; i < entries.Count; i++)
            {
                Entry entry = entries[i];
                if (entry.storageTier == ESTagStorageTier.HotSlot)
                {
                    entry.bakedId = nextHotSlot <= ESTagIdRange.CoreRuntimeEnd
                        ? (ushort)nextHotSlot++
                        : ESTagId.InvalidValue;
                }
                else if (nextSparseRuntimeKey <= maxSparseRuntimeKey)
                {
                    entry.bakedId = (ushort)nextSparseRuntimeKey;
                    nextSparseRuntimeKey++;
                }
                else
                {
                    entry.bakedId = ESTagId.InvalidValue;
#if UNITY_EDITOR
                    Debug.LogWarning("[ESTagBakeTable] Sparse RuntimeKey range is exhausted. Expand range or split table.");
#endif
                }

                entries[i] = entry;
            }

            cacheReady = false;
            schemaHash = null;
        }

        [Button("Apply Mask Level")]
        private void EditorApplyMaskLevelRange()
        {
            stringStartId = ESTagIdRange.ExtensionRuntimeStart;
            // MaskLevel controls only optional explicit-query mask conversion APIs. Sparse Tag
            // declarations always use the complete RuntimeKey range outside HotSlot.
            stringEndId = ESTagIdRange.StringDefaultEnd;
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

        private static bool IsDefinedEnumKey(ESTagEnumGroup enumGroup, ushort enumValue)
        {
            if (enumValue == ESTagId.InvalidValue)
                return false;

            switch (enumGroup)
            {
                case ESTagEnumGroup.Primary:
                    return ESGameTagCatalog.IsDefinedCore((ESGameTag)enumValue);
                case ESTagEnumGroup.Optional:
                    return Enum.IsDefined(typeof(ESGameTagOptional), (ESGameTagOptional)enumValue)
                           && enumValue != (ushort)ESGameTagOptional.None;
                default:
                    return false;
            }
        }

        private static ulong GetEnumLookupKey(ESTagEnumGroup enumGroup, ushort enumValue)
        {
            return ((ulong)(byte)enumGroup << 16) | enumValue;
        }

        private static int CompareEntries(Entry left, Entry right)
        {
            bool leftHasEnum = left.enumValue != ESTagId.InvalidValue;
            bool rightHasEnum = right.enumValue != ESTagId.InvalidValue;
            if (leftHasEnum != rightHasEnum)
                return leftHasEnum ? -1 : 1;

            int groupCompare = left.enumGroup.CompareTo(right.enumGroup);
            if (groupCompare != 0)
                return groupCompare;

            int enumCompare = left.enumValue.CompareTo(right.enumValue);
            if (enumCompare != 0)
                return enumCompare;

            return string.CompareOrdinal(left.key, right.key);
        }
    }
}
