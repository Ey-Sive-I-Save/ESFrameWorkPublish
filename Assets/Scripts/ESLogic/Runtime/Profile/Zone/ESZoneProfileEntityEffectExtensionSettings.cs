using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [Serializable]
    [TypeRegistryItem("Zone/Entity Tag 与 Buff")]
    public sealed class ESZoneProfileEntityEffectExtensionSettings : ESZoneProfileExtensionSettings
    {
        public const string StableTypeId = "es.zone.entity-effect";
        public const int CurrentSchemaVersion = 1;
        public const int DefaultOrderValue = 0;
        public const string DefaultNameTitle = "Entity Tag 与 Buff";

        [SerializeField, ReadOnly, LabelText("Schema 版本")]
        private int schemaVersion = CurrentSchemaVersion;

        [SerializeField, LabelText("启用")]
        private bool enabled = true;

        [SerializeField, MinValue(0), LabelText("预热成员容量")]
        [Tooltip("在 Profile Awake 预建常见并发成员状态，降低首次进入区域时的托管分配。")]
        private int prewarmMemberCapacity = 4;

        [SerializeField, LabelText("区域内添加 Tag")]
        private List<ESTagStableReference> tags = new List<ESTagStableReference>();

        [SerializeField, LabelText("区域内持续 Buff")]
        [Tooltip("Buff 必须配置为无限持续；生命周期由成员进入/退出管理。")]
        private List<BuffDefinitionDataInfo> buffs = new List<BuffDefinitionDataInfo>();

        public override string TypeId => StableTypeId;
        public override int SchemaVersion => schemaVersion;
        public override int SupportedSchemaVersion => CurrentSchemaVersion;
        public override int DefaultOrder => DefaultOrderValue;
        public override string NameTitleDefault => DefaultNameTitle;
        public override bool Enabled => enabled;
        public int PrewarmMemberCapacity => Mathf.Max(0, prewarmMemberCapacity);
        public IReadOnlyList<ESTagStableReference> Tags => tags;
        public IReadOnlyList<BuffDefinitionDataInfo> Buffs => buffs;

        public override ESZoneProfileExtensionRuntime CreateRuntime()
        {
            return new ESZoneProfileEntityEffectExtensionRuntime(this);
        }

        protected internal override bool Validate(
            ESZoneProfile profile,
            ESZoneProfileSettings settings,
            List<string> issues)
        {
            if (!Enabled)
                return true;

            bool valid = true;
            if ((tags == null || tags.Count == 0) && (buffs == null || buffs.Count == 0))
            {
                issues?.Add("Entity Effect Extension 至少需要一个 Tag 或 Buff；纯标记 Zone 不需要添加空能力。");
                valid = false;
            }

            if (tags != null && tags.Count > 0 && !ESTagLeaseSet.TryValidateTags(tags, out string tagError))
            {
                issues?.Add(tagError);
                valid = false;
            }

            if (!TryValidateBuffs(out string buffError))
            {
                issues?.Add(buffError);
                valid = false;
            }

            return valid;
        }

        internal bool TryValidateBuffs(out string error)
        {
            if (buffs == null || buffs.Count == 0)
            {
                error = null;
                return true;
            }

            for (int i = 0; i < buffs.Count; i++)
            {
                BuffDefinitionDataInfo definition = buffs[i];
                if (definition == null || definition.SharedData == null)
                {
                    error = "第 " + (i + 1) + " 个 Buff 为空或缺少 SharedData。";
                    return false;
                }

                BuffSharedData sharedData = definition.SharedData;
                if (sharedData.key == null || !sharedData.key.IsConfigured)
                {
                    error = "Buff " + definition.name + " 缺少稳定 Key。";
                    return false;
                }

                if (sharedData.duration >= 0f)
                {
                    error = "Buff " + definition.name + " 必须配置为无限持续（duration < 0）。";
                    return false;
                }

                if (sharedData.sourceIsolationMode != ESBuffSourceIsolationMode.ByCustomSourceId
                    && sharedData.stackMode != ESBuffStackMode.IndependentInstance)
                {
                    error = "Buff " + definition.name
                        + " 必须使用 ByCustomSourceId 来源隔离或 IndependentInstance 叠层。";
                    return false;
                }

                for (int previous = 0; previous < i; previous++)
                {
                    if (!ReferenceEquals(buffs[previous], definition))
                        continue;

                    error = "Buff " + definition.name + " 在 Extension 列表中重复配置。";
                    return false;
                }
            }

            error = null;
            return true;
        }
    }

    public sealed class ESZoneProfileEntityEffectExtensionRuntime : ESZoneProfileExtensionRuntime
    {
        private sealed class Occupant
        {
            public Occupant(int tagCapacity, int buffCapacity)
            {
                TagLeases = new ESTagLeaseSet(tagCapacity);
                Buffs = new List<ESActiveBuffRuntime>(buffCapacity);
            }

            public readonly ESTagLeaseSet TagLeases;
            public readonly List<ESActiveBuffRuntime> Buffs;
        }

        private readonly ESZoneProfileEntityEffectExtensionSettings settings;
        private readonly Dictionary<Entity, Occupant> occupants;
        private readonly Stack<Occupant> occupantPool;
        private readonly List<Entity> entityCleanupBuffer;

        public ESZoneProfileEntityEffectExtensionRuntime(
            ESZoneProfileEntityEffectExtensionSettings settings)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            int capacity = settings.PrewarmMemberCapacity;
            occupants = new Dictionary<Entity, Occupant>(capacity);
            occupantPool = new Stack<Occupant>(capacity);
            entityCleanupBuffer = new List<Entity>(capacity);
        }

        public int ActiveEntityCount => occupants.Count;

        public override void OnProfileAwake(
            ESZoneProfile profile,
            ESZoneProfileRuntimeContext context)
        {
            int capacity = settings.PrewarmMemberCapacity;
            context.EnsureMemberCapacity(capacity);
            while (occupantPool.Count < capacity)
                occupantPool.Push(CreateOccupant());
        }

        public override ESZoneMemberEnterResult TryEnterMember(
            ESZoneProfile profile,
            ESZoneProfileRuntimeContext context,
            ESZoneMember member,
            out string error)
        {
            Entity entity = member.Core as Entity;
            if (entity == null)
            {
                error = null;
                return ESZoneMemberEnterResult.Ignored;
            }

            if (occupants.ContainsKey(entity))
            {
                error = null;
                return ESZoneMemberEnterResult.Entered;
            }

            Occupant occupant = occupantPool.Count > 0 ? occupantPool.Pop() : CreateOccupant();
            if (settings.Tags != null && settings.Tags.Count > 0
                && !occupant.TagLeases.TryApply(entity.Tags, settings.Tags, profile, out error))
            {
                occupant.TagLeases.Dispose();
                ReturnOccupant(occupant);
                return ESZoneMemberEnterResult.Failed;
            }

            int sourceId = profile.GetInstanceID();
            for (int i = 0; settings.Buffs != null && i < settings.Buffs.Count; i++)
            {
                BuffDefinitionDataInfo definition = settings.Buffs[i];
                ESActiveBuffRuntime buff = entity.buffDomain != null
                    ? entity.buffDomain.AddBuff(definition, customSourceId: sourceId)
                    : null;
                if (buff == null)
                {
                    error = "Buff 添加失败: " + definition.name;
                    ReleaseEffects(entity, occupant);
                    ReturnOccupant(occupant);
                    return ESZoneMemberEnterResult.Failed;
                }

                occupant.Buffs.Add(buff);
            }

            occupants.Add(entity, occupant);
            error = null;
            return ESZoneMemberEnterResult.Entered;
        }

        public override void ExitMember(
            ESZoneProfile profile,
            ESZoneProfileRuntimeContext context,
            ESZoneMember member)
        {
            Entity entity = member.Core as Entity;
            if (entity == null || !occupants.TryGetValue(entity, out Occupant occupant))
                return;

            ReleaseEffects(entity, occupant);
            occupants.Remove(entity);
            ReturnOccupant(occupant);
        }

        public override void OnProfileDisable(
            ESZoneProfile profile,
            ESZoneProfileRuntimeContext context)
        {
            entityCleanupBuffer.Clear();
            foreach (Entity entity in occupants.Keys)
                entityCleanupBuffer.Add(entity);

            for (int i = 0; i < entityCleanupBuffer.Count; i++)
            {
                Entity entity = entityCleanupBuffer[i];
                if (occupants.TryGetValue(entity, out Occupant occupant))
                {
                    ReleaseEffects(entity, occupant);
                    occupants.Remove(entity);
                    ReturnOccupant(occupant);
                }
            }
            entityCleanupBuffer.Clear();
        }

        public override void OnProfileDestroy(
            ESZoneProfile profile,
            ESZoneProfileRuntimeContext context)
        {
            OnProfileDisable(profile, context);
        }

        private static void ReleaseEffects(Entity entity, Occupant occupant)
        {
            if (occupant == null)
                return;

            if (entity != null && entity.buffDomain != null)
            {
                for (int i = occupant.Buffs.Count - 1; i >= 0; i--)
                {
                    ESActiveBuffRuntime buff = occupant.Buffs[i];
                    if (buff != null)
                        entity.buffDomain.ApplyBuff(buff, ESBuffOperation.Remove);
                }
            }

            occupant.Buffs.Clear();
            occupant.TagLeases.Dispose();
        }

        private void ReturnOccupant(Occupant occupant)
        {
            if (occupant != null)
                occupantPool.Push(occupant);
        }

        private Occupant CreateOccupant()
        {
            int tagCapacity = settings.Tags?.Count ?? 0;
            int buffCapacity = settings.Buffs?.Count ?? 0;
            return new Occupant(tagCapacity, buffCapacity);
        }
    }
}
