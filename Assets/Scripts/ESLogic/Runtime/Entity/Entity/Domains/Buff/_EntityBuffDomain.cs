using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [Serializable, TypeRegistryItem("Buff域")]
    public class EntityBuffDomain : Domain<Entity, EntityBuffModuleBase>
    {
        [TitleGroup("运行支持", Alignment = TitleAlignments.Left)]
        [NonSerialized, ShowInInspector, Sirenix.OdinInspector.ReadOnly, LabelText("Buff域 OpSupport")]
        public ESOpSupport opSupport;

        [TitleGroup("运行时", Alignment = TitleAlignments.Left)]
        [ShowInInspector, ReadOnly, LabelText("运行中 Buff")]
        private readonly List<ESActiveBuffRuntime> activeBuffs = new List<ESActiveBuffRuntime>(8);

        [ShowInInspector, ReadOnly, LabelText("静默 Buff")]
        private readonly List<ESActiveBuffRuntime> inactiveBuffs = new List<ESActiveBuffRuntime>(8);

        // Mirrors the Input service's BeginFrame -> Write -> EndFrame contract for state-driven
        // effects. This is command-buffer state, not a serialized Buff configuration wrapper.
        private struct BuffFrameWrite
        {
            public BuffDefinitionDataInfo definition;
            public BuffSharedData sharedData;
            public int definitionKey;
            public ESRuntimeTargetPack target;
            public ESOpSupport sourceSupport;
        }

        private List<BuffFrameWrite> buffFrameWrites;
        private object buffFrameOwner;
        private ulong buffFrameNumber;
        private bool buffFrameWriteFailed;

        // Optional read-only UI/combat-log notification. Domains with no observers do not allocate
        // a Link container or enter dispatch on their normal Buff lifecycle path.
        private LinkReceiveList<ESBuffChangedLink> buffChangedLinks;

        public ESOpSupport OpSupport
        {
            get
            {
                EnsureBuffOpSupport();
                return opSupport;
            }
        }

        public int ActiveBuffCount => activeBuffs.Count;
        public int InactiveBuffCount => inactiveBuffs.Count;
        public bool IsBuffFrameOpen => buffFrameOwner != null;

        /// <summary>
        /// Registers a read-only Buff lifecycle observer. Payloads are value snapshots and never
        /// expose a mutable Buff Runtime, so observers cannot take ownership of Buff state.
        /// </summary>
        public bool AddBuffChangedReceiver(IReceiveLink<ESBuffChangedLink> receiver)
        {
            if (receiver == null)
                return false;

            buffChangedLinks ??= new LinkReceiveList<ESBuffChangedLink>();
            return buffChangedLinks.AddReceiver(receiver);
        }

        /// <summary>Unregisters a Buff lifecycle observer. Link dispatch semantics apply on re-entry.</summary>
        public bool RemoveBuffChangedReceiver(IReceiveLink<ESBuffChangedLink> receiver)
        {
            return receiver != null && buffChangedLinks != null && buffChangedLinks.RemoveReceiver(receiver);
        }

        private Entity RequireValueChangeOwner()
        {
            if (MyCore == null)
            {
                throw new InvalidOperationException(
                    "EntityBuffDomain no longer owns ValueChange state. Bind the domain to an Entity and use Entity.ValueChange APIs.");
            }

            return MyCore;
        }

        // Compatibility only. New gameplay code must call the Entity APIs directly.
        [Obsolete("ValueChange is owned by Entity. Call Entity.BindSuperAttributeTable instead.")]
        public void BindSuperAttributeTable(ESSuperAttributeTable table) => RequireValueChangeOwner().BindSuperAttributeTable(table);
        [Obsolete("ValueChange is owned by Entity. Read Entity.SuperAttributeCatalog instead.")]
        public ESSuperAttributeCatalog SuperAttributeCatalog => RequireValueChangeOwner().SuperAttributeCatalog;
        [Obsolete("ValueChange is owned by Entity. Read Entity.SuperAttributeCatalogError instead.")]
        public string SuperAttributeCatalogError => RequireValueChangeOwner().SuperAttributeCatalogError;
        [Obsolete("ValueChange is owned by Entity. Call Entity.ActiveValueChangeEffectCount instead.")]
        public int ActiveValueChangeEffectCount => RequireValueChangeOwner().ActiveValueChangeEffectCount;
        [Obsolete("ValueChange is owned by Entity. Call Entity.CreateValueChangeEffectLease instead.")]
        public ESEffectLease CreateValueChangeEffectLease() => RequireValueChangeOwner().CreateValueChangeEffectLease();
        [Obsolete("ValueChange is owned by Entity. Leases release through Entity automatically.")]
        public bool ReleaseEffect(int effectSlot, int generation) => RequireValueChangeOwner().ReleaseEffect(effectSlot, generation);
        [Obsolete("ValueChange is owned by Entity. Call Entity.GetFloatStat instead.")]
        public ESFloatValueChangeSet GetFloatStat(string key, float baseValue = 0f) => RequireValueChangeOwner().GetFloatStat(key, baseValue);
        [Obsolete("ValueChange is owned by Entity. Call Entity.GetFloatStat instead.")]
        public ESFloatValueChangeSet GetFloatStat(ushort enumKey, string key, float baseValue = 0f) => RequireValueChangeOwner().GetFloatStat(enumKey, key, baseValue);
        [Obsolete("ValueChange is owned by Entity. Call Entity.GetFloatStat instead.")]
        public ESFloatValueChangeSet GetFloatStat(int runtimeKey, float baseValue = 0f) => RequireValueChangeOwner().GetFloatStat(runtimeKey, baseValue);
        [Obsolete("ValueChange is owned by Entity. Call Entity.GetCharacterFloatStat instead.")]
        public ESFloatValueChangeSet GetCharacterFloatStat(ESCharacterFloatAttributeId id, float fallbackBaseValue = 0f) => RequireValueChangeOwner().GetCharacterFloatStat(id, fallbackBaseValue);
        [Obsolete("ValueChange is owned by Entity. Call Entity.TryGetFloatStat instead.")]
        public bool TryGetFloatStat(string key, out ESFloatValueChangeSet set) => RequireValueChangeOwner().TryGetFloatStat(key, out set);
        [Obsolete("ValueChange is owned by Entity. Call Entity.TryGetFloatStat instead.")]
        public bool TryGetFloatStat(ushort enumKey, string key, out ESFloatValueChangeSet set) => RequireValueChangeOwner().TryGetFloatStat(enumKey, key, out set);
        [Obsolete("ValueChange is owned by Entity. Call Entity.TryGetFloatStat instead.")]
        public bool TryGetFloatStat(int runtimeKey, out ESFloatValueChangeSet set) => RequireValueChangeOwner().TryGetFloatStat(runtimeKey, out set);
        [Obsolete("ValueChange is owned by Entity. Call Entity.GetFloatStatValue instead.")]
        public float GetFloatStatValue(string key, float baseValue = 0f) => RequireValueChangeOwner().GetFloatStatValue(key, baseValue);
        [Obsolete("ValueChange is owned by Entity. Call Entity.GetFloatStatValue instead.")]
        public float GetFloatStatValue(ushort enumKey, string key, float baseValue = 0f) => RequireValueChangeOwner().GetFloatStatValue(enumKey, key, baseValue);
        [Obsolete("ValueChange is owned by Entity. Call Entity.GetCharacterFloatStatValue instead.")]
        public float GetCharacterFloatStatValue(ESCharacterFloatAttributeId id, float fallbackBaseValue = 0f) => RequireValueChangeOwner().GetCharacterFloatStatValue(id, fallbackBaseValue);
        [Obsolete("ValueChange is owned by Entity. Call Entity.SetFloatStatBaseValue instead.")]
        public void SetFloatStatBaseValue(string key, float baseValue) => RequireValueChangeOwner().SetFloatStatBaseValue(key, baseValue);
        [Obsolete("ValueChange is owned by Entity. Call Entity.SetFloatStatBaseValue instead.")]
        public void SetFloatStatBaseValue(ushort enumKey, string key, float baseValue) => RequireValueChangeOwner().SetFloatStatBaseValue(enumKey, key, baseValue);
        [Obsolete("ValueChange is owned by Entity. Call Entity.SetCharacterFloatStatBaseValue instead.")]
        public void SetCharacterFloatStatBaseValue(ESCharacterFloatAttributeId id, float baseValue) => RequireValueChangeOwner().SetCharacterFloatStatBaseValue(id, baseValue);
        [Obsolete("ValueChange is owned by Entity. Call Entity.SetPermitFallbackValue instead.")]
        public void SetPermitFallbackValue(string key, bool fallbackValue) => RequireValueChangeOwner().SetPermitFallbackValue(key, fallbackValue);
        [Obsolete("ValueChange is owned by Entity. Call Entity.SetPermitFallbackValue instead.")]
        public void SetPermitFallbackValue(ushort enumKey, string key, bool fallbackValue) => RequireValueChangeOwner().SetPermitFallbackValue(enumKey, key, fallbackValue);
        [Obsolete("ValueChange is owned by Entity. Call Entity.SetCharacterPermitFallbackValue instead.")]
        public void SetCharacterPermitFallbackValue(ESCharacterPermitAttributeId id, bool fallbackValue) => RequireValueChangeOwner().SetCharacterPermitFallbackValue(id, fallbackValue);
        [Obsolete("ValueChange is owned by Entity. Call Entity.GetPermit instead.")]
        public ESPermitSet GetPermit(string key, bool fallbackValue = true) => RequireValueChangeOwner().GetPermit(key, fallbackValue);
        [Obsolete("ValueChange is owned by Entity. Call Entity.GetPermit instead.")]
        public ESPermitSet GetPermit(ushort enumKey, string key, bool fallbackValue = true) => RequireValueChangeOwner().GetPermit(enumKey, key, fallbackValue);
        [Obsolete("ValueChange is owned by Entity. Call Entity.GetPermit instead.")]
        public ESPermitSet GetPermit(int runtimeKey, bool fallbackValue = true) => RequireValueChangeOwner().GetPermit(runtimeKey, fallbackValue);
        [Obsolete("ValueChange is owned by Entity. Call Entity.GetCharacterPermit instead.")]
        public ESPermitSet GetCharacterPermit(ESCharacterPermitAttributeId id, bool fallbackValue = true) => RequireValueChangeOwner().GetCharacterPermit(id, fallbackValue);
        [Obsolete("ValueChange is owned by Entity. Call Entity.GetPermitValue instead.")]
        public bool GetPermitValue(string key, bool fallbackValue = true) => RequireValueChangeOwner().GetPermitValue(key, fallbackValue);
        [Obsolete("ValueChange is owned by Entity. Call Entity.GetPermitValue instead.")]
        public bool GetPermitValue(ushort enumKey, string key, bool fallbackValue = true) => RequireValueChangeOwner().GetPermitValue(enumKey, key, fallbackValue);
        [Obsolete("ValueChange is owned by Entity. Call Entity.GetCharacterPermitValue instead.")]
        public bool GetCharacterPermitValue(ESCharacterPermitAttributeId id, bool fallbackValue = true) => RequireValueChangeOwner().GetCharacterPermitValue(id, fallbackValue);
        [Obsolete("ValueChange is owned by Entity. Call Entity.GetPermitResult instead.")]
        public ESPermitLawResult GetPermitResult(string key, bool fallbackValue = true) => RequireValueChangeOwner().GetPermitResult(key, fallbackValue);
        [Obsolete("ValueChange is owned by Entity. Call Entity.GetPermitResult instead.")]
        public ESPermitLawResult GetPermitResult(ushort enumKey, string key, bool fallbackValue = true) => RequireValueChangeOwner().GetPermitResult(enumKey, key, fallbackValue);
        [Obsolete("ValueChange is owned by Entity. Call Entity.ClearValueChanges instead.")]
        public void ClearValueChanges() => RequireValueChangeOwner().ClearValueChanges();
        [Obsolete("ValueChange is owned by Entity. Call Entity.TryGetPermit instead.")]
        public bool TryGetPermit(string key, out ESPermitSet set) => RequireValueChangeOwner().TryGetPermit(key, out set);
        [Obsolete("ValueChange is owned by Entity. Call Entity.TryGetPermit instead.")]
        public bool TryGetPermit(ushort enumKey, string key, out ESPermitSet set) => RequireValueChangeOwner().TryGetPermit(enumKey, key, out set);
        [Obsolete("ValueChange is owned by Entity. Call Entity.TryGetPermit instead.")]
        public bool TryGetPermit(int runtimeKey, out ESPermitSet set) => RequireValueChangeOwner().TryGetPermit(runtimeKey, out set);

        public override void _AwakeRegisterAllModules()
        {
            EnsureBuffOpSupport();
            base._AwakeRegisterAllModules();
        }

        public override void UpdateAsHosting()
        {
            base.UpdateAsHosting();
            TickActiveBuffs(Time.deltaTime);
        }

        protected override void OnDestroy()
        {
            ReturnAllBuffsToPool(false);
            base.OnDestroy();
            opSupport?.Dispose();
            opSupport = null;
        }

        public void EnsureBuffOpSupport()
        {
            if (opSupport == null || opSupport.IsRecycled)
                opSupport = ESOpSupport.CreateStandalone();

            ESOpSupport hostSupport = MyCore != null ? MyCore.OpSupport : null;
            int ownerId = MyCore != null ? MyCore.GetInstanceID() : 0;
            if (opSupport.Kind != ESOpSupportKind.Buff || opSupport.OwnerBuffDomain != this || opSupport.Parent != hostSupport)
                opSupport.InitializeBuffOwner(this, null, hostSupport, ownerId);
        }

        /// <summary>Applies one authored Buff definition. This is the preferred path for a direct asset reference.</summary>
        public ESActiveBuffRuntime AddBuff(
            BuffDefinitionDataInfo definition,
            ESRuntimeTargetPack target = null,
            ESOpSupport sourceSupport = null,
            float durationOverride = -1f,
            int customSourceId = 0)
        {
            return AddBuffInternal(definition, definition != null ? definition.SharedData : null, target, sourceSupport, null, null, null,
                customSourceId, durationOverride, 1, ResolveDefinitionInitialLevel(definition));
        }

        /// <summary>Applies an authored Buff whose clock follows the supplied State.</summary>
        public ESActiveBuffRuntime AddBuffByStateTime(
            BuffDefinitionDataInfo definition,
            StateBase stateTimeSource,
            ESRuntimeTargetPack target = null,
            ESOpSupport sourceSupport = null,
            float durationOverride = -1f,
            int customSourceId = 0)
        {
            return AddBuffInternal(definition, definition != null ? definition.SharedData : null, target, sourceSupport, null, null, stateTimeSource,
                customSourceId, durationOverride, 1, ResolveDefinitionInitialLevel(definition));
        }

        /// <summary>
        /// Applies a GameCore Buff by its stable Enum key. No RuntimeKey is accepted by the public API.
        /// </summary>
        public ESActiveBuffRuntime AddBuff(
            ESBuffEnumKey buffKey,
            ESRuntimeTargetPack target = null,
            ESOpSupport sourceSupport = null,
            float durationOverride = -1f,
            int customSourceId = 0)
        {
            return ESRuntimeDataGameCore.Buffs.TryGet((int)buffKey, out ESBuffRuntimeData runtimeData)
                ? AddRuntimeBuff(runtimeData, target, sourceSupport, null, durationOverride, customSourceId)
                : null;
        }

        /// <summary>
        /// Applies a GameCore Buff by its stable String key. String lookup is an application-time
        /// operation; the active Buff stores the resolved process-local runtime key internally.
        /// </summary>
        public ESActiveBuffRuntime AddBuff(
            string buffKey,
            ESRuntimeTargetPack target = null,
            ESOpSupport sourceSupport = null,
            float durationOverride = -1f,
            int customSourceId = 0)
        {
            return TryGetRuntimeBuffData(buffKey, out ESBuffRuntimeData runtimeData)
                ? AddRuntimeBuff(runtimeData, target, sourceSupport, null, durationOverride, customSourceId)
                : null;
        }

        /// <summary>State-clock variant of <see cref="AddBuff(ESBuffEnumKey,ESRuntimeTargetPack,ESOpSupport,float,int)"/>.</summary>
        public ESActiveBuffRuntime AddBuffByStateTime(
            ESBuffEnumKey buffKey,
            StateBase stateTimeSource,
            ESRuntimeTargetPack target = null,
            ESOpSupport sourceSupport = null,
            float durationOverride = -1f,
            int customSourceId = 0)
        {
            return ESRuntimeDataGameCore.Buffs.TryGet((int)buffKey, out ESBuffRuntimeData runtimeData)
                ? AddRuntimeBuff(runtimeData, target, sourceSupport, stateTimeSource, durationOverride, customSourceId)
                : null;
        }

        /// <summary>State-clock variant of <see cref="AddBuff(string,ESRuntimeTargetPack,ESOpSupport,float,int)"/>.</summary>
        public ESActiveBuffRuntime AddBuffByStateTime(
            string buffKey,
            StateBase stateTimeSource,
            ESRuntimeTargetPack target = null,
            ESOpSupport sourceSupport = null,
            float durationOverride = -1f,
            int customSourceId = 0)
        {
            return TryGetRuntimeBuffData(buffKey, out ESBuffRuntimeData runtimeData)
                ? AddRuntimeBuff(runtimeData, target, sourceSupport, stateTimeSource, durationOverride, customSourceId)
                : null;
        }

        /// <summary>
        /// Advanced runtime-only path. Normal gameplay should pass a definition or stable key so
        /// GameCore remains the configuration authority.
        /// </summary>
        public ESActiveBuffRuntime AddBuff(
            BuffSharedData sharedData,
            ESRuntimeTargetPack target = null,
            ESOpSupport sourceSupport = null,
            float durationOverride = -1f,
            int customSourceId = 0)
        {
            return AddBuffInternal(null, sharedData, target, sourceSupport, null, null, null, customSourceId, durationOverride, 1, 1);
        }

        private ESActiveBuffRuntime AddRuntimeBuff(
            ESBuffRuntimeData runtimeData,
            ESRuntimeTargetPack target,
            ESOpSupport sourceSupport,
            StateBase stateTimeSource,
            float durationOverride,
            int customSourceId)
        {
            return runtimeData != null
                ? AddBuffInternal(runtimeData.soSource, runtimeData.sharedData, target, sourceSupport, null, null, stateTimeSource,
                    customSourceId, durationOverride, 1,
                    runtimeData.defaultVariableData != null ? runtimeData.defaultVariableData.level : ResolveDefinitionInitialLevel(runtimeData.soSource))
                : null;
        }

        private static int ResolveDefinitionInitialLevel(BuffDefinitionDataInfo definition)
        {
            return definition != null && definition.VariableData != null ? definition.VariableData.level : 1;
        }

        #region Buff 操作集

        /// <summary>
        /// Runs a composed operation against one Enum-keyed Buff. <see cref="ESBuffOperation.Default"/>
        /// has exactly the same behaviour as <see cref="AddBuff(ESBuffEnumKey,ESRuntimeTargetPack,ESOpSupport,float,int)"/>;
        /// use a configured operation when gameplay needs an explicit timer/stack/level change.
        /// </summary>
        public ESActiveBuffRuntime ApplyBuff(
            ESBuffEnumKey buffKey,
            ESBuffOperation operation,
            ESRuntimeTargetPack target = null,
            ESOpSupport sourceSupport = null,
            int customSourceId = 0)
        {
            return ESRuntimeDataGameCore.Buffs.TryGet((int)buffKey, out ESBuffRuntimeData runtimeData)
                ? ApplyBuffOperation(runtimeData.soSource, runtimeData.sharedData, (int)buffKey,
                    operation, target, sourceSupport, customSourceId,
                    runtimeData.defaultVariableData != null ? runtimeData.defaultVariableData.level : ResolveDefinitionInitialLevel(runtimeData.soSource))
                : null;
        }

        /// <summary>
        /// Stable GameCore-key counterpart of the Enum/String APIs. This resolves both aliases as
        /// one identity, so a configured Op cannot silently prefer an EnumKey over a conflicting
        /// StringKey.
        /// </summary>
        public ESActiveBuffRuntime ApplyBuff(
            ESBuffConfigKey buffKey,
            ESBuffOperation operation,
            ESRuntimeTargetPack target = null,
            ESOpSupport sourceSupport = null,
            int customSourceId = 0)
        {
            return buffKey != null
                   && ESRuntimeDataGameCore.Buffs.TryGetRuntimeKey(buffKey, out int runtimeKey)
                   && ESRuntimeDataGameCore.Buffs.TryGet(runtimeKey, out ESBuffRuntimeData runtimeData)
                ? ApplyBuffOperation(runtimeData.soSource, runtimeData.sharedData, runtimeKey,
                    operation, target, sourceSupport, customSourceId,
                    runtimeData.defaultVariableData != null ? runtimeData.defaultVariableData.level : ResolveDefinitionInitialLevel(runtimeData.soSource))
                : null;
        }

        /// <summary>String-keyed counterpart of <see cref="ApplyBuff(ESBuffEnumKey,ESBuffOperation,ESRuntimeTargetPack,ESOpSupport,int)"/>.</summary>
        public ESActiveBuffRuntime ApplyBuff(
            string buffKey,
            ESBuffOperation operation,
            ESRuntimeTargetPack target = null,
            ESOpSupport sourceSupport = null,
            int customSourceId = 0)
        {
            return TryGetRuntimeBuffKey(buffKey, out int runtimeKey)
                && ESRuntimeDataGameCore.Buffs.TryGet(runtimeKey, out ESBuffRuntimeData runtimeData)
                ? ApplyBuffOperation(runtimeData.soSource, runtimeData.sharedData, runtimeKey,
                    operation, target, sourceSupport, customSourceId,
                    runtimeData.defaultVariableData != null ? runtimeData.defaultVariableData.level : ResolveDefinitionInitialLevel(runtimeData.soSource))
                : null;
        }

        /// <summary>Definition-reference counterpart of the stable-key operation APIs.</summary>
        public ESActiveBuffRuntime ApplyBuff(
            BuffDefinitionDataInfo definition,
            ESBuffOperation operation,
            ESRuntimeTargetPack target = null,
            ESOpSupport sourceSupport = null,
            int customSourceId = 0)
        {
            int definitionKey = ESBuffSourceKeyUtility.ResolveDefinitionKey(definition);
            return definition != null && definitionKey != 0
                ? ApplyBuffOperation(definition, definition.SharedData, definitionKey,
                    operation, target, sourceSupport, customSourceId, ResolveDefinitionInitialLevel(definition))
                : null;
        }

        /// <summary>
        /// Exact-instance counterpart for IndependentInstance Buffs. A key cannot safely choose
        /// between two independent instances with the same source; retain the handle returned by
        /// AddBuff/ApplyBuff and operate on that handle instead.
        /// </summary>
        public bool ApplyBuff(ESActiveBuffRuntime buff, ESBuffOperation operation)
        {
            if (buff == null || !ContainsActiveBuff(buff))
                return false;

            if (operation.action == ESBuffOperationAction.Remove)
            {
                int index = IndexOfActiveBuff(buff);
                if (index < 0)
                    return false;

                RemoveBuffAt(index);
                return true;
            }

            if (operation.UsesDefinitionReapply)
            {
                buff.AddStackOrRefresh(buff.SharedData != null ? buff.SharedData.duration : 0f, 1);
                return true;
            }

            return buff.ApplyOperation(operation);
        }

        private ESActiveBuffRuntime ApplyBuffOperation(
            BuffDefinitionDataInfo definition,
            BuffSharedData sharedData,
            int definitionKey,
            ESBuffOperation operation,
            ESRuntimeTargetPack target,
            ESOpSupport sourceSupport,
            int customSourceId,
            int definitionInitialLevel)
        {
            if (sharedData == null || definitionKey == 0)
                return null;

            int sourceKey = ESBuffSourceKeyUtility.ResolveSourceKey(sharedData, sourceSupport, null, null, customSourceId);
            ESActiveBuffRuntime existing = FindUniqueBuffForOperation(definitionKey, sourceKey, out bool ambiguous);
            if (ambiguous)
            {
                Debug.LogError("[Buff] Key 操作无法在同一来源的多个 IndependentInstance Buff 中选择目标；请保存 AddBuff 返回的 ESActiveBuffRuntime 后调用 ApplyBuff(runtime, operation)。");
                return null;
            }

            if (operation.action == ESBuffOperationAction.Remove)
            {
                if (existing == null)
                    return null;

                int index = IndexOfActiveBuff(existing);
                if (index >= 0)
                    RemoveBuffAt(index);
                return null;
            }

            if (operation.UsesDefinitionReapply)
            {
                if (operation.missingPolicy == ESBuffMissingPolicy.Ignore)
                {
                    if (existing == null)
                        return null;

                    existing.AddStackOrRefresh(sharedData.duration, 1);
                    return existing;
                }

                // Preserve the ordinary public AddBuff contract, including the definition's
                // source isolation, stacking and group-conflict rules.
                return AddBuffInternal(definition, sharedData, target, sourceSupport, null, null, null,
                    customSourceId, -1f, 1, definitionInitialLevel);
            }

            if (existing != null)
                return existing.ApplyOperation(operation) ? existing : null;

            if (operation.missingPolicy == ESBuffMissingPolicy.Ignore || !CanApplyBuffSharedData(sharedData))
                return null;

            // On first creation an explicit stack/time/level operation supplies the initial value;
            // it is not applied a second time after creation.
            return AddBuffInternal(definition, sharedData, target, sourceSupport, null, null, null,
                customSourceId, operation.ResolveInitialDuration(sharedData),
                operation.ResolveInitialStack(sharedData),
                operation.levelOperation == ESBuffLevelOperation.Keep
                    ? definitionInitialLevel
                    : operation.ResolveInitialLevel(sharedData));
        }

        private ESActiveBuffRuntime FindUniqueBuffForOperation(int definitionKey, int sourceKey, out bool ambiguous)
        {
            ambiguous = false;
            ESActiveBuffRuntime found = null;
            for (int i = 0; i < activeBuffs.Count; i++)
            {
                ESActiveBuffRuntime candidate = activeBuffs[i];
                if (!candidate.CanMergeWith(definitionKey, sourceKey))
                    continue;

                if (found != null)
                {
                    ambiguous = true;
                    return null;
                }

                found = candidate;
            }

            return found;
        }

        private int IndexOfActiveBuff(ESActiveBuffRuntime buff)
        {
            for (int i = 0; i < activeBuffs.Count; i++)
            {
                if (ReferenceEquals(activeBuffs[i], buff))
                    return i;
            }

            return -1;
        }

        #endregion

        #region 状态效果帧

        /// <summary>
        /// Starts an exact state-effect write for one owner. Pair with <see cref="EndBuffFrame"/>
        /// in the same update point, just as input uses BeginFrame/Write/EndFrame.
        /// Effects written by this owner survive; its old effects omitted from the completed frame
        /// are removed. Ordinary <see cref="AddBuff"/> lifecycles are never touched.
        /// </summary>
        public bool BeginBuffFrame(object owner)
        {
            if (owner == null)
            {
                Debug.LogError("[Buff] BeginBuffFrame 需要非空来源对象。请传入 State、技能运行时或其他稳定生命周期对象。");
                return false;
            }

            if (buffFrameOwner != null)
            {
                Debug.LogError("[Buff] 同一 EntityBuffDomain 不允许嵌套 BuffFrame；必须先 EndBuffFrame 再开始下一帧。");
                return false;
            }

            buffFrameOwner = owner;
            buffFrameNumber++;
            if (buffFrameNumber == 0)
                buffFrameNumber = 1;

            buffFrameWrites?.Clear();
            buffFrameWriteFailed = false;
            return true;
        }

        /// <summary>
        /// Declares one Enum-keyed effect for the current Buff frame. The frame owns its lifetime,
        /// so this is an infinite state fact until a later completed frame omits it; do not pass a
        /// duration override here. Use <see cref="AddBuff"/> for timed Buffs.
        /// </summary>
        public bool SetBuff(
            ESBuffEnumKey buffKey,
            ESRuntimeTargetPack target = null,
            ESOpSupport sourceSupport = null)
        {
            if (!ESRuntimeDataGameCore.Buffs.TryGet((int)buffKey, out ESBuffRuntimeData runtimeData))
                return RejectBuffFrameWrite("未找到 Enum Buff Key：" + buffKey);

            return QueueBuffFrameWrite(runtimeData, (int)buffKey, target, sourceSupport);
        }

        /// <summary>
        /// Declares one String-keyed effect for the current Buff frame. String resolution occurs
        /// only while writing the frame, never in the active Buff Tick path.
        /// </summary>
        public bool SetBuff(
            string buffKey,
            ESRuntimeTargetPack target = null,
            ESOpSupport sourceSupport = null)
        {
            if (!TryGetRuntimeBuffKey(buffKey, out int runtimeKey)
                || !ESRuntimeDataGameCore.Buffs.TryGet(runtimeKey, out ESBuffRuntimeData runtimeData))
                return RejectBuffFrameWrite("未找到 String Buff Key：" + (buffKey ?? "<null>"));

            return QueueBuffFrameWrite(runtimeData, runtimeKey, target, sourceSupport);
        }

        /// <summary>Definition-reference counterpart of <see cref="SetBuff(ESBuffEnumKey,ESRuntimeTargetPack,ESOpSupport)"/>.</summary>
        public bool SetBuff(
            BuffDefinitionDataInfo definition,
            ESRuntimeTargetPack target = null,
            ESOpSupport sourceSupport = null)
        {
            int definitionKey = ESBuffSourceKeyUtility.ResolveDefinitionKey(definition);
            if (definition == null || definitionKey == 0)
                return RejectBuffFrameWrite("Buff Definition 未配置有效稳定 Key。");

            return QueueBuffFrameWrite(definition, definition.SharedData, definitionKey, target, sourceSupport);
        }

        /// <summary>
        /// Commits the current exact state-effect frame. If any key/configuration write was invalid,
        /// the previous frame remains untouched instead of being accidentally cleared.
        /// </summary>
        public bool EndBuffFrame()
        {
            if (buffFrameOwner == null)
            {
                Debug.LogError("[Buff] EndBuffFrame 前必须先 BeginBuffFrame。");
                return false;
            }

            object owner = buffFrameOwner;
            ulong frameNumber = buffFrameNumber;
            bool success = false;
            try
            {
                if (buffFrameWriteFailed || !TryValidateBuffFrameWrites())
                    return false;

                int writeCount = buffFrameWrites != null ? buffFrameWrites.Count : 0;
                for (int i = 0; i < writeCount; i++)
                {
                    if (!ApplyBuffFrameWrite(owner, frameNumber, buffFrameWrites[i]))
                    {
                        RollbackCreatedBuffFrameWrites(owner, frameNumber);
                        return false;
                    }
                }

                RemoveBuffFrameEntriesNotWritten(owner, frameNumber);
                success = true;
                return true;
            }
            finally
            {
                // The source's previous state is retained on failed validation/application. A
                // caller can safely start its next frame without a dangling transaction.
                buffFrameWrites?.Clear();
                buffFrameWriteFailed = false;
                buffFrameOwner = null;

                if (!success)
                    Debug.LogWarning("[Buff] BuffFrame 未提交；该来源上一份状态效果保持不变。");
            }
        }

        /// <summary>
        /// Drops uncommitted writes and keeps the owner's last committed state. Use this only when
        /// the caller aborts its own update before <see cref="EndBuffFrame"/> can run.
        /// </summary>
        public bool CancelBuffFrame()
        {
            if (buffFrameOwner == null)
                return false;

            buffFrameWrites?.Clear();
            buffFrameWriteFailed = false;
            buffFrameOwner = null;
            return true;
        }

        /// <summary>
        /// Immediately removes all state effects previously committed by this frame owner. Call it
        /// from a State/skill teardown when that owner will not submit another empty Buff frame.
        /// </summary>
        public int ClearBuffFrame(object owner)
        {
            if (owner == null)
                return 0;

            if (ReferenceEquals(buffFrameOwner, owner))
            {
                Debug.LogError("[Buff] 正在写入的 BuffFrame 不能直接 Clear；请先 EndBuffFrame，或提交空帧完成清理。");
                return 0;
            }

            int removed = 0;
            for (int i = activeBuffs.Count - 1; i >= 0; i--)
            {
                if (!activeBuffs[i].IsOwnedByBuffFrame(owner))
                    continue;

                RemoveBuffAt(i);
                removed++;
            }

            return removed;
        }

        private bool QueueBuffFrameWrite(
            ESBuffRuntimeData runtimeData,
            int definitionKey,
            ESRuntimeTargetPack target,
            ESOpSupport sourceSupport)
        {
            return runtimeData != null
                && QueueBuffFrameWrite(runtimeData.soSource, runtimeData.sharedData, definitionKey, target, sourceSupport);
        }

        private bool QueueBuffFrameWrite(
            BuffDefinitionDataInfo definition,
            BuffSharedData sharedData,
            int definitionKey,
            ESRuntimeTargetPack target,
            ESOpSupport sourceSupport)
        {
            if (buffFrameOwner == null)
                return RejectBuffFrameWrite("SetBuff 必须位于 BeginBuffFrame 与 EndBuffFrame 之间。");

            if (sharedData == null || definitionKey == 0)
                return RejectBuffFrameWrite("Buff 定义或稳定 Key 无效。");

            buffFrameWrites ??= new List<BuffFrameWrite>(4);
            BuffFrameWrite write = new BuffFrameWrite
            {
                definition = definition,
                sharedData = sharedData,
                definitionKey = definitionKey,
                target = target,
                sourceSupport = sourceSupport
            };

            // Input writes use last-value-wins semantics. A state frame uses the same rule: one
            // owner can own at most one runtime instance of one Buff definition.
            for (int i = 0; i < buffFrameWrites.Count; i++)
            {
                if (buffFrameWrites[i].definitionKey != definitionKey)
                    continue;

                buffFrameWrites[i] = write;
                return true;
            }

            buffFrameWrites.Add(write);
            return true;
        }

        private bool RejectBuffFrameWrite(string reason)
        {
            if (buffFrameOwner != null)
                buffFrameWriteFailed = true;

            Debug.LogError("[Buff] BuffFrame 写入被拒绝：" + reason);
            return false;
        }

        private bool TryValidateBuffFrameWrites()
        {
            int writeCount = buffFrameWrites != null ? buffFrameWrites.Count : 0;
            for (int i = 0; i < writeCount; i++)
            {
                if (!CanApplyBuffSharedData(buffFrameWrites[i].sharedData))
                    return false;
            }

            return true;
        }

        private bool ApplyBuffFrameWrite(object owner, ulong frameNumber, BuffFrameWrite write)
        {
            ESActiveBuffRuntime existing = FindBuffFrameEntry(owner, write.definitionKey);
            if (existing != null)
            {
                existing.MarkSeenByBuffFrame(frameNumber);
                return true;
            }

            if (!ResolveGroupConflict(write.sharedData, write.definitionKey))
                return false;

            ESActiveBuffRuntime buff = RentBuffRuntime();
            // Frame ownership controls this Buff's lifetime. It intentionally does not use the
            // authored duration or stack mode: repeated SetBuff calls are idempotent declarations,
            // not repeated gameplay applications.
            int sourceKey = ESBuffSourceKeyUtility.ResolveSourceKey(write.sharedData, write.sourceSupport);
            buff.Initialize(this, write.definition, write.sharedData, write.target, write.sourceSupport, null,
                -1f, 1, write.definitionKey, sourceKey, 1, owner, frameNumber);
            activeBuffs.Add(buff);
            if (!TryApplyRegisteredRuntimeBuff(buff))
            {
                ReturnFailedApplyBuffToPool(buff);
                return false;
            }

            NotifyBuffChanged(ESBuffChangedLink.From(buff, ESBuffChangeType.Applied));
            return true;
        }

        private ESActiveBuffRuntime FindBuffFrameEntry(object owner, int definitionKey)
        {
            for (int i = 0; i < activeBuffs.Count; i++)
            {
                ESActiveBuffRuntime buff = activeBuffs[i];
                if (buff.DefinitionKey == definitionKey && buff.IsOwnedByBuffFrame(owner))
                    return buff;
            }

            return null;
        }

        private void RemoveBuffFrameEntriesNotWritten(object owner, ulong frameNumber)
        {
            for (int i = activeBuffs.Count - 1; i >= 0; i--)
            {
                ESActiveBuffRuntime buff = activeBuffs[i];
                if (buff.IsOwnedByBuffFrame(owner) && buff.LastSeenFrame != frameNumber)
                    RemoveBuffAt(i);
            }
        }

        private void RollbackCreatedBuffFrameWrites(object owner, ulong frameNumber)
        {
            for (int i = activeBuffs.Count - 1; i >= 0; i--)
            {
                ESActiveBuffRuntime buff = activeBuffs[i];
                if (buff.IsOwnedByBuffFrame(owner) && buff.CreatedFrame == frameNumber)
                    RemoveBuffAt(i);
            }
        }

        #endregion

        private ESActiveBuffRuntime AddBuffInternal(
            BuffDefinitionDataInfo definition,
            BuffSharedData sharedData,
            ESRuntimeTargetPack target,
            ESOpSupport sourceSupport,
            Entity casterEntity,
            Item sourceItem,
            StateBase stateTimeSource,
            int customSourceId,
            float durationOverride,
            int stackDelta,
            int initialLevel = 1)
        {
            if (!CanApplyBuffSharedData(sharedData))
                return null;

            EnsureBuffOpSupport();

            int definitionKey = ESBuffSourceKeyUtility.ResolveDefinitionKey(definition, sharedData);
            int sourceKey = ESBuffSourceKeyUtility.ResolveSourceKey(sharedData, sourceSupport, casterEntity, sourceItem, customSourceId);
            if (definitionKey == 0)
                return null;

            if (!ResolveGroupConflict(sharedData, definitionKey))
                return null;

            ESActiveBuffRuntime mergeTarget = FindMergeTarget(sharedData, definitionKey, sourceKey);
            if (mergeTarget != null && sharedData.stackMode != ESBuffStackMode.IndependentInstance)
            {
                mergeTarget.AddStackOrRefresh(durationOverride >= 0f ? durationOverride : sharedData.duration, Mathf.Max(1, stackDelta));
                return mergeTarget;
            }

            ESActiveBuffRuntime buff = RentBuffRuntime();
            buff.Initialize(this, definition, sharedData, target, sourceSupport, stateTimeSource,
                durationOverride >= 0f ? durationOverride : sharedData.duration, Mathf.Max(1, stackDelta),
                definitionKey, sourceKey, initialLevel);
            // Keep the established Buff contract: OnApply operations can query their own active
            // Buff. TryApply rolls back its owned resources on failure; this path then removes the
            // provisional list entry before the runtime is returned to the pool.
            activeBuffs.Add(buff);
            if (!TryApplyRegisteredRuntimeBuff(buff))
            {
                ReturnFailedApplyBuffToPool(buff);
                return null;
            }

            NotifyBuffChanged(ESBuffChangedLink.From(buff, ESBuffChangeType.Applied));
            return ContainsActiveBuff(buff) ? buff : null;
        }

        private bool CanApplyBuffSharedData(BuffSharedData sharedData)
        {
            if (sharedData == null)
                return false;

            if (!sharedData.TryValidateGameTagConfiguration(out string gameTagConfigurationError))
            {
                Debug.LogError("[BuffTag] 已拒绝无效的 Buff GameTag 配置：" + gameTagConfigurationError);
                return false;
            }

            if (!sharedData.TryGetApplyTargetTagCondition(out ESTagConditionRuntime applyCondition, out string requirementError))
            {
                Debug.LogError("[BuffTag] 已拒绝无效的施加目标 Tag 条件：" + requirementError);
                return false;
            }

            if (!applyCondition.IsEmpty
                && (MyCore == null
                    || !MyCore.TryMatchesTagCondition(applyCondition, out bool applies, out requirementError)
                    || !applies))
                return false;

            return true;
        }

        public bool RemoveBuff(BuffDefinitionDataInfo definition)
        {
            int runtimeKey = ESBuffSourceKeyUtility.ResolveDefinitionKey(definition);
            return RemoveBuffByKey(runtimeKey);
        }

        public bool RemoveBuff(ESBuffEnumKey buffKey)
        {
            return RemoveBuffByKey((ushort)buffKey);
        }

        public bool RemoveBuffByStringKey(string stringKey)
        {
            return TryGetRuntimeBuffKey(stringKey, out int runtimeKey) && RemoveBuffByKey(runtimeKey);
        }

        public bool RemoveBuffByKey(int runtimeKey)
        {
            for (int i = activeBuffs.Count - 1; i >= 0; i--)
            {
                ESActiveBuffRuntime buff = activeBuffs[i];
                if (buff.DefinitionKey == runtimeKey)
                {
                    RemoveBuffAt(i);
                    return true;
                }
            }

            return false;
        }

        public int RemoveAllBuff(BuffDefinitionDataInfo definition)
        {
            int runtimeKey = ESBuffSourceKeyUtility.ResolveDefinitionKey(definition);
            return RemoveAllBuffByKey(runtimeKey);
        }

        public int RemoveAllBuff(ESBuffEnumKey buffKey)
        {
            return RemoveAllBuffByKey((ushort)buffKey);
        }

        public int RemoveAllBuffByStringKey(string stringKey)
        {
            return TryGetRuntimeBuffKey(stringKey, out int runtimeKey) ? RemoveAllBuffByKey(runtimeKey) : 0;
        }

        public int RemoveAllBuffByKey(int runtimeKey)
        {
            int removed = 0;
            int i = activeBuffs.Count - 1;
            while (i >= 0)
            {
                ESActiveBuffRuntime buff = activeBuffs[i];
                if (buff.DefinitionKey == runtimeKey)
                {
                    RemoveBuffAt(i);
                    removed++;
                    if (i >= activeBuffs.Count)
                        i = activeBuffs.Count - 1;
                    continue;
                }

                i--;
            }

            return removed;
        }

        public int RemoveAllBuffBySource(int sourceKey)
        {
            if (sourceKey == 0)
                return 0;

            int removed = 0;
            int i = activeBuffs.Count - 1;
            while (i >= 0)
            {
                ESActiveBuffRuntime buff = activeBuffs[i];
                if (buff.variableData.sourceKey == sourceKey)
                {
                    RemoveBuffAt(i);
                    removed++;
                    if (i >= activeBuffs.Count)
                        i = activeBuffs.Count - 1;
                    continue;
                }

                i--;
            }

            return removed;
        }

        public bool HasBuff(BuffDefinitionDataInfo definition)
        {
            return FindBuffByKey(ESBuffSourceKeyUtility.ResolveDefinitionKey(definition)) != null;
        }

        public bool HasBuff(ESBuffEnumKey buffKey)
        {
            return FindBuffByKey((ushort)buffKey) != null;
        }

        public bool HasBuffByStringKey(string stringKey)
        {
            return TryGetRuntimeBuffKey(stringKey, out int runtimeKey) && FindBuffByKey(runtimeKey) != null;
        }

        public int CountBuff(BuffDefinitionDataInfo definition)
        {
            return CountBuffByKey(ESBuffSourceKeyUtility.ResolveDefinitionKey(definition));
        }

        public int CountBuff(ESBuffEnumKey buffKey)
        {
            return CountBuffByKey((ushort)buffKey);
        }

        public int CountBuffByStringKey(string stringKey)
        {
            return TryGetRuntimeBuffKey(stringKey, out int runtimeKey) ? CountBuffByKey(runtimeKey) : 0;
        }

        private static bool TryGetRuntimeBuffKey(string stringKey, out int runtimeKey)
        {
            return ESRuntimeDataGameCore.Buffs.TryGetRuntimeKey(stringKey, out runtimeKey);
        }

        private static bool TryGetRuntimeBuffData(string stringKey, out ESBuffRuntimeData runtimeData)
        {
            runtimeData = null;
            return TryGetRuntimeBuffKey(stringKey, out int runtimeKey)
                && ESRuntimeDataGameCore.Buffs.TryGet(runtimeKey, out runtimeData);
        }

        public int CountBuffByKey(int runtimeKey)
        {
            int count = 0;
            for (int i = 0; i < activeBuffs.Count; i++)
            {
                ESActiveBuffRuntime buff = activeBuffs[i];
                if (buff.DefinitionKey == runtimeKey)
                    count += Mathf.Max(1, buff.variableData.stackCount);
            }

            return count;
        }

        public void ClearAllBuffs()
        {
            while (activeBuffs.Count > 0)
                RemoveBuffAt(activeBuffs.Count - 1);
        }

        public void ReturnAllBuffsToPool(bool triggerRemoveOps = true)
        {
            while (activeBuffs.Count > 0)
                ReturnActiveBuffAtToPool(activeBuffs.Count - 1, triggerRemoveOps);

            for (int i = inactiveBuffs.Count - 1; i >= 0; i--)
                inactiveBuffs[i].TryAutoPushedToPool();
            inactiveBuffs.Clear();
        }

        private bool ResolveGroupConflict(BuffSharedData incomingSharedData, int incomingKey)
        {
            if (incomingSharedData.groupConflictMode == ESBuffGroupConflictMode.None || string.IsNullOrEmpty(incomingSharedData.buffGroup))
                return true;

            int i = activeBuffs.Count - 1;
            while (i >= 0)
            {
                ESActiveBuffRuntime existing = activeBuffs[i];
                if (existing.DefinitionKey == incomingKey || existing.GroupKey != incomingSharedData.buffGroup)
                {
                    i--;
                    continue;
                }

                switch (incomingSharedData.groupConflictMode)
                {
                    case ESBuffGroupConflictMode.ReplaceWeakerInGroup:
                        if (incomingSharedData.strength > existing.Strength)
                        {
                            RemoveBuffAt(i);
                            if (i >= activeBuffs.Count)
                                i = activeBuffs.Count - 1;
                            continue;
                        }
                        else
                            return false;
                    case ESBuffGroupConflictMode.ReplaceLowerOrEqualInGroup:
                        if (incomingSharedData.strength >= existing.Strength)
                        {
                            RemoveBuffAt(i);
                            if (i >= activeBuffs.Count)
                                i = activeBuffs.Count - 1;
                            continue;
                        }
                        else
                            return false;
                    case ESBuffGroupConflictMode.RejectIfWeakerInGroup:
                        if (incomingSharedData.strength < existing.Strength)
                            return false;
                        break;
                }

                i--;
            }

            return true;
        }

        private ESActiveBuffRuntime FindMergeTarget(BuffSharedData sharedData, int definitionKey, int sourceKey)
        {
            if (sharedData.stackMode == ESBuffStackMode.IndependentInstance)
                return null;

            for (int i = 0; i < activeBuffs.Count; i++)
            {
                ESActiveBuffRuntime buff = activeBuffs[i];
                if (buff.CanMergeWith(definitionKey, sourceKey))
                    return buff;
            }

            return null;
        }

        private ESActiveBuffRuntime FindBuffByKey(int runtimeKey)
        {
            for (int i = 0; i < activeBuffs.Count; i++)
            {
                ESActiveBuffRuntime buff = activeBuffs[i];
                if (buff.DefinitionKey == runtimeKey)
                    return buff;
            }

            return null;
        }

        private void TickActiveBuffs(float deltaTime)
        {
            int i = activeBuffs.Count - 1;
            while (i >= 0)
            {
                ESActiveBuffRuntime buff = activeBuffs[i];
                if (buff.Tick(deltaTime))
                {
                    RemoveBuffAt(i);
                    if (i >= activeBuffs.Count)
                        i = activeBuffs.Count - 1;
                    continue;
                }

                i--;
            }
        }

        private ESActiveBuffRuntime RentBuffRuntime()
        {
            int last = inactiveBuffs.Count - 1;
            if (last >= 0)
            {
                ESActiveBuffRuntime buff = inactiveBuffs[last];
                inactiveBuffs.RemoveAt(last);
                return buff;
            }

            return ESActiveBuffRuntime.Pool.GetInPool();
        }

        private void ReturnFailedApplyBuffToPool(ESActiveBuffRuntime buff)
        {
            UnregisterRuntimeBuff(buff);
            for (int i = activeBuffs.Count - 1; i >= 0; i--)
            {
                if (!ReferenceEquals(activeBuffs[i], buff))
                    continue;

                int last = activeBuffs.Count - 1;
                if (i != last)
                    activeBuffs[i] = activeBuffs[last];
                activeBuffs.RemoveAt(last);
                buff.Deactivate(false);
                buff.TryAutoPushedToPool();
                return;
            }
        }

        private bool ContainsActiveBuff(ESActiveBuffRuntime buff)
        {
            for (int i = 0; i < activeBuffs.Count; i++)
            {
                if (ReferenceEquals(activeBuffs[i], buff))
                    return true;
            }

            return false;
        }

        private void RemoveBuffAt(int index)
        {
            ESActiveBuffRuntime buff = activeBuffs[index];
            ESBuffChangedLink change = ESBuffChangedLink.From(buff, ESBuffChangeType.Removed);
            int last = activeBuffs.Count - 1;
            if (index != last)
                activeBuffs[index] = activeBuffs[last];

            activeBuffs.RemoveAt(last);
            UnregisterRuntimeBuff(buff);
            buff.Deactivate(true);
            inactiveBuffs.Add(buff);
            NotifyBuffChanged(change);
        }

        private void ReturnActiveBuffAtToPool(int index, bool triggerRemoveOps)
        {
            ESActiveBuffRuntime buff = activeBuffs[index];
            ESBuffChangedLink change = ESBuffChangedLink.From(buff, ESBuffChangeType.Removed);
            int last = activeBuffs.Count - 1;
            if (index != last)
                activeBuffs[index] = activeBuffs[last];

            activeBuffs.RemoveAt(last);
            UnregisterRuntimeBuff(buff);
            buff.Deactivate(triggerRemoveOps);
            buff.TryAutoPushedToPool();
            NotifyBuffChanged(change);
        }

        private bool TryRegisterRuntimeBuff(ESActiveBuffRuntime buff)
        {
            if (buff == null || MyCore == null)
                return false;
            if (ESRuntimeDataModule.BuffInstanceTable.TryAddInstance(
                    buff,
                    buff.DefinitionKey,
                    MyCore.GetInstanceID(),
                    out ESInstanceHandle handle))
            {
                buff.RuntimeInstanceHandle = handle;
                return true;
            }

            Debug.LogError("[Buff] Buff 实例表容量不足或身份无效，已拒绝激活。", MyCore);
            return false;
        }

        private bool TryApplyRegisteredRuntimeBuff(ESActiveBuffRuntime buff)
        {
            if (!TryRegisterRuntimeBuff(buff))
                return false;

            ESInstanceHandle registeredHandle = buff.RuntimeInstanceHandle;
            if (buff.TryApply() && ContainsActiveBuff(buff))
                return true;

            if (registeredHandle.IsValid)
                ESRuntimeDataModule.BuffInstanceTable.TryRemove(registeredHandle, out _);
            buff.RuntimeInstanceHandle = default;
            return false;
        }

        private static void UnregisterRuntimeBuff(ESActiveBuffRuntime buff)
        {
            if (buff == null)
                return;
            ESInstanceHandle handle = buff.RuntimeInstanceHandle;
            if (handle.IsValid)
                ESRuntimeDataModule.BuffInstanceTable.TryRemove(handle, out _);
            buff.RuntimeInstanceHandle = default;
        }

        internal void NotifyBuffRefreshed(ESActiveBuffRuntime buff)
        {
            if (buff == null || !ContainsActiveBuff(buff))
                return;

            NotifyBuffChanged(ESBuffChangedLink.From(buff, ESBuffChangeType.Refreshed));
        }

        private void NotifyBuffChanged(ESBuffChangedLink change)
        {
            if (buffChangedLinks?.SubscriberCount > 0)
                buffChangedLinks.SendLink(change);
        }
    }

    public enum ESBuffChangeType : byte
    {
        Applied,
        Refreshed,
        Removed
    }

    /// <summary>
    /// Read-only in-process Buff lifecycle notification. Runtime keys are local acceleration IDs,
    /// so this payload must not be used as a save or network protocol.
    /// </summary>
    public readonly struct ESBuffChangedLink
    {
        public ESBuffChangeType ChangeType { get; }
        public int DefinitionRuntimeKey { get; }
        public int SourceKey { get; }
        public int StackCount { get; }
        public int Level { get; }
        public float RemainingTime { get; }
        public float ElapsedTime { get; }
        public bool IsInfinite { get; }

        private ESBuffChangedLink(
            ESBuffChangeType changeType,
            int definitionRuntimeKey,
            int sourceKey,
            int stackCount,
            int level,
            float remainingTime,
            float elapsedTime)
        {
            ChangeType = changeType;
            DefinitionRuntimeKey = definitionRuntimeKey;
            SourceKey = sourceKey;
            StackCount = stackCount;
            Level = level;
            RemainingTime = remainingTime;
            ElapsedTime = elapsedTime;
            IsInfinite = remainingTime < 0f;
        }

        internal static ESBuffChangedLink From(ESActiveBuffRuntime buff, ESBuffChangeType changeType)
        {
            return new ESBuffChangedLink(
                changeType,
                buff.DefinitionKey,
                buff.SourceKey,
                buff.StackCount,
                buff.Level,
                buff.RemainingTime,
                buff.ElapsedTime);
        }
    }

}
