using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [Serializable, TypeRegistryItem("Buff域")]
    public class EntityBuffDomain : Domain<Entity, EntityBuffModuleBase>, IESEffectLeaseOwner
    {
        private struct ValueChangeEffectSlot
        {
            public int generation;
            public bool isActive;
        }

        [TitleGroup("运行支持", Alignment = TitleAlignments.Left)]
        [NonSerialized, ShowInInspector, Sirenix.OdinInspector.ReadOnly, LabelText("Buff域 OpSupport")]
        public ESOpSupport opSupport;

        [TitleGroup("运行时", Alignment = TitleAlignments.Left)]
        [ShowInInspector, ReadOnly, LabelText("运行中 Buff")]
        private readonly List<ESActiveBuffRuntime> activeBuffs = new List<ESActiveBuffRuntime>(8);

        [ShowInInspector, ReadOnly, LabelText("静默 Buff")]
        private readonly List<ESActiveBuffRuntime> inactiveBuffs = new List<ESActiveBuffRuntime>(8);

        // Fixed character slots are compact reference arrays. A resolver is materialized only when a
        // Buff/code modifier actually targets that slot; KCC can read an unmodified base value directly.
        [ShowInInspector, ReadOnly, LabelText("角色 Float ValueChange")]
        private readonly ESFloatValueChangeSet[] characterFloatStats = new ESFloatValueChangeSet[(int)ESCharacterFloatAttributeId.Count];

        [ShowInInspector, ReadOnly, LabelText("角色 Permit ValueChange")]
        private readonly ESPermitSet[] characterPermitStats = new ESPermitSet[(int)ESCharacterPermitAttributeId.Count];

        // Compiled with the definition table, then read directly by KCC. These arrays deliberately
        // replace per-frame Catalog/Dictionary lookups for fixed character slots.
        private readonly float[] characterFloatDefinitionBases = new float[(int)ESCharacterFloatAttributeId.Count];
        private readonly byte[] characterFloatHasDefinitionBase = new byte[(int)ESCharacterFloatAttributeId.Count];
        private readonly float[] characterFloatDefinitionMinimums = new float[(int)ESCharacterFloatAttributeId.Count];
        private readonly float[] characterFloatDefinitionMaximums = new float[(int)ESCharacterFloatAttributeId.Count];
        private readonly float[] characterFloatExplicitBases = new float[(int)ESCharacterFloatAttributeId.Count];
        private readonly byte[] characterFloatHasExplicitBase = new byte[(int)ESCharacterFloatAttributeId.Count];
        private readonly byte[] characterPermitDefinitionFallbacks = new byte[(int)ESCharacterPermitAttributeId.Count];
        private readonly byte[] characterPermitHasDefinitionFallback = new byte[(int)ESCharacterPermitAttributeId.Count];
        private readonly byte[] characterPermitExplicitFallbacks = new byte[(int)ESCharacterPermitAttributeId.Count];
        private readonly byte[] characterPermitHasExplicitFallback = new byte[(int)ESCharacterPermitAttributeId.Count];

        // Optional attributes remain sparse, but are always indexed by an already-resolved
        // process-local RuntimeKey. Their StringKey never enters a per-instance dictionary.
        [ShowInInspector, ReadOnly, LabelText("稀疏 Float ValueChange")]
        private readonly Dictionary<int, ESFloatValueChangeSet> sparseFloatStats = new Dictionary<int, ESFloatValueChangeSet>(16);

        [ShowInInspector, ReadOnly, LabelText("稀疏 Permit ValueChange")]
        private readonly Dictionary<int, ESPermitSet> sparsePermitStats = new Dictionary<int, ESPermitSet>(16);

        // Explicit bases belong to business runtime state, not to a modifier Set. Sparse RuntimeKey
        // entries are discarded on catalog rebind; fixed character ids remain stable for the entity.
        private readonly Dictionary<int, float> sparseFloatExplicitBases = new Dictionary<int, float>(8);
        private readonly Dictionary<int, bool> sparsePermitExplicitFallbacks = new Dictionary<int, bool>(8);

        [NonSerialized] private ESSuperAttributeTable superAttributeTable;
        [NonSerialized] private ESSuperAttributeCatalog superAttributeCatalog;
        [NonSerialized] private string superAttributeCatalogError;
        private readonly List<ValueChangeEffectSlot> valueChangeEffectSlots = new List<ValueChangeEffectSlot>(8);
        private readonly List<int> freeValueChangeEffectSlots = new List<int>(8);
        private int activeValueChangeEffectCount;
        private bool isValueChangeResetting;

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
        public int ActiveValueChangeEffectCount => activeValueChangeEffectCount;

        /// <summary>
        /// Binds and compiles the owning attribute definition table. A catalog transition invalidates
        /// process-local RuntimeKeys, so it is only valid before this domain accepts modifiers.
        /// </summary>
        public void BindSuperAttributeTable(ESSuperAttributeTable table)
        {
            // A disabled table contributes no schema/defaults. Fixed built-in slots still work from
            // their caller-supplied base values, while unregistered sparse keys remain unavailable.
            ESSuperAttributeTable effectiveTable = table != null && table.enabled ? table : null;
            if (ReferenceEquals(superAttributeTable, effectiveTable) && superAttributeCatalog != null)
                return;

            if (activeValueChangeEffectCount != 0)
            {
                throw new InvalidOperationException(
                    "Cannot bind a different AttributeTable while ValueChange effects are active. Release the owning EffectLease first.");
            }

            if (superAttributeCatalog != null || HasMaterializedValueChangeSets())
                ClearValueChanges();

            if (!ReferenceEquals(superAttributeTable, effectiveTable))
            {
                sparseFloatExplicitBases.Clear();
                sparsePermitExplicitFallbacks.Clear();
            }

            superAttributeTable = effectiveTable;
            superAttributeCatalog = null;
            superAttributeCatalogError = null;
            if (effectiveTable != null && !effectiveTable.TryBuildCatalog(out superAttributeCatalog, out superAttributeCatalogError))
                superAttributeCatalog = null;

            RebuildFixedSlotDefinitionCache();
        }

        public ESSuperAttributeCatalog SuperAttributeCatalog => superAttributeCatalog;
        public string SuperAttributeCatalogError => superAttributeCatalogError;

        /// <summary>
        /// Creates one runtime-only ownership boundary for modifiers. The returned lease is the
        /// only supported way for a producer to end that boundary; its owner id is used internally
        /// by Set bulk release and is never serialized, replicated or treated as a business key.
        /// </summary>
        public ESEffectLease CreateValueChangeEffectLease(out int ownerId)
        {
            if (isValueChangeResetting)
                throw new InvalidOperationException("Cannot create a ValueChange EffectLease while the domain is resetting or rebinding.");

            int slotIndex;
            ValueChangeEffectSlot slot;
            int freeLast = freeValueChangeEffectSlots.Count - 1;
            if (freeLast >= 0)
            {
                slotIndex = freeValueChangeEffectSlots[freeLast];
                freeValueChangeEffectSlots.RemoveAt(freeLast);
                slot = valueChangeEffectSlots[slotIndex];
            }
            else
            {
                slotIndex = valueChangeEffectSlots.Count;
                slot = default;
                valueChangeEffectSlots.Add(slot);
            }

            if (slot.generation == int.MaxValue)
                throw new InvalidOperationException("Entity ValueChange effect generation exhausted.");

            slot.generation++;
            slot.isActive = true;
            valueChangeEffectSlots[slotIndex] = slot;
            activeValueChangeEffectCount++;
            ownerId = slotIndex + 1;
            return new ESEffectLease(this, slotIndex, slot.generation);
        }

        /// <summary>
        /// Lease callback. A stale or copied lease cannot release a newer effect slot because the
        /// generation must match. All Tokens owned by this effect are released across every Set.
        /// </summary>
        public bool ReleaseEffect(int effectSlot, int generation)
        {
            if ((uint)effectSlot >= (uint)valueChangeEffectSlots.Count)
                return false;

            ValueChangeEffectSlot slot = valueChangeEffectSlots[effectSlot];
            if (!slot.isActive || slot.generation != generation)
                return false;

            slot.isActive = false;
            valueChangeEffectSlots[effectSlot] = slot;
            try
            {
                // Keep this slot unavailable while Set notifications run. A listener may create a
                // new effect, but it must receive a different OwnerId until this release completes.
                ReleaseAllValueChangesByOwner(effectSlot + 1);
            }
            finally
            {
                activeValueChangeEffectCount--;
                freeValueChangeEffectSlots.Add(effectSlot);
            }
            return true;
        }

        public ESFloatValueChangeSet GetFloatStat(string key, float baseValue = 0f)
        {
            return GetFloatStat(0, key, baseValue);
        }

        /// <summary>Stable-key boundary. Both aliases must resolve to the same attribute definition.</summary>
        public ESFloatValueChangeSet GetFloatStat(ushort enumKey, string key, float baseValue = 0f)
        {
            if (TryResolveCharacterFloatSlot(enumKey, key, out ESCharacterFloatAttributeId characterId))
                return GetCharacterFloatStat(characterId, baseValue);

            return TryResolveFloatRuntimeKey(enumKey, key, out int runtimeKey)
                ? GetFloatStat(runtimeKey, baseValue)
                : null;
        }

        /// <summary>Runtime path for an already resolved catalog key.</summary>
        public ESFloatValueChangeSet GetFloatStat(int runtimeKey, float baseValue = 0f)
        {
            if (superAttributeCatalog == null
                || !superAttributeCatalog.TryGetFloatDefinition(runtimeKey, out ESSuperFloatAttributeDefinition definition))
                return null;

            if (definition.storagePolicy == ESKeyStoragePolicy.HotSlot)
            {
                return ESCharacterAttributeCatalog.TryGetFloatId(definition.enumKey, out ESCharacterFloatAttributeId characterId)
                    ? GetCharacterFloatStat(characterId, baseValue)
                    : null;
            }

            float resolvedBaseValue = ResolveSparseFloatBase(runtimeKey, baseValue);
            if (!sparseFloatStats.TryGetValue(runtimeKey, out ESFloatValueChangeSet set))
            {
                set = new ESFloatValueChangeSet(resolvedBaseValue);
                set.SetBounds(definition.minValue, definition.maxValue);
                sparseFloatStats.Add(runtimeKey, set);
            }
            else if (set.BaseValue != resolvedBaseValue)
            {
                set.BaseValue = resolvedBaseValue;
            }

            return set;
        }

        /// <summary>Gets or creates the modifier resolver for a fixed character float slot.</summary>
        public ESFloatValueChangeSet GetCharacterFloatStat(ESCharacterFloatAttributeId id, float fallbackBaseValue = 0f)
        {
            if (!ESCharacterAttributeCatalog.IsValid(id))
                return null;

            int index = (int)id;
            float resolvedBaseValue = ResolveCharacterFloatBase(id, fallbackBaseValue);
            ESFloatValueChangeSet set = characterFloatStats[index];
            if (set == null)
            {
                set = new ESFloatValueChangeSet(resolvedBaseValue);
                set.SetBounds(characterFloatDefinitionMinimums[index], characterFloatDefinitionMaximums[index]);
                characterFloatStats[index] = set;
            }
            else if (set.BaseValue != resolvedBaseValue)
            {
                set.BaseValue = resolvedBaseValue;
            }

            return set;
        }

        /// <summary>Returns an existing float stat without creating an empty ValueChange set.</summary>
        public bool TryGetFloatStat(string key, out ESFloatValueChangeSet set)
        {
            return TryGetFloatStat(0, key, out set);
        }

        public bool TryGetFloatStat(ushort enumKey, string key, out ESFloatValueChangeSet set)
        {
            if (TryResolveCharacterFloatSlot(enumKey, key, out ESCharacterFloatAttributeId characterId))
            {
                set = characterFloatStats[(int)characterId];
                return set != null;
            }

            if (TryResolveFloatRuntimeKey(enumKey, key, out int runtimeKey))
                return TryGetFloatStat(runtimeKey, out set);

            set = null;
            return false;
        }

        public bool TryGetFloatStat(int runtimeKey, out ESFloatValueChangeSet set)
        {
            if (superAttributeCatalog != null
                && superAttributeCatalog.TryGetFloatDefinition(runtimeKey, out ESSuperFloatAttributeDefinition definition)
                && definition.storagePolicy == ESKeyStoragePolicy.HotSlot
                && ESCharacterAttributeCatalog.TryGetFloatId(definition.enumKey, out ESCharacterFloatAttributeId characterId))
            {
                set = characterFloatStats[(int)characterId];
                return set != null;
            }

            return sparseFloatStats.TryGetValue(runtimeKey, out set);
        }

        /// <summary>Gets the resolved float value, creating the stat with <paramref name="baseValue"/> when needed.</summary>
        public float GetFloatStatValue(string key, float baseValue = 0f)
        {
            return GetFloatStatValue(0, key, baseValue);
        }

        public float GetFloatStatValue(ushort enumKey, string key, float baseValue = 0f)
        {
            if (TryResolveCharacterFloatSlot(enumKey, key, out ESCharacterFloatAttributeId characterId))
                return GetCharacterFloatStatValue(characterId, baseValue);

            if (!TryResolveFloatRuntimeKey(enumKey, key, out int runtimeKey))
                return baseValue;

            ESFloatValueChangeSet set = GetFloatStat(runtimeKey, baseValue);
            return set != null ? set.Value : baseValue;
        }

        /// <summary>
        /// Fixed-slot read for KCC and combat hot paths. It performs only array access and scalar work;
        /// no string lookup, Dictionary lookup, or resolver allocation occurs for an untouched slot.
        /// </summary>
        public float GetCharacterFloatStatValue(ESCharacterFloatAttributeId id, float fallbackBaseValue = 0f)
        {
            if (!ESCharacterAttributeCatalog.IsValid(id))
                return fallbackBaseValue;

            float resolvedBaseValue = ResolveCharacterFloatBase(id, fallbackBaseValue);
            ESFloatValueChangeSet set = characterFloatStats[(int)id];
            if (set == null)
                return ClampCharacterFloatValue(id, resolvedBaseValue);

            if (set.BaseValue != resolvedBaseValue)
                set.BaseValue = resolvedBaseValue;
            return set.Value;
        }

        /// <summary>Sets a runtime business base without affecting active modifiers.</summary>
        public void SetFloatStatBaseValue(string key, float baseValue)
        {
            SetFloatStatBaseValue(0, key, baseValue);
        }

        public void SetFloatStatBaseValue(ushort enumKey, string key, float baseValue)
        {
            ValidateFiniteFloatBase(baseValue);

            if (TryResolveCharacterFloatSlot(enumKey, key, out ESCharacterFloatAttributeId characterId))
            {
                SetCharacterFloatStatBaseValue(characterId, baseValue);
                return;
            }

            if (!TryResolveFloatRuntimeKey(enumKey, key, out int runtimeKey))
                return;

            sparseFloatExplicitBases[runtimeKey] = baseValue;
            if (sparseFloatStats.TryGetValue(runtimeKey, out ESFloatValueChangeSet set))
                set.BaseValue = baseValue;
        }

        /// <summary>Sets a fixed runtime base without materializing a modifier resolver.</summary>
        public void SetCharacterFloatStatBaseValue(ESCharacterFloatAttributeId id, float baseValue)
        {
            ValidateFiniteFloatBase(baseValue);

            if (!ESCharacterAttributeCatalog.IsValid(id))
                return;

            int index = (int)id;
            characterFloatExplicitBases[index] = baseValue;
            characterFloatHasExplicitBase[index] = 1;
            ESFloatValueChangeSet set = characterFloatStats[index];
            if (set != null)
                set.BaseValue = baseValue;
        }

        /// <summary>Sets a permit's fallback value without changing any active permit modifiers.</summary>
        public void SetPermitFallbackValue(string key, bool fallbackValue)
        {
            SetPermitFallbackValue(0, key, fallbackValue);
        }

        public void SetPermitFallbackValue(ushort enumKey, string key, bool fallbackValue)
        {
            if (TryResolveCharacterPermitSlot(enumKey, key, out ESCharacterPermitAttributeId characterId))
            {
                SetCharacterPermitFallbackValue(characterId, fallbackValue);
                return;
            }

            if (!TryResolvePermitRuntimeKey(enumKey, key, out int runtimeKey))
                return;

            sparsePermitExplicitFallbacks[runtimeKey] = fallbackValue;
            if (sparsePermitStats.TryGetValue(runtimeKey, out ESPermitSet set))
                set.FallbackValue = fallbackValue;
        }

        /// <summary>Sets a fixed permit fallback without materializing a resolver.</summary>
        public void SetCharacterPermitFallbackValue(ESCharacterPermitAttributeId id, bool fallbackValue)
        {
            if (!ESCharacterAttributeCatalog.IsValid(id))
                return;

            int index = (int)id;
            characterPermitExplicitFallbacks[index] = fallbackValue ? (byte)1 : (byte)0;
            characterPermitHasExplicitFallback[index] = 1;
            ESPermitSet set = characterPermitStats[index];
            if (set != null)
                set.FallbackValue = fallbackValue;
        }

        public ESPermitSet GetPermit(string key, bool fallbackValue = true)
        {
            return GetPermit(0, key, fallbackValue);
        }

        /// <summary>Stable-key boundary. Both aliases must resolve to the same permit definition.</summary>
        public ESPermitSet GetPermit(ushort enumKey, string key, bool fallbackValue = true)
        {
            if (TryResolveCharacterPermitSlot(enumKey, key, out ESCharacterPermitAttributeId characterId))
                return GetCharacterPermit(characterId, fallbackValue);

            return TryResolvePermitRuntimeKey(enumKey, key, out int runtimeKey)
                ? GetPermit(runtimeKey, fallbackValue)
                : null;
        }

        /// <summary>Runtime path for an already resolved catalog key.</summary>
        public ESPermitSet GetPermit(int runtimeKey, bool fallbackValue = true)
        {
            if (superAttributeCatalog == null
                || !superAttributeCatalog.TryGetPermitDefinition(runtimeKey, out ESSuperPermitAttributeDefinition definition))
                return null;

            if (definition.storagePolicy == ESKeyStoragePolicy.HotSlot)
            {
                return ESCharacterAttributeCatalog.TryGetPermitId(definition.enumKey, out ESCharacterPermitAttributeId characterId)
                    ? GetCharacterPermit(characterId, fallbackValue)
                    : null;
            }

            bool resolvedFallbackValue = ResolveSparsePermitFallback(runtimeKey, fallbackValue);
            if (!sparsePermitStats.TryGetValue(runtimeKey, out ESPermitSet set))
            {
                set = new ESPermitSet(resolvedFallbackValue);
                sparsePermitStats.Add(runtimeKey, set);
            }
            else if (set.FallbackValue != resolvedFallbackValue)
            {
                set.FallbackValue = resolvedFallbackValue;
            }

            return set;
        }

        /// <summary>Gets or creates the modifier resolver for a fixed character permit slot.</summary>
        public ESPermitSet GetCharacterPermit(ESCharacterPermitAttributeId id, bool fallbackValue = true)
        {
            if (!ESCharacterAttributeCatalog.IsValid(id))
                return null;

            int index = (int)id;
            bool resolvedFallbackValue = ResolveCharacterPermitFallback(id, fallbackValue);
            ESPermitSet set = characterPermitStats[index];
            if (set == null)
            {
                set = new ESPermitSet(resolvedFallbackValue);
                characterPermitStats[index] = set;
            }
            else if (set.FallbackValue != resolvedFallbackValue)
            {
                set.FallbackValue = resolvedFallbackValue;
            }

            return set;
        }

        /// <summary>Gets the resolved permission value, creating the set with <paramref name="fallbackValue"/> when needed.</summary>
        public bool GetPermitValue(string key, bool fallbackValue = true)
        {
            return GetPermitValue(0, key, fallbackValue);
        }

        public bool GetPermitValue(ushort enumKey, string key, bool fallbackValue = true)
        {
            if (TryResolveCharacterPermitSlot(enumKey, key, out ESCharacterPermitAttributeId characterId))
                return GetCharacterPermitValue(characterId, fallbackValue);

            if (!TryResolvePermitRuntimeKey(enumKey, key, out int runtimeKey))
                return fallbackValue;

            ESPermitSet set = GetPermit(runtimeKey, fallbackValue);
            return set == null ? fallbackValue : set.Value;
        }

        /// <summary>Fixed-slot permit read for hot character paths; no resolver is created for the common no-modifier case.</summary>
        public bool GetCharacterPermitValue(ESCharacterPermitAttributeId id, bool fallbackValue = true)
        {
            if (!ESCharacterAttributeCatalog.IsValid(id))
                return fallbackValue;

            bool resolvedFallbackValue = ResolveCharacterPermitFallback(id, fallbackValue);
            ESPermitSet set = characterPermitStats[(int)id];
            if (set == null)
                return resolvedFallbackValue;

            if (set.FallbackValue != resolvedFallbackValue)
                set.FallbackValue = resolvedFallbackValue;
            return set.Value;
        }

        /// <summary>Gets the resolved permission and the winning rule's metadata.</summary>
        public ESPermitLawResult GetPermitResult(string key, bool fallbackValue = true)
        {
            return GetPermitResult(0, key, fallbackValue);
        }

        public ESPermitLawResult GetPermitResult(ushort enumKey, string key, bool fallbackValue = true)
        {
            if (TryResolveCharacterPermitSlot(enumKey, key, out ESCharacterPermitAttributeId characterId))
            {
                ESPermitSet fixedSet = characterPermitStats[(int)characterId];
                bool resolvedFallbackValue = ResolveCharacterPermitFallback(characterId, fallbackValue);
                if (fixedSet == null)
                    return ESPermitLawResult.Fallback(resolvedFallbackValue);

                if (fixedSet.FallbackValue != resolvedFallbackValue)
                    fixedSet.FallbackValue = resolvedFallbackValue;
                return fixedSet.Result;
            }

            if (!TryResolvePermitRuntimeKey(enumKey, key, out int runtimeKey))
                return ESPermitLawResult.Fallback(fallbackValue);

            ESPermitSet set = GetPermit(runtimeKey, fallbackValue);
            return set == null ? ESPermitLawResult.Fallback(fallbackValue) : set.Result;
        }

        /// <summary>
        /// Clears inactive domain-level ValueChange sets and invalidates their existing tokens.
        /// Active effects must first release their leases so a live Buff cannot be left holding
        /// stale Tokens after a reset or catalog transition.
        /// </summary>
        public void ClearValueChanges()
        {
            if (activeValueChangeEffectCount != 0)
            {
                throw new InvalidOperationException(
                    "Cannot clear ValueChanges while effects are active. Release their EffectLease or remove the owning Buff first.");
            }

            if (isValueChangeResetting)
                throw new InvalidOperationException("ValueChanges are already being reset or rebound.");

            isValueChangeResetting = true;
            try
            {
                for (int i = 0; i < characterFloatStats.Length; i++)
                {
                    ESFloatValueChangeSet set = characterFloatStats[i];
                    if (set != null)
                        set.Clear();
                    characterFloatStats[i] = null;
                }

                foreach (ESFloatValueChangeSet set in sparseFloatStats.Values)
                    set.Clear();
                sparseFloatStats.Clear();

                for (int i = 0; i < characterPermitStats.Length; i++)
                {
                    ESPermitSet set = characterPermitStats[i];
                    if (set != null)
                        set.Clear();
                    characterPermitStats[i] = null;
                }

                foreach (ESPermitSet set in sparsePermitStats.Values)
                    set.Clear();
                sparsePermitStats.Clear();
            }
            finally
            {
                isValueChangeResetting = false;
            }
        }

        private void ReleaseAllValueChangesByOwner(int ownerId)
        {
            for (int i = 0; i < characterFloatStats.Length; i++)
                characterFloatStats[i]?.ReleaseAllByOwner(ownerId);
            foreach (ESFloatValueChangeSet set in sparseFloatStats.Values)
                set.ReleaseAllByOwner(ownerId);

            for (int i = 0; i < characterPermitStats.Length; i++)
                characterPermitStats[i]?.ReleaseAllByOwner(ownerId);
            foreach (ESPermitSet set in sparsePermitStats.Values)
                set.ReleaseAllByOwner(ownerId);
        }

        private bool HasMaterializedValueChangeSets()
        {
            for (int i = 0; i < characterFloatStats.Length; i++)
            {
                if (characterFloatStats[i] != null)
                    return true;
            }
            if (sparseFloatStats.Count != 0)
                return true;

            for (int i = 0; i < characterPermitStats.Length; i++)
            {
                if (characterPermitStats[i] != null)
                    return true;
            }
            return sparsePermitStats.Count != 0;
        }

        /// <summary>
        /// 只读查询现有许可，不创建字典项。适合交互、移动等高频运行时检查。
        /// </summary>
        public bool TryGetPermit(string key, out ESPermitSet set)
        {
            return TryGetPermit(0, key, out set);
        }

        public bool TryGetPermit(ushort enumKey, string key, out ESPermitSet set)
        {
            if (TryResolveCharacterPermitSlot(enumKey, key, out ESCharacterPermitAttributeId characterId))
            {
                set = characterPermitStats[(int)characterId];
                return set != null;
            }

            if (TryResolvePermitRuntimeKey(enumKey, key, out int runtimeKey))
                return TryGetPermit(runtimeKey, out set);

            set = null;
            return false;
        }

        public bool TryGetPermit(int runtimeKey, out ESPermitSet set)
        {
            if (superAttributeCatalog != null
                && superAttributeCatalog.TryGetPermitDefinition(runtimeKey, out ESSuperPermitAttributeDefinition definition)
                && definition.storagePolicy == ESKeyStoragePolicy.HotSlot
                && ESCharacterAttributeCatalog.TryGetPermitId(definition.enumKey, out ESCharacterPermitAttributeId characterId))
            {
                set = characterPermitStats[(int)characterId];
                return set != null;
            }

            return sparsePermitStats.TryGetValue(runtimeKey, out set);
        }

        private float ResolveCharacterFloatBase(ESCharacterFloatAttributeId id, float fallbackBaseValue)
        {
            int index = (int)id;
            if (characterFloatHasExplicitBase[index] != 0)
                return characterFloatExplicitBases[index];

            return characterFloatHasDefinitionBase[index] != 0
                ? characterFloatDefinitionBases[index]
                : fallbackBaseValue;
        }

        private static void ValidateFiniteFloatBase(float baseValue)
        {
            if (float.IsNaN(baseValue) || float.IsInfinity(baseValue))
                throw new System.ArgumentOutOfRangeException(nameof(baseValue), "Entity attribute base value must be finite.");
        }

        private bool ResolveCharacterPermitFallback(ESCharacterPermitAttributeId id, bool fallbackValue)
        {
            int index = (int)id;
            if (characterPermitHasExplicitFallback[index] != 0)
                return characterPermitExplicitFallbacks[index] != 0;

            return characterPermitHasDefinitionFallback[index] != 0
                ? characterPermitDefinitionFallbacks[index] != 0
                : fallbackValue;
        }

        private float ResolveSparseFloatBase(int runtimeKey, float fallbackBaseValue)
        {
            if (sparseFloatExplicitBases.TryGetValue(runtimeKey, out float explicitBase))
                return explicitBase;

            superAttributeCatalog.TryResolveFloatBase(runtimeKey, fallbackBaseValue, out float resolvedBaseValue);
            return resolvedBaseValue;
        }

        private bool ResolveSparsePermitFallback(int runtimeKey, bool fallbackValue)
        {
            if (sparsePermitExplicitFallbacks.TryGetValue(runtimeKey, out bool explicitFallback))
                return explicitFallback;

            superAttributeCatalog.TryResolvePermitFallback(runtimeKey, fallbackValue, out bool resolvedFallbackValue);
            return resolvedFallbackValue;
        }

        private float ClampCharacterFloatValue(ESCharacterFloatAttributeId id, float value)
        {
            int index = (int)id;
            float minimum = characterFloatDefinitionMinimums[index];
            float maximum = characterFloatDefinitionMaximums[index];
            if (value < minimum)
                return minimum;
            return value > maximum ? maximum : value;
        }

        /// <summary>
        /// Resolves authored fixed-slot defaults exactly once per table bind. KCC later reads only
        /// these compact arrays and its existing resolver slots; custom sparse definitions stay in
        /// the Catalog path because they are never part of the motion hot loop.
        /// </summary>
        private void RebuildFixedSlotDefinitionCache()
        {
            Array.Clear(characterFloatDefinitionBases, 0, characterFloatDefinitionBases.Length);
            Array.Clear(characterFloatHasDefinitionBase, 0, characterFloatHasDefinitionBase.Length);
            Array.Clear(characterPermitDefinitionFallbacks, 0, characterPermitDefinitionFallbacks.Length);
            Array.Clear(characterPermitHasDefinitionFallback, 0, characterPermitHasDefinitionFallback.Length);

            for (int i = 0; i < characterFloatDefinitionMinimums.Length; i++)
            {
                characterFloatDefinitionMinimums[i] = float.NegativeInfinity;
                characterFloatDefinitionMaximums[i] = float.PositiveInfinity;
            }

            if (superAttributeCatalog == null)
                return;

            for (int i = 0; i < characterFloatStats.Length; i++)
            {
                ESCharacterFloatAttributeId id = (ESCharacterFloatAttributeId)i;
                ushort enumKey = ESCharacterAttributeCatalog.GetEnumKey(id);
                if (!superAttributeCatalog.TryGetRuntimeKey(enumKey, ESCharacterAttributeCatalog.GetKey(id), out int runtimeKey)
                    || !superAttributeCatalog.TryGetFloatDefinition(runtimeKey, out ESSuperFloatAttributeDefinition definition)
                    || definition.storagePolicy != ESKeyStoragePolicy.HotSlot
                    || definition.enumKey != enumKey)
                {
                    continue;
                }

                characterFloatDefinitionMinimums[i] = definition.minValue;
                characterFloatDefinitionMaximums[i] = definition.maxValue;
                characterFloatDefinitionBases[i] = definition.baseValue;
                characterFloatHasDefinitionBase[i] = definition.overrideBaseValue ? (byte)1 : (byte)0;
            }

            for (int i = 0; i < characterPermitStats.Length; i++)
            {
                ESCharacterPermitAttributeId id = (ESCharacterPermitAttributeId)i;
                ushort enumKey = ESCharacterAttributeCatalog.GetEnumKey(id);
                if (!superAttributeCatalog.TryGetRuntimeKey(enumKey, ESCharacterAttributeCatalog.GetKey(id), out int runtimeKey)
                    || !superAttributeCatalog.TryGetPermitDefinition(runtimeKey, out ESSuperPermitAttributeDefinition definition)
                    || definition.storagePolicy != ESKeyStoragePolicy.HotSlot
                    || definition.enumKey != enumKey
                    || !definition.overrideFallbackValue)
                {
                    continue;
                }

                characterPermitDefinitionFallbacks[i] = definition.fallbackValue ? (byte)1 : (byte)0;
                characterPermitHasDefinitionFallback[i] = 1;
            }
        }

        private bool TryResolveFloatRuntimeKey(ushort enumKey, string key, out int runtimeKey)
        {
            runtimeKey = 0;
            return superAttributeCatalog != null
                   && (enumKey != 0 || !string.IsNullOrEmpty(key))
                   && superAttributeCatalog.TryGetRuntimeKey(enumKey, key, out runtimeKey)
                   && superAttributeCatalog.TryGetFloatDefinition(runtimeKey, out _);
        }

        private bool TryResolvePermitRuntimeKey(ushort enumKey, string key, out int runtimeKey)
        {
            runtimeKey = 0;
            return superAttributeCatalog != null
                   && (enumKey != 0 || !string.IsNullOrEmpty(key))
                   && superAttributeCatalog.TryGetRuntimeKey(enumKey, key, out runtimeKey)
                   && superAttributeCatalog.TryGetPermitDefinition(runtimeKey, out _);
        }

        private bool TryResolveCharacterFloatSlot(ushort enumKey, string key, out ESCharacterFloatAttributeId id)
        {
            bool enumConfigured = enumKey != 0;
            bool stringConfigured = !string.IsNullOrEmpty(key);
            ESCharacterFloatAttributeId enumId = default;
            ESCharacterFloatAttributeId stringId = default;
            bool hasEnum = enumConfigured && ESCharacterAttributeCatalog.TryGetFloatId(enumKey, out enumId);
            bool hasString = stringConfigured && ESCharacterAttributeCatalog.TryGetFloatId(key, out stringId);

            if ((enumConfigured && !hasEnum)
                || (stringConfigured && !hasString)
                || (hasEnum && hasString && enumId != stringId))
            {
                id = default;
                return false;
            }

            id = hasEnum ? enumId : stringId;
            if (!hasEnum && !hasString)
                return false;

            if (superAttributeTable != null && superAttributeCatalog == null)
                return false;

            if (superAttributeCatalog == null)
                return true;

            return superAttributeCatalog.TryGetRuntimeKey(enumKey, key, out int runtimeKey)
                   && superAttributeCatalog.TryGetFloatDefinition(runtimeKey, out ESSuperFloatAttributeDefinition definition)
                   && definition.storagePolicy == ESKeyStoragePolicy.HotSlot
                   && ESCharacterAttributeCatalog.TryGetFloatId(definition.enumKey, out ESCharacterFloatAttributeId catalogId)
                   && catalogId == id;
        }

        private bool TryResolveCharacterPermitSlot(ushort enumKey, string key, out ESCharacterPermitAttributeId id)
        {
            bool enumConfigured = enumKey != 0;
            bool stringConfigured = !string.IsNullOrEmpty(key);
            ESCharacterPermitAttributeId enumId = default;
            ESCharacterPermitAttributeId stringId = default;
            bool hasEnum = enumConfigured && ESCharacterAttributeCatalog.TryGetPermitId(enumKey, out enumId);
            bool hasString = stringConfigured && ESCharacterAttributeCatalog.TryGetPermitId(key, out stringId);

            if ((enumConfigured && !hasEnum)
                || (stringConfigured && !hasString)
                || (hasEnum && hasString && enumId != stringId))
            {
                id = default;
                return false;
            }

            id = hasEnum ? enumId : stringId;
            if (!hasEnum && !hasString)
                return false;

            if (superAttributeTable != null && superAttributeCatalog == null)
                return false;

            if (superAttributeCatalog == null)
                return true;

            return superAttributeCatalog.TryGetRuntimeKey(enumKey, key, out int runtimeKey)
                   && superAttributeCatalog.TryGetPermitDefinition(runtimeKey, out ESSuperPermitAttributeDefinition definition)
                   && definition.storagePolicy == ESKeyStoragePolicy.HotSlot
                   && ESCharacterAttributeCatalog.TryGetPermitId(definition.enumKey, out ESCharacterPermitAttributeId catalogId)
                   && catalogId == id;
        }

        public override void _AwakeRegisterAllModules()
        {
            if (superAttributeTable == null && MyCore != null)
                BindSuperAttributeTable(MyCore.superAttributes);

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
            ClearValueChanges();
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

            if (!sharedData.TryValidateGameTagConfiguration(out string gameTagConfigurationError))
            {
                Debug.LogError("[BuffTag] 已拒绝无效的 Buff GameTag 配置：" + gameTagConfigurationError);
                return null;
            }

            if (!sharedData.TryGetApplyTargetTagCondition(out ESTagConditionRuntime applyCondition, out string requirementError))
            {
                Debug.LogError("[BuffTag] 已拒绝无效的施加目标 Tag 条件：" + requirementError);
                return null;
            }

            if (!applyCondition.IsEmpty
                && (MyCore == null
                    || !MyCore.TryMatchesTagCondition(applyCondition, out bool applies, out requirementError)
                    || !applies))
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
