using System;
using System.Collections.Generic;

namespace ES
{
    public enum ESActionPresentationAnchor : byte
    {
        OwnerRoot = 0,
        WeaponMount = 1,
    }

    [Serializable]
    public sealed class ESActionPresentationMappingEntry
    {
        public ESActionEventKind eventKind;
        public ESActionPresentationChannel channel;
        public ESActionPresentationOwner owner;
        public ESActionPresentationAnchor anchor;
        public ESActionConfigKey actionKey = new ESActionConfigKey();
        public ESWeaponConfigKey weaponKey = new ESWeaponConfigKey();
        public ESAudioCueKey audioCueKey = new ESAudioCueKey();
        public ESAssetReferPrefabConfigKey vfxPrefabKey = new ESAssetReferPrefabConfigKey();
        public ESCameraDefinitionReference cameraDefinition;
        public float cameraShakeAmplitude;
        public float hitstopSeconds;
    }

    public readonly struct ESActionEventContext
    {
        public readonly ESActionRuntimeHandle actionHandle;
        public readonly int emissionId;
        public readonly ESActionEventKind eventKind;
        public readonly ESActionPresentationChannel channel;
        public readonly ESActionConfigKey actionKey;
        public readonly ESWeaponConfigKey weaponKey;

        public ESActionEventContext(
            ESActionRuntimeHandle actionHandle,
            int emissionId,
            ESActionEventKind eventKind,
            ESActionPresentationChannel channel,
            ESActionConfigKey actionKey,
            ESWeaponConfigKey weaponKey)
        {
            this.actionHandle = actionHandle;
            this.emissionId = emissionId;
            this.eventKind = eventKind;
            this.channel = channel;
            this.actionKey = actionKey;
            this.weaponKey = weaponKey;
        }
    }

    public readonly struct ESActionResolvedPresentationPayload
    {
        public static readonly ESActionResolvedPresentationPayload Invalid = default;

        public readonly int mappingCatalogIdentity;
        public readonly int mappingCatalogGeneration;
        public readonly ESActionPresentationOwner owner;
        public readonly ESActionPresentationAnchor anchor;
        public readonly ESActionChannelState audioState;
        public readonly ESActionChannelState vfxState;
        public readonly ESActionChannelState cameraState;
        public readonly ESActionChannelState hitstopState;
        public readonly ESAudioCueKey audioCueKey;
        public readonly ESAssetReferPrefabConfigKey vfxPrefabKey;
        public readonly ESCameraDefinitionReference cameraDefinition;
        public readonly float cameraShakeAmplitude;
        public readonly float hitstopSeconds;
        public readonly bool hasPayload;

        internal ESActionResolvedPresentationPayload(
            int mappingCatalogIdentity,
            int mappingCatalogGeneration,
            ESActionPresentationOwner owner,
            ESActionPresentationAnchor anchor,
            ESActionChannelState audioState,
            ESActionChannelState vfxState,
            ESActionChannelState cameraState,
            ESActionChannelState hitstopState,
            ESAudioCueKey audioCueKey,
            ESAssetReferPrefabConfigKey vfxPrefabKey,
            ESCameraDefinitionReference cameraDefinition,
            float cameraShakeAmplitude,
            float hitstopSeconds)
        {
            this.mappingCatalogIdentity = mappingCatalogIdentity;
            this.mappingCatalogGeneration = mappingCatalogGeneration;
            this.owner = owner;
            this.anchor = anchor;
            this.audioState = audioState;
            this.vfxState = vfxState;
            this.cameraState = cameraState;
            this.hitstopState = hitstopState;
            this.audioCueKey = audioCueKey;
            this.vfxPrefabKey = vfxPrefabKey;
            this.cameraDefinition = cameraDefinition;
            this.cameraShakeAmplitude = cameraShakeAmplitude;
            this.hitstopSeconds = hitstopSeconds;
            hasPayload = true;
        }

        public bool IsSilent => hasPayload && owner == ESActionPresentationOwner.None;
    }

    public readonly struct ESActionChannelState
    {
        public readonly ESActionPresentationOwner owner;
        public readonly bool isDeclared;
        public readonly bool requiresCatalogHandle;

        public ESActionChannelState(
            ESActionPresentationOwner owner,
            bool isDeclared,
            bool requiresCatalogHandle)
        {
            this.owner = owner;
            this.isDeclared = isDeclared;
            this.requiresCatalogHandle = requiresCatalogHandle;
        }
    }

    public static class ESActionPresentationMappingTable
    {
        private static readonly List<ESActionPresentationMappingEntry> Entries =
            new List<ESActionPresentationMappingEntry>(32);
        private static readonly Dictionary<ESActionPresentationMappingLookupKey, ESActionPresentationMappingEntry> ExactIndex =
            new Dictionary<ESActionPresentationMappingLookupKey, ESActionPresentationMappingEntry>(32);
        private static bool isBuilding;
        private static int mappingCatalogIdentity = 1;
        private static int mappingCatalogGeneration;

        public static IReadOnlyList<ESActionPresentationMappingEntry> Snapshot => Entries;
        public static bool IsBuilding => isBuilding;

        public static void Clear()
        {
            Entries.Clear();
            ExactIndex.Clear();
        }

        public static void BeginBuild(bool clear)
        {
            if (clear)
            {
                Entries.Clear();
                ExactIndex.Clear();
                mappingCatalogGeneration++;
            }
            isBuilding = true;
        }

        public static void EndBuild()
        {
            isBuilding = false;
        }

        public static void Inject(IReadOnlyList<ESActionPresentationMappingEntry> entries)
        {
            if (entries == null)
                throw new ArgumentNullException(nameof(entries));

            bool ownsBuild = !isBuilding;
            if (ownsBuild)
                BeginBuild(false);
            try
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    ESActionPresentationMappingEntry entry = entries[i];
                    ValidateEntry(entry, i);
                    ValidateNoOverlappingEntry(entry);
                    if (!ExactIndex.TryAdd(new ESActionPresentationMappingLookupKey(entry), entry))
                        throw new InvalidOperationException("Action Presentation Mapping 重复：" + entry.eventKind + "/" + entry.channel);
                    Entries.Add(entry);
                }
            }
            finally
            {
                if (ownsBuild)
                    EndBuild();
            }
        }

        public static bool TryResolve(
            in ESActionEventContext context,
            ESActionPresentationOwner templateOwner,
            out ESActionResolvedPresentationPayload payload,
            out string error)
        {
            payload = default;
            error = null;

            if (TryResolveExact(context, templateOwner, out payload, out error))
                return true;
            if (error != null)
                return false;

            int bestPriority = -1;
            ESActionPresentationMappingEntry bestEntry = null;
            int bestCount = 0;
            for (int i = 0; i < Entries.Count; i++)
            {
                ESActionPresentationMappingEntry entry = Entries[i];
                if (!MatchesContext(entry, context))
                    continue;

                int priority = GetPriority(entry);
                if (priority > bestPriority)
                {
                    bestPriority = priority;
                    bestEntry = entry;
                    bestCount = 1;
                }
                else if (priority == bestPriority)
                {
                    bestCount++;
                }
            }

            if (bestCount > 1)
            {
                error = "Action Presentation Mapping 同一优先级存在多条命中映射："
                        + context.eventKind + "/" + context.channel;
                return false;
            }

            if (bestCount == 1)
            {
                if (bestEntry.owner != templateOwner)
                {
                    error = "Action Presentation Mapping Owner 与 Template 不一致："
                            + context.eventKind + "/" + context.channel;
                    return false;
                }

                payload = CreatePayload(bestEntry);
                return true;
            }

            if (templateOwner == ESActionPresentationOwner.None)
            {
                payload = new ESActionResolvedPresentationPayload(
                    mappingCatalogIdentity,
                    mappingCatalogGeneration,
                    ESActionPresentationOwner.None,
                    ESActionPresentationAnchor.OwnerRoot,
                    new ESActionChannelState(ESActionPresentationOwner.None, false, false),
                    new ESActionChannelState(ESActionPresentationOwner.None, false, false),
                    new ESActionChannelState(ESActionPresentationOwner.None, false, false),
                    new ESActionChannelState(ESActionPresentationOwner.None, false, false),
                    default,
                    default,
                    default,
                    0f,
                    0f);
                return true;
            }

            error = "Action Presentation Mapping 未找到 Direct/SkillTrack 映射："
                    + context.eventKind + "/" + context.channel;
            return false;
        }

        private static bool TryResolveExact(
            in ESActionEventContext context,
            ESActionPresentationOwner templateOwner,
            out ESActionResolvedPresentationPayload payload,
            out string error)
        {
            payload = default;
            error = null;
            ESActionPresentationMappingEntry entry;
            if (!TryGetExact(context, context.actionKey, context.weaponKey, out entry)
                && !TryGetExact(context, context.actionKey, null, out entry)
                && !TryGetExact(context, null, context.weaponKey, out entry))
                return false;

            if (entry.owner != templateOwner)
            {
                error = "Action Presentation Mapping Owner 与 Template 不一致："
                        + context.eventKind + "/" + context.channel;
                return false;
            }

            payload = CreatePayload(entry);
            return true;
        }

        private static bool TryGetExact(
            in ESActionEventContext context,
            ESActionConfigKey actionKey,
            ESWeaponConfigKey weaponKey,
            out ESActionPresentationMappingEntry entry)
        {
            return ExactIndex.TryGetValue(
                new ESActionPresentationMappingLookupKey(context.eventKind, context.channel, actionKey, weaponKey),
                out entry);
        }

        private static bool MatchesContext(
            ESActionPresentationMappingEntry entry,
            in ESActionEventContext context)
        {
            if (entry.eventKind != context.eventKind
                || entry.channel != context.channel)
                return false;

            if (entry.actionKey != null && entry.actionKey.IsConfigured)
            {
                if (context.actionKey == null || !ESConfigKeyMatch.Matches(
                        entry.actionKey.EnumKeyInt,
                        entry.actionKey.StringKey,
                        context.actionKey.EnumKeyInt,
                        context.actionKey.StringKey))
                    return false;
            }

            if (entry.weaponKey != null && entry.weaponKey.IsConfigured)
            {
                if (context.weaponKey == null || !ESConfigKeyMatch.Matches(
                        entry.weaponKey.EnumKeyInt,
                        entry.weaponKey.StringKey,
                        context.weaponKey.EnumKeyInt,
                        context.weaponKey.StringKey))
                    return false;
            }

            return true;
        }

        private static int GetPriority(ESActionPresentationMappingEntry entry)
        {
            int priority = 0;
            if (entry.actionKey != null && entry.actionKey.IsConfigured)
                priority += 2;
            if (entry.weaponKey != null && entry.weaponKey.IsConfigured)
                priority += 1;
            return priority;
        }

        private static ESActionResolvedPresentationPayload CreatePayload(ESActionPresentationMappingEntry entry)
        {
            bool isAudio = entry.channel == ESActionPresentationChannel.Audio;
            bool isVfx = entry.channel == ESActionPresentationChannel.Vfx;
            bool isCamera = entry.channel == ESActionPresentationChannel.Camera;
            bool isHitstop = entry.channel == ESActionPresentationChannel.Hitstop;
            return new ESActionResolvedPresentationPayload(
                mappingCatalogIdentity,
                mappingCatalogGeneration,
                entry.owner,
                entry.anchor,
                new ESActionChannelState(
                    entry.owner,
                    isAudio && entry.audioCueKey != null && entry.audioCueKey.IsConfigured,
                    isAudio && entry.audioCueKey != null && entry.audioCueKey.IsConfigured),
                new ESActionChannelState(
                    entry.owner,
                    isVfx && entry.vfxPrefabKey != null && entry.vfxPrefabKey.IsConfigured,
                    isVfx && entry.vfxPrefabKey != null && entry.vfxPrefabKey.IsConfigured),
                new ESActionChannelState(
                    entry.owner,
                    isCamera && entry.cameraDefinition.IsConfigured,
                    isCamera && entry.cameraDefinition.IsConfigured),
                new ESActionChannelState(
                    entry.owner,
                    isHitstop && entry.hitstopSeconds > 0f,
                    false),
                entry.audioCueKey,
                entry.vfxPrefabKey,
                entry.cameraDefinition,
                entry.cameraShakeAmplitude,
                entry.hitstopSeconds);
        }

        private static void ValidateEntry(ESActionPresentationMappingEntry entry, int index)
        {
            if (entry == null || entry.eventKind == ESActionEventKind.None
                || entry.channel == ESActionPresentationChannel.None)
                throw new InvalidOperationException("Action Presentation Mapping 非法：" + index);

            if (entry.owner == ESActionPresentationOwner.None)
                throw new InvalidOperationException("Action Presentation Mapping 不能使用 None Owner：" + index);

            if (entry.owner != ESActionPresentationOwner.Direct)
                return;

            switch (entry.channel)
            {
                case ESActionPresentationChannel.Audio:
                    if (entry.audioCueKey == null || !entry.audioCueKey.IsConfigured)
                        throw new InvalidOperationException("Direct Audio Mapping 必须配置 AudioCueKey：" + index);
                    break;
                case ESActionPresentationChannel.Vfx:
                    if (entry.vfxPrefabKey == null || !entry.vfxPrefabKey.IsConfigured)
                        throw new InvalidOperationException("Direct Vfx Mapping 必须配置 VfxPrefabKey：" + index);
                    break;
                case ESActionPresentationChannel.Camera:
                    if (!entry.cameraDefinition.IsConfigured && entry.cameraShakeAmplitude <= 0f)
                        throw new InvalidOperationException("Direct Camera Mapping 必须配置 CameraDefinition 或 Modifier：" + index);
                    break;
                case ESActionPresentationChannel.Hitstop:
                    if (entry.hitstopSeconds <= 0f)
                        throw new InvalidOperationException("Direct Hitstop Mapping 必须配置大于 0 的时长：" + index);
                    break;
                case ESActionPresentationChannel.Animation:
                    throw new InvalidOperationException("Direct Animation 尚无 Slice A 执行器，不能配置 Mapping：" + index);
            }
        }

        private static void ValidateNoOverlappingEntry(ESActionPresentationMappingEntry candidate)
        {
            int candidatePriority = GetPriority(candidate);
            for (int i = 0; i < Entries.Count; i++)
            {
                ESActionPresentationMappingEntry existing = Entries[i];
                if (existing.eventKind != candidate.eventKind
                    || existing.channel != candidate.channel
                    || GetPriority(existing) != candidatePriority)
                    continue;

                if (ReferencesOverlap(existing.actionKey, candidate.actionKey)
                    && ReferencesOverlap(existing.weaponKey, candidate.weaponKey))
                    throw new InvalidOperationException(
                        "Action Presentation Mapping 同一优先级存在重叠映射："
                        + candidate.eventKind + "/" + candidate.channel);
            }
        }

        private static bool ReferencesOverlap(IESConfigKey left, IESConfigKey right)
        {
            bool leftConfigured = left != null
                && ESConfigKeyMatch.IsConfigured(left.EnumKeyInt, left.StringKey);
            bool rightConfigured = right != null
                && ESConfigKeyMatch.IsConfigured(right.EnumKeyInt, right.StringKey);
            if (!leftConfigured || !rightConfigured)
                return true;

            return ESConfigKeyMatch.Matches(
                left.EnumKeyInt,
                left.StringKey,
                right.EnumKeyInt,
                right.StringKey);
        }
    }

    internal readonly struct ESActionPresentationMappingLookupKey : IEquatable<ESActionPresentationMappingLookupKey>
    {
        private readonly ESActionEventKind eventKind;
        private readonly ESActionPresentationChannel channel;
        private readonly int actionEnumKey;
        private readonly string actionStringKey;
        private readonly int weaponEnumKey;
        private readonly string weaponStringKey;

        internal ESActionPresentationMappingLookupKey(ESActionPresentationMappingEntry entry)
            : this(entry.eventKind, entry.channel, entry.actionKey, entry.weaponKey) { }

        internal ESActionPresentationMappingLookupKey(
            ESActionEventKind eventKind,
            ESActionPresentationChannel channel,
            ESActionConfigKey actionKey,
            ESWeaponConfigKey weaponKey)
        {
            this.eventKind = eventKind;
            this.channel = channel;
            actionEnumKey = actionKey != null && actionKey.IsConfigured ? actionKey.EnumKeyInt : 0;
            actionStringKey = actionKey != null && actionKey.IsConfigured ? actionKey.StringKey : null;
            weaponEnumKey = weaponKey != null && weaponKey.IsConfigured ? weaponKey.EnumKeyInt : 0;
            weaponStringKey = weaponKey != null && weaponKey.IsConfigured ? weaponKey.StringKey : null;
        }

        public bool Equals(ESActionPresentationMappingLookupKey other)
            => eventKind == other.eventKind && channel == other.channel
               && actionEnumKey == other.actionEnumKey && actionStringKey == other.actionStringKey
               && weaponEnumKey == other.weaponEnumKey && weaponStringKey == other.weaponStringKey;

        public override bool Equals(object obj)
            => obj is ESActionPresentationMappingLookupKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = ((int)eventKind * 397) ^ (int)channel;
                hash = (hash * 397) ^ actionEnumKey;
                hash = (hash * 397) ^ (actionStringKey != null ? actionStringKey.GetHashCode() : 0);
                hash = (hash * 397) ^ weaponEnumKey;
                return (hash * 397) ^ (weaponStringKey != null ? weaponStringKey.GetHashCode() : 0);
            }
        }
    }

    [ESCreatePath("数据信息", "动作表现映射数据信息")]
    public sealed class ActionPresentationMappingDataInfo : SoDataInfo, IGameCoreSO
    {
        public List<ESActionPresentationMappingEntry> entries = new List<ESActionPresentationMappingEntry>();

        public void InjectGameCoreTables()
        {
            ESActionPresentationMappingTable.Inject(entries);
        }
    }

    [ESCreatePath("数据组/GameCore", "动作表现映射数据组")]
    public sealed class ActionPresentationMappingDataGroup : SoDataGroup<ActionPresentationMappingDataInfo>
    {
    }
}
