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
        public const int MaxPrewarmMemberCapacity = 256;

        [SerializeField, ReadOnly, LabelText("Schema 版本")]
        private int schemaVersion = CurrentSchemaVersion;

        [SerializeField, LabelText("启用")]
        private bool enabled = true;

        [SerializeField, MinValue(0), LabelText("预热成员容量")]
        [Tooltip("在 Profile Awake 显式预建最多 256 个成员状态；未配置更高预热时，运行时回收池最多自动扩至 64 个。")]
        private int prewarmMemberCapacity;

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
        public int PrewarmMemberCapacity => Mathf.Clamp(
            prewarmMemberCapacity,
            0,
            MaxPrewarmMemberCapacity);
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
            if (prewarmMemberCapacity < 0
                || prewarmMemberCapacity > MaxPrewarmMemberCapacity)
            {
                issues?.Add("Entity Effect 预热成员容量必须在 0 到 "
                    + MaxPrewarmMemberCapacity + " 之间。");
                valid = false;
            }
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
        private const int MaxAutomaticallyRetainedOccupantCount = 64;

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
        private readonly int retainedOccupantLimit;
        private Dictionary<Entity, Occupant> occupants;
        private Stack<Occupant> occupantPool;
        private List<Entity> entityCleanupBuffer;

        public ESZoneProfileEntityEffectExtensionRuntime(
            ESZoneProfileEntityEffectExtensionSettings settings)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            int capacity = settings.PrewarmMemberCapacity;
            retainedOccupantLimit = Mathf.Max(capacity, MaxAutomaticallyRetainedOccupantCount);
            if (capacity > 0)
                EnsureCollections(capacity);
        }

        public int ActiveEntityCount => occupants?.Count ?? 0;
        public int RetainedOccupantCount => occupantPool?.Count ?? 0;

        public override void OnProfileAwake(
            ESZoneProfile profile,
            ESZoneProfileRuntimeContext context)
        {
            int capacity = settings.PrewarmMemberCapacity;
            if (capacity <= 0)
                return;

            EnsureCollections(capacity);
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

            EnsureCollections(0);
            if (occupants.ContainsKey(entity))
            {
                error = null;
                return ESZoneMemberEnterResult.Entered;
            }

            Occupant occupant = occupantPool.Count > 0 ? occupantPool.Pop() : CreateOccupant();
            try
            {
                if (settings.Tags != null && settings.Tags.Count > 0
                    && !occupant.TagLeases.TryApply(entity.Tags, settings.Tags, profile, out error))
                {
                    ReleaseEffects(entity, occupant);
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
            }
            catch (Exception exception)
            {
                ReleaseEffects(entity, occupant);
                ReturnOccupant(occupant);
                Debug.LogException(exception, entity);
                error = "Entity Effect 登记异常: " + exception.Message;
                return ESZoneMemberEnterResult.Failed;
            }
            error = null;
            return ESZoneMemberEnterResult.Entered;
        }

        public override void ExitMember(
            ESZoneProfile profile,
            ESZoneProfileRuntimeContext context,
            ESZoneMember member)
        {
            Entity entity = member.Core as Entity;
            if (entity == null || occupants == null
                || !occupants.TryGetValue(entity, out Occupant occupant))
                return;

            occupants.Remove(entity);
            ReleaseEffects(entity, occupant);
            ReturnOccupant(occupant);
        }

        public override void OnProfileDisable(
            ESZoneProfile profile,
            ESZoneProfileRuntimeContext context)
        {
            if (occupants == null || occupants.Count == 0)
                return;

            EnsureCollections(0);
            entityCleanupBuffer.Clear();
            foreach (Entity entity in occupants.Keys)
                entityCleanupBuffer.Add(entity);

            for (int i = 0; i < entityCleanupBuffer.Count; i++)
            {
                Entity entity = entityCleanupBuffer[i];
                if (occupants.TryGetValue(entity, out Occupant occupant))
                {
                    occupants.Remove(entity);
                    ReleaseEffects(entity, occupant);
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

            try
            {
                if (entity != null && entity.buffDomain != null)
                {
                    for (int i = occupant.Buffs.Count - 1; i >= 0; i--)
                    {
                        ESActiveBuffRuntime buff = occupant.Buffs[i];
                        if (buff == null)
                            continue;

                        try
                        {
                            entity.buffDomain.ApplyBuff(buff, ESBuffOperation.Remove);
                        }
                        catch (Exception exception)
                        {
                            Debug.LogException(exception, entity);
                        }
                    }
                }
            }
            finally
            {
                occupant.Buffs.Clear();
                try
                {
                    occupant.TagLeases.Dispose();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, entity);
                }
            }
        }

        private void ReturnOccupant(Occupant occupant)
        {
            if (occupant != null
                && occupant.Buffs.Count == 0
                && occupant.TagLeases.Count == 0
                && occupantPool.Count < retainedOccupantLimit)
            {
                occupantPool.Push(occupant);
            }
        }

        private void EnsureCollections(int capacity)
        {
            occupants ??= new Dictionary<Entity, Occupant>(capacity);
            occupantPool ??= new Stack<Occupant>(Mathf.Min(capacity, MaxAutomaticallyRetainedOccupantCount));
            entityCleanupBuffer ??= new List<Entity>(capacity);
        }

        private Occupant CreateOccupant()
        {
            int tagCapacity = settings.Tags?.Count ?? 0;
            int buffCapacity = settings.Buffs?.Count ?? 0;
            return new Occupant(tagCapacity, buffCapacity);
        }
    }
}
