using System;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{
    public partial class Item
    {
        private struct ItemEffectSlot
        {
            public int generation;
            public bool active;
        }

        [NonSerialized] private ESSuperAttributeCatalog itemAttributeCatalog;
        [NonSerialized] private string itemAttributeError;
        [NonSerialized] private bool waitsForAttributeCatalog;
        [NonSerialized] private ItemDataInfo itemAttributeDefinition;
        [NonSerialized] private bool isResettingAttributes;

        // Hot arrays are created only for an Item that actually touches an Item HotSlot. Sparse
        // values remain fully lazy, so ordinary Items do not pay a per-instance stat allocation.
        [NonSerialized] private ESFloatValueChangeSet[] hotFloatStats;
        [NonSerialized] private ESPermitSet[] hotPermitStats;
        [NonSerialized] private float[] hotFloatBases;
        [NonSerialized] private byte[] hotFloatHasBase;
        [NonSerialized] private byte[] hotPermitFallbacks;
        [NonSerialized] private byte[] hotPermitHasFallback;

        [NonSerialized] private Dictionary<int, ESFloatValueChangeSet> sparseFloatStats;
        [NonSerialized] private Dictionary<int, ESPermitSet> sparsePermitStats;
        [NonSerialized] private Dictionary<int, float> sparseFloatBases;
        [NonSerialized] private Dictionary<int, bool> sparsePermitFallbacks;
        [NonSerialized] private List<ESFloatValueChangeSet> recycledSparseFloatStats;
        [NonSerialized] private List<ESPermitSet> recycledSparsePermitStats;

        [NonSerialized] private List<ItemEffectSlot> attributeEffectSlots;
        [NonSerialized] private List<int> freeAttributeEffectSlots;
        [NonSerialized] private int activeAttributeEffectCount;

        public ESSuperAttributeCatalog AttributeCatalog => itemAttributeCatalog;
        public string AttributeError => itemAttributeError ?? string.Empty;
        public int ActiveAttributeEffectCount => activeAttributeEffectCount;

        /// <summary>
        /// Binds the current GameCore Item Catalog. Binding only occurs at startup, spawn or the
        /// Catalog-ready callback; it is never a gameplay lookup path.
        /// </summary>
        private void EnsureItemAttributes()
        {
            if (ESAttributeRuntimeCatalog.TryGet(ESAttributeBakeTable.ItemScope, out ESSuperAttributeCatalog catalog))
            {
                BindItemAttributeCatalog(catalog);
                UnsubscribeFromAttributeCatalog();
                if (itemAttributeDefinition != null)
                    ApplyItemDefinitionAttributeValues(itemAttributeDefinition);
                return;
            }

            itemAttributeCatalog = null;
            itemAttributeError = "物品属性 Catalog 尚未绑定。GameCore 必须在 Item 启动前完成加载。";
            SubscribeToAttributeCatalog();
        }

        private void BindItemAttributeCatalog(ESSuperAttributeCatalog catalog)
        {
            if (ReferenceEquals(itemAttributeCatalog, catalog))
                return;
            if (activeAttributeEffectCount != 0)
            {
                throw new InvalidOperationException(
                    "Cannot bind a different Item Attribute Catalog while ValueChange effects are active. Release the owning EffectLease first.");
            }

            ResetItemAttributesForLifecycleEnd();
            itemAttributeCatalog = catalog;
            itemAttributeError = catalog == null ? "物品属性 Catalog 缺失。" : null;
        }

        private void SubscribeToAttributeCatalog()
        {
            if (waitsForAttributeCatalog)
                return;

            ESAttributeRuntimeCatalog.CatalogBound += HandleAttributeCatalogBound;
            waitsForAttributeCatalog = true;
        }

        private void UnsubscribeFromAttributeCatalog()
        {
            if (!waitsForAttributeCatalog)
                return;

            ESAttributeRuntimeCatalog.CatalogBound -= HandleAttributeCatalogBound;
            waitsForAttributeCatalog = false;
        }

        private void HandleAttributeCatalogBound()
        {
            EnsureItemAttributes();
        }

        private void BindItemAttributeDefinition(ItemDataInfo definition)
        {
            if (!ReferenceEquals(itemAttributeDefinition, definition))
            {
                if (activeAttributeEffectCount != 0)
                {
                    throw new InvalidOperationException(
                        "Cannot change an Item definition while attribute effects are active. Release the owning EffectLease first.");
                }

                ResetItemAttributesForLifecycleEnd();
            }
            itemAttributeDefinition = definition;
            if (itemAttributeCatalog != null)
                ApplyItemDefinitionAttributeValues(definition);
        }

        private bool CanBindItemAttributeDefinition(ItemDataInfo definition, out string error)
        {
            if (!ReferenceEquals(itemAttributeDefinition, definition) && activeAttributeEffectCount != 0)
            {
                error = "属性效果仍在生效，不能切换 Item 定义。请先释放该 Item 的 EffectLease。";
                return false;
            }
            if (definition == null || itemAttributeCatalog == null)
            {
                error = null;
                return true;
            }
            if (!ESItemAttributeValues.TryValidate(definition.floatValues, definition.permitValues, out error))
                return false;

            for (int i = 0; i < definition.floatValues.Count; i++)
            {
                ESItemFloatValue value = definition.floatValues[i];
                if (float.IsNaN(value.value) || float.IsInfinity(value.value)
                    || !itemAttributeCatalog.TryGetRuntimeKey(value.enumKey, value.key, out int runtimeKey)
                    || !itemAttributeCatalog.TryGetFloatDefinition(runtimeKey, out _))
                {
                    error = "物品 Float 基础值无法解析：" + DescribeAttribute(value.enumKey, value.key);
                    return false;
                }
            }
            for (int i = 0; i < definition.permitValues.Count; i++)
            {
                ESItemPermitValue value = definition.permitValues[i];
                if (!itemAttributeCatalog.TryGetRuntimeKey(value.enumKey, value.key, out int runtimeKey)
                    || !itemAttributeCatalog.TryGetPermitDefinition(runtimeKey, out _))
                {
                    error = "物品 Permit 基础值无法解析：" + DescribeAttribute(value.enumKey, value.key);
                    return false;
                }
            }

            error = null;
            return true;
        }

        private void ApplyItemDefinitionAttributeValues(ItemDataInfo definition)
        {
            if (definition == null || itemAttributeCatalog == null)
                return;
            if (!ESItemAttributeValues.TryValidate(definition.floatValues, definition.permitValues, out string error))
            {
                itemAttributeError = error;
                return;
            }

            // Resolve every entry before touching a base value. A wrong type or missing key must
            // not leave half of an Item definition applied.
            for (int i = 0; i < definition.floatValues.Count; i++)
            {
                ESItemFloatValue value = definition.floatValues[i];
                if (float.IsNaN(value.value) || float.IsInfinity(value.value)
                    || !itemAttributeCatalog.TryGetRuntimeKey(value.enumKey, value.key, out int runtimeKey)
                    || !itemAttributeCatalog.TryGetFloatDefinition(runtimeKey, out _))
                {
                    itemAttributeError = "物品 Float 基础值无法解析：" + DescribeAttribute(value.enumKey, value.key);
                    return;
                }
            }

            for (int i = 0; i < definition.permitValues.Count; i++)
            {
                ESItemPermitValue value = definition.permitValues[i];
                if (!itemAttributeCatalog.TryGetRuntimeKey(value.enumKey, value.key, out int runtimeKey)
                    || !itemAttributeCatalog.TryGetPermitDefinition(runtimeKey, out _))
                {
                    itemAttributeError = "物品 Permit 基础值无法解析：" + DescribeAttribute(value.enumKey, value.key);
                    return;
                }
            }

            for (int i = 0; i < definition.floatValues.Count; i++)
            {
                ESItemFloatValue value = definition.floatValues[i];
                itemAttributeCatalog.TryGetRuntimeKey(value.enumKey, value.key, out int runtimeKey);
                SetFloatBase(runtimeKey, value.value);
            }

            for (int i = 0; i < definition.permitValues.Count; i++)
            {
                ESItemPermitValue value = definition.permitValues[i];
                itemAttributeCatalog.TryGetRuntimeKey(value.enumKey, value.key, out int runtimeKey);
                SetPermitFallback(runtimeKey, value.value);
            }

            itemAttributeError = null;
        }

        /// <summary>Low-frequency stable-key boundary. Cache the resolved RuntimeKey before a hot loop.</summary>
        public bool TryGetAttributeRuntimeKey(ushort enumKey, string key, out int runtimeKey)
        {
            runtimeKey = 0;
            return itemAttributeCatalog != null
                   && (enumKey != 0 || !string.IsNullOrEmpty(key))
                   && itemAttributeCatalog.TryGetRuntimeKey(enumKey, key, out runtimeKey);
        }

        public ESFloatValueChangeSet GetFloatStat(ushort enumKey, string key, float fallbackBaseValue = 0f)
        {
            return TryGetAttributeRuntimeKey(enumKey, key, out int runtimeKey)
                ? GetFloatStat(runtimeKey, fallbackBaseValue)
                : null;
        }

        /// <summary>Runtime-key write path. Hot definitions use a Catalog slot; Sparse definitions allocate only on first use.</summary>
        public ESFloatValueChangeSet GetFloatStat(int runtimeKey, float fallbackBaseValue = 0f)
        {
            ThrowIfResettingAttributes();
            if (itemAttributeCatalog == null
                || !itemAttributeCatalog.TryGetFloatDefinition(runtimeKey, out ESSuperFloatAttributeDefinition definition))
            {
                return null;
            }

            float baseValue = ResolveFloatBase(runtimeKey, fallbackBaseValue);
            if (itemAttributeCatalog.TryGetFloatHotSlot(runtimeKey, out int hotSlot))
            {
                EnsureHotFloatStorage();
                ESFloatValueChangeSet set = hotFloatStats[hotSlot];
                if (set == null)
                {
                    set = new ESFloatValueChangeSet(baseValue);
                    hotFloatStats[hotSlot] = set;
                }
                ConfigureFloat(set, baseValue, definition.minValue, definition.maxValue);
                set.BindEffectLeaseHost(this);
                return set;
            }

            if (sparseFloatStats == null || !sparseFloatStats.TryGetValue(runtimeKey, out ESFloatValueChangeSet sparseSet))
            {
                sparseSet = RentSparseFloat(baseValue, definition.minValue, definition.maxValue);
                (sparseFloatStats ??= new Dictionary<int, ESFloatValueChangeSet>(4)).Add(runtimeKey, sparseSet);
            }
            else
            {
                ConfigureFloat(sparseSet, baseValue, definition.minValue, definition.maxValue);
            }

            sparseSet.BindEffectLeaseHost(this);
            return sparseSet;
        }

        public float GetFloatStatValue(int runtimeKey, float fallbackBaseValue = 0f)
        {
            ThrowIfResettingAttributes();
            if (itemAttributeCatalog == null
                || !itemAttributeCatalog.TryGetFloatDefinition(runtimeKey, out ESSuperFloatAttributeDefinition definition))
            {
                return fallbackBaseValue;
            }

            float baseValue = ResolveFloatBase(runtimeKey, fallbackBaseValue);
            ESFloatValueChangeSet set = null;
            if (itemAttributeCatalog.TryGetFloatHotSlot(runtimeKey, out int hotSlot))
            {
                if (hotFloatStats != null)
                    set = hotFloatStats[hotSlot];
            }
            else if (sparseFloatStats != null)
            {
                sparseFloatStats.TryGetValue(runtimeKey, out set);
            }

            if (set == null)
                return Clamp(baseValue, definition.minValue, definition.maxValue);

            if (set.BaseValue != baseValue)
                set.BaseValue = baseValue;
            return set.Value;
        }

        public bool SetFloatBase(int runtimeKey, float value)
        {
            ThrowIfResettingAttributes();
            if (float.IsNaN(value) || float.IsInfinity(value)
                || itemAttributeCatalog == null
                || !itemAttributeCatalog.TryGetFloatDefinition(runtimeKey, out ESSuperFloatAttributeDefinition definition))
            {
                return false;
            }

            if (itemAttributeCatalog.TryGetFloatHotSlot(runtimeKey, out int hotSlot))
            {
                EnsureHotFloatStorage();
                hotFloatBases[hotSlot] = value;
                hotFloatHasBase[hotSlot] = 1;
                if (hotFloatStats[hotSlot] != null)
                    ConfigureFloat(hotFloatStats[hotSlot], value, definition.minValue, definition.maxValue);
                return true;
            }

            (sparseFloatBases ??= new Dictionary<int, float>(4))[runtimeKey] = value;
            if (sparseFloatStats != null && sparseFloatStats.TryGetValue(runtimeKey, out ESFloatValueChangeSet sparseSet))
                ConfigureFloat(sparseSet, value, definition.minValue, definition.maxValue);
            return true;
        }

        public ESPermitSet GetPermit(ushort enumKey, string key, bool fallbackValue = true)
        {
            return TryGetAttributeRuntimeKey(enumKey, key, out int runtimeKey)
                ? GetPermit(runtimeKey, fallbackValue)
                : null;
        }

        public ESPermitSet GetPermit(int runtimeKey, bool fallbackValue = true)
        {
            ThrowIfResettingAttributes();
            if (itemAttributeCatalog == null
                || !itemAttributeCatalog.TryGetPermitDefinition(runtimeKey, out _))
            {
                return null;
            }

            bool resolvedFallback = ResolvePermitFallback(runtimeKey, fallbackValue);
            if (itemAttributeCatalog.TryGetPermitHotSlot(runtimeKey, out int hotSlot))
            {
                EnsureHotPermitStorage();
                ESPermitSet set = hotPermitStats[hotSlot];
                if (set == null)
                {
                    set = new ESPermitSet(resolvedFallback);
                    hotPermitStats[hotSlot] = set;
                }
                else if (set.FallbackValue != resolvedFallback)
                {
                    set.FallbackValue = resolvedFallback;
                }
                set.BindEffectLeaseHost(this);
                return set;
            }

            if (sparsePermitStats == null || !sparsePermitStats.TryGetValue(runtimeKey, out ESPermitSet sparseSet))
            {
                sparseSet = RentSparsePermit(resolvedFallback);
                (sparsePermitStats ??= new Dictionary<int, ESPermitSet>(4)).Add(runtimeKey, sparseSet);
            }
            else if (sparseSet.FallbackValue != resolvedFallback)
            {
                sparseSet.FallbackValue = resolvedFallback;
            }

            sparseSet.BindEffectLeaseHost(this);
            return sparseSet;
        }

        public bool GetPermitValue(int runtimeKey, bool fallbackValue = true)
        {
            ThrowIfResettingAttributes();
            if (itemAttributeCatalog == null || !itemAttributeCatalog.TryGetPermitDefinition(runtimeKey, out _))
                return fallbackValue;

            bool resolvedFallback = ResolvePermitFallback(runtimeKey, fallbackValue);
            ESPermitSet set = null;
            if (itemAttributeCatalog.TryGetPermitHotSlot(runtimeKey, out int hotSlot))
            {
                if (hotPermitStats != null)
                    set = hotPermitStats[hotSlot];
            }
            else if (sparsePermitStats != null)
            {
                sparsePermitStats.TryGetValue(runtimeKey, out set);
            }

            if (set == null)
                return resolvedFallback;
            if (set.FallbackValue != resolvedFallback)
                set.FallbackValue = resolvedFallback;
            return set.Value;
        }

        public bool SetPermitFallback(int runtimeKey, bool value)
        {
            ThrowIfResettingAttributes();
            if (itemAttributeCatalog == null || !itemAttributeCatalog.TryGetPermitDefinition(runtimeKey, out _))
                return false;

            if (itemAttributeCatalog.TryGetPermitHotSlot(runtimeKey, out int hotSlot))
            {
                EnsureHotPermitStorage();
                hotPermitFallbacks[hotSlot] = value ? (byte)1 : (byte)0;
                hotPermitHasFallback[hotSlot] = 1;
                if (hotPermitStats[hotSlot] != null)
                    hotPermitStats[hotSlot].FallbackValue = value;
                return true;
            }

            (sparsePermitFallbacks ??= new Dictionary<int, bool>(4))[runtimeKey] = value;
            if (sparsePermitStats != null && sparsePermitStats.TryGetValue(runtimeKey, out ESPermitSet sparseSet))
                sparseSet.FallbackValue = value;
            return true;
        }

        /// <summary>
        /// Creates an allocation-free ownership handle for Item modifiers. The lease owns both
        /// writes and release; Item never exposes its reusable internal slot id.
        /// </summary>
        public ESEffectLease CreateAttributeEffectLease()
        {
            ThrowIfResettingAttributes();
            attributeEffectSlots ??= new List<ItemEffectSlot>(4);
            freeAttributeEffectSlots ??= new List<int>(4);
            int lastFree = freeAttributeEffectSlots.Count - 1;
            int slotIndex;
            ItemEffectSlot slot;
            if (lastFree >= 0)
            {
                slotIndex = freeAttributeEffectSlots[lastFree];
                freeAttributeEffectSlots.RemoveAt(lastFree);
                slot = attributeEffectSlots[slotIndex];
            }
            else
            {
                slotIndex = attributeEffectSlots.Count;
                slot = default;
                attributeEffectSlots.Add(slot);
            }

            if (slot.generation == int.MaxValue)
                throw new InvalidOperationException("Item Attribute effect generation exhausted.");

            slot.generation++;
            slot.active = true;
            attributeEffectSlots[slotIndex] = slot;
            activeAttributeEffectCount++;
            return new ESEffectLease(this, slotIndex, slot.generation);
        }

        bool IESEffectLeaseOwner.IsEffectActive(int effectSlot, int generation)
        {
            return IsEffectSlotActive(effectSlot, generation);
        }

        private bool IsEffectSlotActive(int effectSlot, int generation)
        {
            return !isResettingAttributes
                   && attributeEffectSlots != null
                   && (uint)effectSlot < (uint)attributeEffectSlots.Count
                   && attributeEffectSlots[effectSlot].active
                   && attributeEffectSlots[effectSlot].generation == generation;
        }

        bool IESEffectLeaseOwner.TryAddEffectFloat(
            int effectSlot,
            int generation,
            ESFloatValueChangeSet set,
            ESFloatValueChangeOp op,
            float value,
            int sourceId,
            int priority,
            bool enabled,
            out ESValueChangeToken token)
        {
            token = ESValueChangeToken.Invalid;
            if (!IsEffectSlotActive(effectSlot, generation)
                || set == null
                || !set.IsEffectLeaseHost(this))
                return false;

            token = set.Add(op, value, effectSlot + 1, sourceId, priority, enabled);
            return token.IsValid;
        }

        bool IESEffectLeaseOwner.TryAddEffectPermit(
            int effectSlot,
            int generation,
            ESPermitSet set,
            ESPermitLaw law,
            int sourceId,
            int priority,
            bool enabled,
            out ESValueChangeToken token)
        {
            token = ESValueChangeToken.Invalid;
            if (!IsEffectSlotActive(effectSlot, generation)
                || set == null
                || !set.IsEffectLeaseHost(this))
                return false;

            token = set.Add(law, effectSlot + 1, sourceId, priority, enabled);
            return token.IsValid;
        }

        public bool ReleaseEffect(int effectSlot, int generation)
        {
            if (attributeEffectSlots == null || (uint)effectSlot >= (uint)attributeEffectSlots.Count)
                return false;

            ItemEffectSlot slot = attributeEffectSlots[effectSlot];
            if (!slot.active || slot.generation != generation)
                return false;

            slot.active = false;
            attributeEffectSlots[effectSlot] = slot;
            try
            {
                ReleaseAllAttributeValuesByOwner(effectSlot + 1);
            }
            finally
            {
                activeAttributeEffectCount--;
                freeAttributeEffectSlots.Add(effectSlot);
            }
            return true;
        }

        private void ResetItemAttributesForLifecycleEnd()
        {
            if (isResettingAttributes)
                return;

            isResettingAttributes = true;
            try
            {
                InvalidateAttributeEffectSlots();
                ClearAttributeBases();
                ResetAttributeSets();
            }
            finally
            {
                activeAttributeEffectCount = 0;
                isResettingAttributes = false;
            }
        }

        private void InvalidateAttributeEffectSlots()
        {
            if (attributeEffectSlots == null)
                return;

            freeAttributeEffectSlots ??= new List<int>(attributeEffectSlots.Count);
            freeAttributeEffectSlots.Clear();
            for (int i = 0; i < attributeEffectSlots.Count; i++)
            {
                ItemEffectSlot slot = attributeEffectSlots[i];
                slot.active = false;
                attributeEffectSlots[i] = slot;
                freeAttributeEffectSlots.Add(i);
            }
        }

        private void ClearAttributeBases()
        {
            if (hotFloatHasBase != null)
                Array.Clear(hotFloatHasBase, 0, hotFloatHasBase.Length);
            if (hotPermitHasFallback != null)
                Array.Clear(hotPermitHasFallback, 0, hotPermitHasFallback.Length);
            sparseFloatBases?.Clear();
            sparsePermitFallbacks?.Clear();
        }

        private void ResetAttributeSets()
        {
            if (hotFloatStats != null)
            {
                for (int i = 0; i < hotFloatStats.Length; i++)
                    ResetSet(hotFloatStats[i]);
            }
            if (hotPermitStats != null)
            {
                for (int i = 0; i < hotPermitStats.Length; i++)
                    ResetSet(hotPermitStats[i]);
            }
            if (sparseFloatStats != null)
            {
                foreach (ESFloatValueChangeSet set in sparseFloatStats.Values)
                {
                    ResetSet(set);
                    (recycledSparseFloatStats ??= new List<ESFloatValueChangeSet>(4)).Add(set);
                }
                sparseFloatStats.Clear();
            }
            if (sparsePermitStats != null)
            {
                foreach (ESPermitSet set in sparsePermitStats.Values)
                {
                    ResetSet(set);
                    (recycledSparsePermitStats ??= new List<ESPermitSet>(4)).Add(set);
                }
                sparsePermitStats.Clear();
            }
        }

        private void ReleaseAllAttributeValuesByOwner(int ownerId)
        {
            if (hotFloatStats != null)
            {
                for (int i = 0; i < hotFloatStats.Length; i++)
                    ReleaseOwner(hotFloatStats[i], ownerId);
            }
            if (hotPermitStats != null)
            {
                for (int i = 0; i < hotPermitStats.Length; i++)
                    ReleaseOwner(hotPermitStats[i], ownerId);
            }
            if (sparseFloatStats != null)
            {
                foreach (ESFloatValueChangeSet set in sparseFloatStats.Values)
                    ReleaseOwner(set, ownerId);
            }
            if (sparsePermitStats != null)
            {
                foreach (ESPermitSet set in sparsePermitStats.Values)
                    ReleaseOwner(set, ownerId);
            }
        }

        private void EnsureHotFloatStorage()
        {
            int count = itemAttributeCatalog != null ? itemAttributeCatalog.FloatHotSlotCount : 0;
            if (hotFloatStats != null && hotFloatStats.Length == count)
                return;

            hotFloatStats = new ESFloatValueChangeSet[count];
            hotFloatBases = new float[count];
            hotFloatHasBase = new byte[count];
        }

        private void EnsureHotPermitStorage()
        {
            int count = itemAttributeCatalog != null ? itemAttributeCatalog.PermitHotSlotCount : 0;
            if (hotPermitStats != null && hotPermitStats.Length == count)
                return;

            hotPermitStats = new ESPermitSet[count];
            hotPermitFallbacks = new byte[count];
            hotPermitHasFallback = new byte[count];
        }

        private float ResolveFloatBase(int runtimeKey, float fallback)
        {
            if (itemAttributeCatalog.TryGetFloatHotSlot(runtimeKey, out int hotSlot)
                && hotFloatHasBase != null
                && hotFloatHasBase[hotSlot] != 0)
            {
                return hotFloatBases[hotSlot];
            }
            if (sparseFloatBases != null && sparseFloatBases.TryGetValue(runtimeKey, out float explicitBase))
                return explicitBase;

            itemAttributeCatalog.TryResolveFloatBase(runtimeKey, fallback, out float resolved);
            return resolved;
        }

        private bool ResolvePermitFallback(int runtimeKey, bool fallback)
        {
            if (itemAttributeCatalog.TryGetPermitHotSlot(runtimeKey, out int hotSlot)
                && hotPermitHasFallback != null
                && hotPermitHasFallback[hotSlot] != 0)
            {
                return hotPermitFallbacks[hotSlot] != 0;
            }
            if (sparsePermitFallbacks != null && sparsePermitFallbacks.TryGetValue(runtimeKey, out bool explicitFallback))
                return explicitFallback;

            itemAttributeCatalog.TryResolvePermitFallback(runtimeKey, fallback, out bool resolved);
            return resolved;
        }

        private ESFloatValueChangeSet RentSparseFloat(float baseValue, float minimum, float maximum)
        {
            ESFloatValueChangeSet set = null;
            if (recycledSparseFloatStats != null && recycledSparseFloatStats.Count > 0)
            {
                int last = recycledSparseFloatStats.Count - 1;
                set = recycledSparseFloatStats[last];
                recycledSparseFloatStats.RemoveAt(last);
            }

            set ??= new ESFloatValueChangeSet(baseValue);
            set.ResetForReuse();
            ConfigureFloat(set, baseValue, minimum, maximum);
            return set;
        }

        private ESPermitSet RentSparsePermit(bool fallback)
        {
            ESPermitSet set = null;
            if (recycledSparsePermitStats != null && recycledSparsePermitStats.Count > 0)
            {
                int last = recycledSparsePermitStats.Count - 1;
                set = recycledSparsePermitStats[last];
                recycledSparsePermitStats.RemoveAt(last);
            }

            if (set == null)
                return new ESPermitSet(fallback);

            set.ResetForReuse();
            set.FallbackValue = fallback;
            return set;
        }

        private static void ConfigureFloat(ESFloatValueChangeSet set, float baseValue, float minimum, float maximum)
        {
            if (set.BaseValue != baseValue)
                set.BaseValue = baseValue;
            if (set.MinimumValue != minimum || set.MaximumValue != maximum)
                set.SetBounds(minimum, maximum);
        }

        private static float Clamp(float value, float minimum, float maximum)
        {
            if (value < minimum)
                return minimum;
            return value > maximum ? maximum : value;
        }

        private static void ResetSet(ESFloatValueChangeSet set)
        {
            if (set == null)
                return;
            try { set.ResetForReuse(); }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        private static void ResetSet(ESPermitSet set)
        {
            if (set == null)
                return;
            try { set.ResetForReuse(); }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        private static void ReleaseOwner(ESFloatValueChangeSet set, int ownerId)
        {
            if (set == null)
                return;
            try { set.ReleaseAllByOwner(ownerId); }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        private static void ReleaseOwner(ESPermitSet set, int ownerId)
        {
            if (set == null)
                return;
            try { set.ReleaseAllByOwner(ownerId); }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        private void ThrowIfResettingAttributes()
        {
            if (isResettingAttributes)
                throw new InvalidOperationException("Cannot create or modify Item attributes while the Item is resetting.");
        }

        private static string DescribeAttribute(ushort enumKey, string key)
        {
            return enumKey != 0 && !string.IsNullOrEmpty(key)
                ? "Enum=" + enumKey + " | String=" + key
                : enumKey != 0 ? "Enum=" + enumKey : "String=" + key;
        }
    }
}
