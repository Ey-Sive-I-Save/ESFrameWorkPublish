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

        [ShowInInspector, ReadOnly, LabelText("Float ValueChange")]
        private readonly Dictionary<string, ESFloatValueChangeSet> floatStats = new Dictionary<string, ESFloatValueChangeSet>(16);

        [ShowInInspector, ReadOnly, LabelText("Permit ValueChange")]
        private readonly Dictionary<string, ESPermitSet> permitStats = new Dictionary<string, ESPermitSet>(16);

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

        public ESFloatValueChangeSet GetFloatStat(string key, float baseValue = 0f)
        {
            if (string.IsNullOrEmpty(key))
                return null;

            if (!floatStats.TryGetValue(key, out ESFloatValueChangeSet set))
            {
                set = new ESFloatValueChangeSet(baseValue);
                floatStats.Add(key, set);
            }

            return set;
        }

        public ESPermitSet GetPermit(string key, bool fallbackValue = true)
        {
            if (string.IsNullOrEmpty(key))
                return null;

            if (!permitStats.TryGetValue(key, out ESPermitSet set))
            {
                set = new ESPermitSet(fallbackValue);
                permitStats.Add(key, set);
            }

            return set;
        }

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

        public ESActiveBuffRuntime AddBuff(BuffDefinitionDataInfo definition, ESRuntimeTargetPack target = null, ESOpSupport sourceSupport = null, float durationOverride = -1f)
        {
            return AddBuffInternal(definition, definition != null ? definition.SharedData : null, target, sourceSupport, null, null, null, 0, durationOverride, 1);
        }

        public ESActiveBuffRuntime AddBuffByStateTime(BuffDefinitionDataInfo definition, StateBase stateTimeSource, ESRuntimeTargetPack target = null, ESOpSupport sourceSupport = null, float durationOverride = -1f)
        {
            return AddBuffInternal(definition, definition != null ? definition.SharedData : null, target, sourceSupport, null, null, stateTimeSource, 0, durationOverride, 1);
        }

        public ESActiveBuffRuntime AddBuff(BuffSharedData sharedData, ESRuntimeTargetPack target = null, ESOpSupport sourceSupport = null, float durationOverride = -1f)
        {
            return AddBuffInternal(null, sharedData, target, sourceSupport, null, null, null, 0, durationOverride, 1);
        }

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
            int stackDelta)
        {
            if (sharedData == null)
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
            buff.Initialize(this, definition, sharedData, target, sourceSupport, stateTimeSource, durationOverride >= 0f ? durationOverride : sharedData.duration, Mathf.Max(1, stackDelta), definitionKey, sourceKey);
            activeBuffs.Add(buff);
            buff.Apply();
            return buff;
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
            ESRuntimeDataModule runtimeData = ESGameManager.RuntimeData;
            if (runtimeData != null && runtimeData.Buffs.TryGetRuntimeKey(stringKey, out runtimeKey))
                return true;

            runtimeKey = 0;
            return false;
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

        private void RemoveBuffAt(int index)
        {
            ESActiveBuffRuntime buff = activeBuffs[index];
            int last = activeBuffs.Count - 1;
            if (index != last)
                activeBuffs[index] = activeBuffs[last];

            activeBuffs.RemoveAt(last);
            buff.Deactivate(true);
            inactiveBuffs.Add(buff);
        }

        private void ReturnActiveBuffAtToPool(int index, bool triggerRemoveOps)
        {
            ESActiveBuffRuntime buff = activeBuffs[index];
            int last = activeBuffs.Count - 1;
            if (index != last)
                activeBuffs[index] = activeBuffs[last];

            activeBuffs.RemoveAt(last);
            buff.Deactivate(triggerRemoveOps);
            buff.TryAutoPushedToPool();
        }
    }
}
